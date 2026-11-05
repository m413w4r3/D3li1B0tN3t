using D3B.Diagnostic.Contract;
using D3B.Protocol.Readable;
using Google.Protobuf.Reflection;
using System.Globalization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Deterministically compares one build schema with readable descriptors and read-only references.</summary>
public sealed class ProtocolResolver
{
    private const int MaxNearCandidates = 16;
    private const int MaxDepth = 32;
    private readonly IReadOnlyList<FileDescriptor> _readableFiles;
    private readonly IReadOnlyList<MessageDescriptor> _readableMessages;
    private readonly IReadOnlyList<ReferenceMessage> _references;
    private readonly IReadOnlyList<PriorMapping> _pins;

    /// <summary>Creates an offline resolver with no network or persistence dependencies.</summary>
    public ProtocolResolver(
        IReadOnlyList<FileDescriptor> readableFiles,
        IEnumerable<ReferenceCorpus> references,
        IEnumerable<PriorMapping> pins)
    {
        ArgumentNullException.ThrowIfNull(readableFiles);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(pins);
        _readableFiles = readableFiles;
        _references = references
            .SelectMany(corpus => corpus.Messages.Select(message => message with
            {
                Source = string.IsNullOrWhiteSpace(message.Source) ? corpus.Source : message.Source,
                Build = string.Equals(message.Build, "other/unknown", StringComparison.Ordinal) ? corpus.Build : message.Build,
                BuildExact = message.BuildExact || (string.Equals(message.Build, "other/unknown", StringComparison.Ordinal) && corpus.BuildExact)
            }))
            .ToArray();
        _pins = pins.ToArray();
        _readableMessages = WalkMessages(_readableFiles.SelectMany(file => file.MessageTypes))
            .Where(message => !IsMapEntry(message))
            .OrderBy(message => message.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Resolves a build schema against analyses, references and prior build pins.</summary>
    public CandidateReport Resolve(
        DiagnosticSchema buildSchema,
        IReadOnlyList<DiagnosticAnalysis> analyses,
        string? targetDirection,
        DiagnosticSummary? observationSummary = null,
        IReadOnlyList<ResolverObservation>? observations = null)
    {
        ArgumentNullException.ThrowIfNull(buildSchema);
        ArgumentNullException.ThrowIfNull(analyses);

        Dictionary<string, DiagnosticMessage> schemaMessages = buildSchema.Messages
            .OrderBy(message => message.Name, StringComparer.Ordinal)
            .ToDictionary(message => message.Name, StringComparer.Ordinal);
        DiagnosticMessage[] candidateSchemaMessages = schemaMessages.Values
            .Where(message => !message.MapEntry)
            .ToArray();
        bool useAggregatedObservations = observations is not null;
        Dictionary<string, ResolverObservation> observationsByToken = observations is null
            ? new Dictionary<string, ResolverObservation>(StringComparer.Ordinal)
            : MapObservations(schemaMessages, observations);
        List<ResolverSchemaPair> namedPairs = [];
        List<ResolverSchemaPair> exactPairs = [];
        List<ResolverSchemaPair> nearPairs = [];

        foreach (DiagnosticMessage schemaMessage in candidateSchemaMessages)
        {
            string token = schemaMessage.Name;
            foreach (ReferenceMessage reference in ReferencesFor(token))
            {
                foreach (MessageDescriptor readable in ReadableBySimpleName(SimpleName(reference.Name)))
                {
                    namedPairs.Add(new ResolverSchemaPair(schemaMessage, readable, reference, null));
                }
            }

            foreach (PriorMapping pin in PinsFor(token))
            {
                foreach (MessageDescriptor readable in ReadableByPinName(pin.ReadableFullName))
                {
                    namedPairs.Add(new ResolverSchemaPair(schemaMessage, readable, null, pin));
                }
            }

            foreach (MessageDescriptor readable in _readableMessages)
            {
                ResolverShapeResult shape = Compare(schemaMessage, readable, schemaMessages);
                if (shape.Exact)
                {
                    exactPairs.Add(new ResolverSchemaPair(schemaMessage, readable, null, null, shape));
                }
                else if (shape.Score > 0)
                {
                    nearPairs.Add(new ResolverSchemaPair(schemaMessage, readable, null, null, shape));
                }
            }
        }

        ResolverSchemaPair[] nearSelection = nearPairs
            .OrderByDescending(pair => pair.Shape?.Score ?? 0)
            .ThenBy(pair => pair.Schema.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Readable!.FullName, StringComparer.Ordinal)
            .Take(MaxNearCandidates)
            .ToArray();
        List<ResolverSchemaPair> selected = namedPairs.Concat(exactPairs).Concat(nearSelection)
            .DistinctBy(pair => (pair.Schema.Name, pair.Readable!.FullName, pair.Reference?.Source, pair.Pin?.ReadableFullName))
            .ToList();

        HashSet<string> selectedTokens = selected.Select(pair => pair.Schema.Name).ToHashSet(StringComparer.Ordinal);
        foreach (string token in analyses.Select(analysis => analysis.CarriedType).Where(type => type is not null).Cast<string>())
        {
            DiagnosticMessage? message = FindSchemaMessage(schemaMessages, token);
            if (message is not null && !selectedTokens.Contains(message.Name))
            {
                selected.Add(new ResolverSchemaPair(message, null, null, null));
                selectedTokens.Add(message.Name);
            }
        }

        foreach (string token in observationsByToken.Keys.OrderBy(token => token, StringComparer.Ordinal))
        {
            if (!selectedTokens.Contains(token))
            {
                DiagnosticMessage message = schemaMessages[token];
                selected.Add(new ResolverSchemaPair(message, null, null, null));
                selectedTokens.Add(token);
            }
        }

        if (selected.Count == 0 && candidateSchemaMessages.Length > 0)
        {
            DiagnosticMessage fallback = candidateSchemaMessages[0];
            selected.Add(new ResolverSchemaPair(fallback, null, null, null));
        }

        CandidateObservationSummary summary = MakeObservationSummary(analyses, observationSummary);
        List<MappingCandidate> candidates = [];
        foreach (IGrouping<string, ResolverSchemaPair> tokenGroup in selected.GroupBy(pair => pair.Schema.Name, StringComparer.Ordinal))
        {
            DiagnosticMessage schemaMessage = tokenGroup.First().Schema;
            List<ResolverSchemaPair> pairs = tokenGroup.Where(pair => pair.Readable is not null).ToList();
            List<MessageDescriptor> exactReadables = _readableMessages
                .Where(readable => Compare(schemaMessage, readable, schemaMessages).Exact)
                .ToList();
            HashSet<string> conflictingNames = ConflictingReferenceNames(schemaMessage.Name);
            foreach (ResolverSchemaPair pair in tokenGroup.OrderBy(pair => pair.Readable?.FullName, StringComparer.Ordinal))
            {
                if (pair.Readable is null)
                {
                    observationsByToken.TryGetValue(schemaMessage.Name, out ResolverObservation? observation);
                    candidates.Add(CreateUnknown(
                        buildSchema,
                        schemaMessage,
                        analyses,
                        targetDirection,
                        observation,
                        useAggregatedObservations,
                        summary));
                    continue;
                }

                ResolverSchemaPair[] relatedPairs = tokenGroup
                    .Where(candidatePair => candidatePair.Readable is not null
                        && string.Equals(candidatePair.Readable.FullName, pair.Readable.FullName, StringComparison.Ordinal))
                    .ToArray();
                candidates.Add(CreateCandidate(
                    buildSchema,
                    schemaMessage,
                    pair,
                    relatedPairs,
                    exactReadables,
                    conflictingNames,
                    analyses,
                    targetDirection,
                    summary,
                    schemaMessages,
                    observationsByToken.GetValueOrDefault(schemaMessage.Name),
                    useAggregatedObservations));
            }
        }

        MappingCandidate[] ordered = candidates
            .DistinctBy(candidate => (candidate.Token, candidate.Readable))
            .OrderBy(candidate => candidate.Token, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Readable, StringComparer.Ordinal)
            .ToArray();

        return new CandidateReport
        {
            ClientVersion = buildSchema.ClientVersion,
            SchemaSha256 = buildSchema.SchemaSha256,
            Observations = summary,
            Candidates = ordered,
            Summary = new CandidateSummary
            {
                Verified = ordered.Count(candidate => candidate.Classification == CandidateClassification.Verified),
                Candidate = ordered.Count(candidate => candidate.Classification == CandidateClassification.Candidate),
                Conflict = ordered.Count(candidate => candidate.Classification == CandidateClassification.Conflict),
                Unknown = ordered.Count(candidate => candidate.Classification == CandidateClassification.Unknown)
            }
        };
    }

    private static MappingCandidate CreateCandidate(
        DiagnosticSchema schema,
        DiagnosticMessage schemaMessage,
        ResolverSchemaPair pair,
        IReadOnlyList<ResolverSchemaPair> relatedPairs,
        List<MessageDescriptor> exactReadables,
        HashSet<string> conflictingNames,
        IReadOnlyList<DiagnosticAnalysis> analyses,
        string? targetDirection,
        CandidateObservationSummary observations,
        IReadOnlyDictionary<string, DiagnosticMessage> schemaMessages,
        ResolverObservation? aggregateObservation,
        bool useAggregatedObservations)
    {
        MessageDescriptor readable = pair.Readable!;
        ResolverShapeResult shape = Compare(schemaMessage, readable, schemaMessages);
        List<ResolverContradiction> contradictions = [.. shape.Contradictions];
        List<ResolverUncertainty> uncertainties = [];
        List<CandidateEvidence> evidence =
        [
            new CandidateEvidence
            {
                Source = "schema",
                Build = schema.ClientVersion,
                Verified = IsBuildExact(schema),
                Provenance = "build schema",
                Detail = DescribeSchema(schemaMessage)
            },
            new CandidateEvidence
            {
                Source = "readable",
                Build = schema.ClientVersion,
                Verified = IsBuildExact(schema),
                Provenance = "readable descriptor",
                Detail = DescribeReadable(readable)
            }
        ];

        foreach (ReferenceMessage reference in relatedPairs
            .Where(related => related.Reference is not null)
            .Select(related => related.Reference!)
            .DistinctBy(reference => (reference.Source, reference.Name, reference.Build)))
        {
            bool refBuildExact = reference.BuildExact
                && string.Equals(reference.Build, schema.ClientVersion, StringComparison.Ordinal);
            evidence.Add(new CandidateEvidence
            {
                Source = "reference",
                Build = reference.Build,
                Verified = refBuildExact,
                Provenance = reference.Source,
                Detail = "reference name has the same simple message name"
            });
            if (!refBuildExact)
            {
                uncertainties.Add(new ResolverUncertainty
                {
                    Code = ResolverUncertaintyCode.BuildMismatchReference,
                    Detail = "reference does not identify this exact build"
                });
            }
        }

        foreach (PriorMapping pin in relatedPairs
            .Where(related => related.Pin is not null)
            .Select(related => related.Pin!)
            .DistinctBy(pin => (pin.ReadableFullName, pin.ObfuscatedToken, pin.Build)))
        {
            bool pinExact = pin.BuildExact && string.Equals(pin.Build, schema.ClientVersion, StringComparison.Ordinal);
            evidence.Add(new CandidateEvidence
            {
                Source = "prior_mapping",
                Build = pin.Build,
                Verified = pinExact,
                Provenance = "pinned pair",
                Detail = "readable full name " + pin.ReadableFullName + " and obfuscated token are pinned"
            });
            if (!pinExact)
            {
                uncertainties.Add(new ResolverUncertainty
                {
                    Code = ResolverUncertaintyCode.BuildMismatchReference,
                    Detail = "prior mapping does not identify this exact build"
                });
            }
        }

        string token = schemaMessage.Name;
        DiagnosticAnalysis[] tokenAnalyses = FindAnalyses(analyses, token).ToArray();
        bool hasValidObservation = useAggregatedObservations
            ? aggregateObservation?.FullyValid == true
            : tokenAnalyses.Any(IsValidObservation);
        string? direction = useAggregatedObservations
            ? ObservationDirection(aggregateObservation, targetDirection)
            : tokenAnalyses.Select(analysis => analysis.Direction).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? targetDirection;
        if (useAggregatedObservations)
        {
            if (aggregateObservation is not null)
            {
                evidence.Add(ObservationEvidence(schema.ClientVersion, aggregateObservation));
                AddObservationDiagnostics(aggregateObservation, contradictions, uncertainties);
            }
            else
            {
                uncertainties.Add(new ResolverUncertainty
                {
                    Code = ResolverUncertaintyCode.MissingValidObservation,
                    Detail = "no VALID analysis supports this token"
                });
            }
        }
        else
        {
            foreach (DiagnosticAnalysis analysis in tokenAnalyses)
            {
                bool valid = IsValidObservation(analysis);
                evidence.Add(new CandidateEvidence
                {
                    Source = "observation",
                    Build = schema.ClientVersion,
                    Verified = valid,
                    Provenance = "diagnostic analysis",
                    Detail = valid ? "analysis status VALID with valid type URL; direction " + SafeDirection(analysis.Direction) : "analysis status " + analysis.Status + "; type URL valid " + analysis.TypeUrlOk
                });
            }
        }

        string simpleName = SimpleName(readable.Name);
        bool directionContradiction = useAggregatedObservations
            ? aggregateObservation is not null && HasDirectionContradiction(simpleName, aggregateObservation)
            : HasDirectionContradiction(simpleName, tokenAnalyses);
        if (directionContradiction)
        {
            contradictions.Add(new ResolverContradiction
            {
                Code = ResolverContradictionCode.DirectionContradiction,
                Detail = "observed direction contradicts the readable message suffix"
            });
        }

        if (conflictingNames.Count > 1)
        {
            contradictions.Add(new ResolverContradiction
            {
                Code = ResolverContradictionCode.ConflictingReferences,
                Detail = "references name different readable messages for this token"
            });
        }

        bool exactShape = shape.Exact;
        bool uniqueShape = exactShape && exactReadables.Count == 1;
        if (IsPartial(readable) || ContainsPartialNested(readable, new HashSet<string>(StringComparer.Ordinal), 0))
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.PartialReadable,
                Detail = "readable descriptor or nested graph is partial"
            });
        }

        if (exactShape && exactReadables.Count > 1)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.AmbiguousReadableShape,
                Detail = "multiple readable descriptors have the same exact shape"
            });
        }

        if (!hasValidObservation)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.MissingValidObservation,
                Detail = "no VALID analysis supports this token"
            });
        }

        if (!string.Equals(observations.Coverage, "complete", StringComparison.Ordinal) || observations.Dropped > 0)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.PartialObservation,
                Detail = "observation coverage is partial"
            });
        }

        bool buildExact = IsBuildExact(schema);
        CandidateClassification classification = contradictions.Count > 0
            ? CandidateClassification.Conflict
            : exactShape && uniqueShape && buildExact && hasValidObservation
                && observations.Dropped == 0
                && string.Equals(observations.Coverage, "complete", StringComparison.Ordinal)
                && !uncertainties.Any(uncertainty => uncertainty.Code is ResolverUncertaintyCode.PartialReadable
                    or ResolverUncertaintyCode.BuildMismatchReference
                    or ResolverUncertaintyCode.PartialObservation)
                ? CandidateClassification.Verified
                : CandidateClassification.Candidate;

        SyntheticFixtureProposal? fixture = classification == CandidateClassification.Verified
            ? BuildFixture(schema, schemaMessage)
            : null;
        if (classification == CandidateClassification.Verified && fixture is null)
        {
            classification = CandidateClassification.Candidate;
        }

        return new MappingCandidate
        {
            Token = token,
            Readable = readable.FullName,
            Direction = direction,
            Classification = classification,
            Score = new HeuristicScore { Value = shape.Score, Kind = "heuristic" },
            ClientVersion = schema.ClientVersion,
            SchemaSha256 = schema.SchemaSha256,
            BuildExact = buildExact,
            ExactShape = exactShape,
            UniqueShape = uniqueShape,
            HasValidObservation = hasValidObservation,
            Evidence = evidence,
            Contradictions = contradictions.DistinctBy(item => (item.Code, item.Detail)).ToArray(),
            Uncertainties = uncertainties.DistinctBy(item => (item.Code, item.Detail)).ToArray(),
            FixtureProposal = fixture
        };
    }

    private static bool IsValidObservation(DiagnosticAnalysis analysis)
    {
        return analysis.Status == DiagnosticStatus.Valid && analysis.TypeUrlOk;
    }

    private static MappingCandidate CreateUnknown(
        DiagnosticSchema buildSchema,
        DiagnosticMessage schema,
        IReadOnlyList<DiagnosticAnalysis> analyses,
        string? targetDirection,
        ResolverObservation? observation,
        bool useAggregatedObservations,
        CandidateObservationSummary summary)
    {
        string? direction = useAggregatedObservations
            ? ObservationDirection(observation, targetDirection)
            : FindAnalyses(analyses, schema.Name).Select(analysis => analysis.Direction).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? targetDirection;
        List<ResolverContradiction> contradictions = [];
        List<ResolverUncertainty> uncertainties = [];
        List<CandidateEvidence> evidence = [];
        if (useAggregatedObservations)
        {
            if (observation is not null)
            {
                evidence.Add(ObservationEvidence(buildSchema.ClientVersion, observation));
                AddObservationDiagnostics(observation, contradictions, uncertainties);
                if (!observation.FullyValid)
                {
                    uncertainties.Add(new ResolverUncertainty
                    {
                        Code = ResolverUncertaintyCode.MissingValidObservation,
                        Detail = "no VALID analysis supports this token"
                    });
                }
            }

            if (!string.Equals(summary.Coverage, "complete", StringComparison.Ordinal) || summary.Dropped > 0)
            {
                uncertainties.Add(new ResolverUncertainty
                {
                    Code = ResolverUncertaintyCode.PartialObservation,
                    Detail = "observation coverage is partial"
                });
            }
        }

        return new MappingCandidate
        {
            Token = schema.Name,
            Direction = direction,
            Classification = CandidateClassification.Unknown,
            ClientVersion = buildSchema.ClientVersion,
            SchemaSha256 = buildSchema.SchemaSha256,
            BuildExact = IsBuildExact(buildSchema),
            Score = new HeuristicScore { Value = 0, Kind = "heuristic" },
            Evidence = evidence,
            Contradictions = contradictions,
            Uncertainties = uncertainties
        };
    }

    private static Dictionary<string, ResolverObservation> MapObservations(
        IReadOnlyDictionary<string, DiagnosticMessage> schemaMessages,
        IReadOnlyList<ResolverObservation> observations)
    {
        List<(string Token, ResolverObservation Observation)> mapped = [];
        foreach (ResolverObservation observation in observations)
        {
            if (observation is null)
            {
                continue;
            }

            DiagnosticMessage? message = FindObservationSchemaMessage(schemaMessages, observation.Token);
            if (message is not null)
            {
                mapped.Add((message.Name, observation));
            }
        }

        Dictionary<string, ResolverObservation> result = new(StringComparer.Ordinal);
        foreach (IGrouping<string, (string Token, ResolverObservation Observation)> group in mapped
            .GroupBy(item => item.Token, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            ResolverObservation[] values = group.Select(item => item.Observation).ToArray();
            if (values.Length == 1)
            {
                result.Add(group.Key, values[0] with { Token = group.Key });
                continue;
            }

            result.Add(group.Key, new ResolverObservation
            {
                Token = group.Key,
                Occurrences = values.Aggregate(0L, (count, value) => AddCount(count, value.Occurrences)),
                Directions = values.SelectMany(value => value.Directions)
                    .GroupBy(direction => direction.Direction, StringComparer.Ordinal)
                    .OrderBy(grouping => grouping.Key, StringComparer.Ordinal)
                    .Select(grouping => new ResolverObservationDirection
                    {
                        Direction = grouping.Key,
                        Occurrences = grouping.Aggregate(0L, (count, value) => AddCount(count, value.Occurrences))
                    }).ToArray(),
                Statuses = values.SelectMany(value => value.Statuses)
                    .GroupBy(status => status.Status)
                    .OrderBy(grouping => StatusName(grouping.Key), StringComparer.Ordinal)
                    .Select(grouping => new ResolverObservationStatus
                    {
                        Status = grouping.Key,
                        Occurrences = grouping.Aggregate(0L, (count, value) => AddCount(count, value.Occurrences))
                    }).ToArray(),
                TypeUrlOkOccurrences = values.Aggregate(0L, (count, value) => AddCount(count, value.TypeUrlOkOccurrences)),
                Problems = values.SelectMany(value => value.Problems)
                    .GroupBy(problem => (problem.Code, problem.FieldPath))
                    .OrderBy(grouping => ProblemName(grouping.Key.Code), StringComparer.Ordinal)
                    .ThenBy(grouping => grouping.Key.FieldPath, StringComparer.Ordinal)
                    .Select(grouping => new ResolverObservationProblem
                    {
                        Code = grouping.Key.Code,
                        FieldPath = grouping.Key.FieldPath,
                        Occurrences = grouping.Aggregate(0L, (count, value) => AddCount(count, value.Occurrences))
                    }).ToArray(),
                Truncated = values.Any(value => value.Truncated)
            });
        }

        return result;
    }

    private static DiagnosticMessage? FindObservationSchemaMessage(
        IReadOnlyDictionary<string, DiagnosticMessage> schemaMessages,
        string token)
    {
        if (schemaMessages.TryGetValue(token, out DiagnosticMessage? exact))
        {
            return exact;
        }

        DiagnosticMessage[] matches = schemaMessages.Values
            .Where(message => string.Equals(SimpleName(message.Name), SimpleName(token), StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string? ObservationDirection(ResolverObservation? observation, string? targetDirection)
    {
        if (observation is null)
        {
            return targetDirection;
        }

        ResolverObservationDirection[] directions = observation.Directions.Where(direction => direction.Occurrences > 0).ToArray();
        if (directions.Length == 0)
        {
            return targetDirection;
        }

        return directions.Length == 1 && IsKnownDirection(directions[0].Direction)
            ? directions[0].Direction
            : null;
    }

    private static bool HasDirectionContradiction(string readableName, ResolverObservation observation)
    {
        return observation.Directions.Any(direction => direction.Occurrences > 0
            && ((readableName.EndsWith("Request", StringComparison.Ordinal) && IsDirection(direction.Direction, "server"))
                || ((readableName.EndsWith("Event", StringComparison.Ordinal) || readableName.EndsWith("Response", StringComparison.Ordinal))
                    && IsDirection(direction.Direction, "client"))));
    }

    private static bool HasDirectionContradiction(string readableName, IReadOnlyList<DiagnosticAnalysis> analyses)
    {
        return (readableName.EndsWith("Request", StringComparison.Ordinal) && analyses.Any(analysis => IsDirection(analysis.Direction, "server")))
            || ((readableName.EndsWith("Event", StringComparison.Ordinal) || readableName.EndsWith("Response", StringComparison.Ordinal))
                && analyses.Any(analysis => IsDirection(analysis.Direction, "client")));
    }

    private static CandidateEvidence ObservationEvidence(string build, ResolverObservation observation)
    {
        List<string> details = [FormatCount(Math.Max(0, observation.Occurrences)) + " observations"];
        ResolverObservationDirection[] directions = observation.Directions
            .Where(direction => direction.Occurrences > 0)
            .OrderBy(direction => SafeObservationDirection(direction.Direction), StringComparer.Ordinal)
            .ToArray();
        details.Add("directions " + (directions.Length == 0
            ? "none"
            : string.Join(", ", directions.Select(direction => SafeObservationDirection(direction.Direction) + "=" + FormatCount(direction.Occurrences)))));

        ResolverObservationStatus[] statuses = observation.Statuses
            .Where(status => status.Occurrences > 0)
            .OrderBy(status => StatusName(status.Status), StringComparer.Ordinal)
            .ToArray();
        details.Add("statuses " + (statuses.Length == 0
            ? "none"
            : string.Join(", ", statuses.Select(status => StatusName(status.Status) + "=" + FormatCount(status.Occurrences)))));
        details.Add("typeUrlOk " + FormatCount(Math.Max(0, observation.TypeUrlOkOccurrences)) + "/" + FormatCount(Math.Max(0, observation.Occurrences)));

        ResolverObservationProblem[] problems = observation.Problems
            .Where(problem => problem.Occurrences > 0 && Enum.IsDefined(problem.Code))
            .OrderBy(problem => ProblemName(problem.Code), StringComparer.Ordinal)
            .ThenBy(problem => SafeFieldPath(problem.FieldPath), StringComparer.Ordinal)
            .ToArray();
        details.Add("problems " + (problems.Length == 0
            ? "none"
            : string.Join(", ", problems.Select(problem => ProblemName(problem.Code) + "=" + FormatCount(problem.Occurrences)
                + (SafeFieldPath(problem.FieldPath) is { } path ? "@" + path : string.Empty)))));

        return new CandidateEvidence
        {
            Source = "observation",
            Build = build,
            Verified = observation.FullyValid,
            Provenance = "diagnostic report aggregate",
            Detail = string.Join("; ", details)
        };
    }

    private static void AddObservationDiagnostics(
        ResolverObservation observation,
        List<ResolverContradiction> contradictions,
        List<ResolverUncertainty> uncertainties)
    {
        ResolverObservationStatus[] statuses = observation.Statuses.Where(status => status.Occurrences > 0).ToArray();
        ResolverObservationDirection[] directions = observation.Directions.Where(direction => direction.Occurrences > 0).ToArray();
        ResolverObservationProblem[] problems = observation.Problems
            .Where(problem => problem.Occurrences > 0 && Enum.IsDefined(problem.Code))
            .ToArray();

        if (observation.TypeUrlOkOccurrences < observation.Occurrences)
        {
            AddContradiction(contradictions, ResolverContradictionCode.ObservationTypeUrlMismatch,
                FormatCount(Math.Max(0, observation.TypeUrlOkOccurrences)) + " of "
                + FormatCount(Math.Max(0, observation.Occurrences)) + " observations have valid type URLs");
        }

        DiagnosticProblemCode[] hardProblems = observation.Problems.Select(problem => problem.Code)
            .Where(IsHardProblem)
            .Distinct()
            .OrderBy(ProblemName, StringComparer.Ordinal)
            .ToArray();
        if (hardProblems.Length > 0)
        {
            string[] paths = observation.Problems.Where(problem => IsHardProblem(problem.Code))
                .Select(problem => SafeFieldPath(problem.FieldPath))
                .Where(path => path is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string detail = string.Join(", ", hardProblems.Select(ProblemName));
            if (paths.Length > 0)
            {
                detail += "; field paths " + string.Join(", ", paths);
            }

            AddContradiction(contradictions, ResolverContradictionCode.ObservationProblem, detail);
        }

        DiagnosticStatus[] hardStatuses = observation.Statuses.Select(status => status.Status)
            .Where(IsHardStatus)
            .Distinct()
            .OrderBy(StatusName, StringComparer.Ordinal)
            .ToArray();
        if (hardStatuses.Length > 0)
        {
            AddContradiction(contradictions, ResolverContradictionCode.ObservationStatusMismatch,
                "hard statuses " + string.Join(", ", hardStatuses.Select(StatusName)));
        }

        if (statuses.Length > 1)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.MixedObservationStatus,
                Detail = "multiple diagnostic statuses are present"
            });
        }

        if (directions.Length > 1)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.AmbiguousObservationDirection,
                Detail = "multiple observation directions are present"
            });
        }

        bool softStatus = statuses.Any(status => IsSoftStatus(status.Status));
        bool softProblem = problems.Any(problem => IsSoftProblem(problem.Code));
        if (observation.Truncated || softStatus || softProblem)
        {
            uncertainties.Add(new ResolverUncertainty
            {
                Code = ResolverUncertaintyCode.ObservationTruncated,
                Detail = "observation detail is truncated or has a soft limit condition"
            });
        }
    }

    private static bool IsHardProblem(DiagnosticProblemCode code)
    {
        return code is DiagnosticProblemCode.UnknownType
            or DiagnosticProblemCode.UnknownFieldNumber
            or DiagnosticProblemCode.WireTypeMismatch
            or DiagnosticProblemCode.PackedNotAllowed
            or DiagnosticProblemCode.InvalidUtf8
            or DiagnosticProblemCode.GroupUnsupported
            or DiagnosticProblemCode.LengthOverflow
            or DiagnosticProblemCode.VarintOverflow
            or DiagnosticProblemCode.Malformed
            or DiagnosticProblemCode.SchemaMessageMissing
            or DiagnosticProblemCode.SchemaInvalid;
    }

    private static bool IsSoftProblem(DiagnosticProblemCode code)
    {
        return code is DiagnosticProblemCode.DepthExceeded or DiagnosticProblemCode.Truncated or DiagnosticProblemCode.LimitExceeded;
    }

    private static bool IsHardStatus(DiagnosticStatus status)
    {
        return status is DiagnosticStatus.SchemaMismatch
            or DiagnosticStatus.Malformed
            or DiagnosticStatus.TrailingBytes
            or DiagnosticStatus.UnknownType;
    }

    private static bool IsSoftStatus(DiagnosticStatus status)
    {
        return status is DiagnosticStatus.Truncated or DiagnosticStatus.LimitExceeded;
    }

    private static bool IsKnownDirection(string direction)
    {
        return direction is "client" or "server" or "d3b";
    }

    private static string SafeObservationDirection(string direction)
    {
        return direction is "client" or "server" or "d3b" or "unknown" ? direction : "unknown";
    }

    private static string? SafeFieldPath(string? fieldPath)
    {
        return fieldPath is { Length: > 0 and <= 128 }
            && fieldPath.All(character => char.IsAsciiDigit(character) || character == '.')
            ? fieldPath
            : null;
    }

    private static string StatusName(DiagnosticStatus status)
    {
        return status switch
        {
            DiagnosticStatus.Valid => "VALID",
            DiagnosticStatus.SchemaMismatch => "SCHEMA_MISMATCH",
            DiagnosticStatus.Malformed => "MALFORMED",
            DiagnosticStatus.Truncated => "TRUNCATED",
            DiagnosticStatus.TrailingBytes => "TRAILING_BYTES",
            DiagnosticStatus.UnknownType => "UNKNOWN_TYPE",
            DiagnosticStatus.LimitExceeded => "LIMIT_EXCEEDED",
            _ => "UNKNOWN_STATUS"
        };
    }

    private static string ProblemName(DiagnosticProblemCode code)
    {
        return code switch
        {
            DiagnosticProblemCode.UnknownType => "UNKNOWN_TYPE",
            DiagnosticProblemCode.UnknownFieldNumber => "UNKNOWN_FIELD_NUMBER",
            DiagnosticProblemCode.WireTypeMismatch => "WIRE_TYPE_MISMATCH",
            DiagnosticProblemCode.PackedNotAllowed => "PACKED_NOT_ALLOWED",
            DiagnosticProblemCode.InvalidUtf8 => "INVALID_UTF8",
            DiagnosticProblemCode.GroupUnsupported => "GROUP_UNSUPPORTED",
            DiagnosticProblemCode.DepthExceeded => "DEPTH_EXCEEDED",
            DiagnosticProblemCode.LengthOverflow => "LENGTH_OVERFLOW",
            DiagnosticProblemCode.VarintOverflow => "VARINT_OVERFLOW",
            DiagnosticProblemCode.Truncated => "TRUNCATED",
            DiagnosticProblemCode.Malformed => "MALFORMED",
            DiagnosticProblemCode.LimitExceeded => "LIMIT_EXCEEDED",
            DiagnosticProblemCode.SchemaMessageMissing => "SCHEMA_MESSAGE_MISSING",
            DiagnosticProblemCode.SchemaInvalid => "SCHEMA_INVALID",
            _ => "UNKNOWN_PROBLEM"
        };
    }

    private static string FormatCount(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static long AddCount(long current, long value)
    {
        if (value <= 0)
        {
            return current;
        }

        return current > long.MaxValue - value ? long.MaxValue : current + value;
    }

    private IEnumerable<ReferenceMessage> ReferencesFor(string token)
    {
        return _references.Where(reference => TokenEquals(reference.Token, token));
    }

    private IEnumerable<PriorMapping> PinsFor(string token)
    {
        return _pins.Where(pin => TokenEquals(pin.ObfuscatedToken, token));
    }

    private IEnumerable<MessageDescriptor> ReadableBySimpleName(string name)
    {
        return _readableMessages.Where(message => string.Equals(SimpleName(message.FullName), name, StringComparison.Ordinal));
    }

    private IEnumerable<MessageDescriptor> ReadableByPinName(string name)
    {
        MessageDescriptor? exact = _readableMessages.FirstOrDefault(message => string.Equals(message.FullName, name, StringComparison.Ordinal));
        return exact is null ? ReadableBySimpleName(SimpleName(name)) : [exact];
    }

    private HashSet<string> ConflictingReferenceNames(string token)
    {
        return ReferencesFor(token)
            .Select(reference => ReadableBySimpleName(SimpleName(reference.Name)).ToArray())
            .Where(matches => matches.Length == 1)
            .Select(matches => matches[0].FullName)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<DiagnosticAnalysis> FindAnalyses(IReadOnlyList<DiagnosticAnalysis> analyses, string token)
    {
        return analyses.Where(analysis => analysis.CarriedType is not null && TokenEquals(analysis.CarriedType, token));
    }

    private static DiagnosticMessage? FindSchemaMessage(IReadOnlyDictionary<string, DiagnosticMessage> messages, string token)
    {
        if (messages.TryGetValue(token, out DiagnosticMessage? exact))
        {
            return exact;
        }

        string simple = SimpleName(token);
        return messages.Values.FirstOrDefault(message => string.Equals(SimpleName(message.Name), simple, StringComparison.Ordinal));
    }

    private static CandidateObservationSummary MakeObservationSummary(
        IReadOnlyList<DiagnosticAnalysis> analyses,
        DiagnosticSummary? summary)
    {
        if (summary is not null)
        {
            return new CandidateObservationSummary
            {
                Frames = summary.Frames,
                Coverage = summary.Coverage,
                Dropped = summary.Dropped
            };
        }

        return new CandidateObservationSummary { Frames = analyses.Count, Coverage = "complete", Dropped = 0 };
    }

    private static ResolverShapeResult Compare(
        DiagnosticMessage schema,
        MessageDescriptor readable,
        IReadOnlyDictionary<string, DiagnosticMessage> schemaByName)
    {
        List<ResolverContradiction> contradictions = [];
        List<(int Expected, int Actual)> matched = [];
        return CompareMessage(schema, readable, schemaByName, new HashSet<string>(StringComparer.Ordinal), 0, contradictions, matched);
    }

    private static ResolverShapeResult CompareMessage(
        DiagnosticMessage schema,
        MessageDescriptor readable,
        IReadOnlyDictionary<string, DiagnosticMessage> schemaByName,
        HashSet<string> visited,
        int depth,
        List<ResolverContradiction> contradictions,
        List<(int Expected, int Actual)> matched)
    {
        string pairKey = schema.Name + "\0" + readable.FullName;
        if (!visited.Add(pairKey))
        {
            return new ResolverShapeResult(contradictions.Count == 0, Score(schema.Fields.Count, matched.Count), contradictions.ToArray());
        }

        if (depth > MaxDepth)
        {
            AddContradiction(contradictions, ResolverContradictionCode.NestedShapeMismatch, "nested graph exceeds comparison depth");
            return new ResolverShapeResult(false, Score(schema.Fields.Count, matched.Count), contradictions.ToArray());
        }

        DiagnosticField[] schemaFields = schema.Fields.OrderBy(field => field.Number).ToArray();
        FieldDescriptor[] readableFields = readable.Fields.InFieldNumberOrder().ToArray();
        if (schemaFields.Length != readableFields.Length)
        {
            AddContradiction(contradictions, ResolverContradictionCode.FieldNumberMismatch, "field number sets have different sizes");
        }

        int count = Math.Min(schemaFields.Length, readableFields.Length);
        for (int index = 0; index < count; index++)
        {
            DiagnosticField schemaField = schemaFields[index];
            FieldDescriptor readableField = readableFields[index];
            if (schemaField.Number != readableField.FieldNumber)
            {
                AddContradiction(contradictions, ResolverContradictionCode.FieldNumberMismatch, "field numbers differ at sorted position " + (index + 1));
                continue;
            }

            matched.Add((schemaField.Number, readableField.FieldNumber));
            DiagnosticFieldKind readableKind = ToDiagnosticKind(readableField.FieldType);
            if (schemaField.Kind != readableKind)
            {
                AddContradiction(contradictions, ResolverContradictionCode.FieldTypeMismatch,
                    "field " + schemaField.Number + " kind differs: schema " + KindName(schemaField.Kind) + " vs readable " + KindName(readableKind));
            }

            if (schemaField.Repeated != readableField.IsRepeated)
            {
                AddContradiction(contradictions, ResolverContradictionCode.FieldRepetitionMismatch,
                    "field " + schemaField.Number + " repetition differs");
            }

            if (schemaField.Kind == DiagnosticFieldKind.Message && readableField.FieldType == FieldType.Message)
            {
                DiagnosticMessage? nestedSchema = FindSchemaMessage(schemaByName, schemaField.MessageType ?? string.Empty);
                if (nestedSchema is not null)
                {
                    int priorCount = contradictions.Count;
                    _ = CompareMessage(nestedSchema, readableField.MessageType, schemaByName, visited, depth + 1, contradictions, matched);
                    if (contradictions.Count > priorCount)
                    {
                        AddContradiction(contradictions, ResolverContradictionCode.NestedShapeMismatch, "nested message graph differs");
                    }
                }
                else
                {
                    AddContradiction(contradictions, ResolverContradictionCode.NestedShapeMismatch, "nested schema message is missing");
                }
            }
        }

        bool exact = contradictions.Count == 0 && schemaFields.Length == readableFields.Length;
        double score = schemaFields.Length == 0 && readableFields.Length == 0
            ? 1
            : Math.Min(1, (double)matched.Count / Math.Max(schemaFields.Length, readableFields.Length));
        return new ResolverShapeResult(exact, score, contradictions.ToArray());
    }

    private static SyntheticFixtureProposal? BuildFixture(DiagnosticSchema schema, DiagnosticMessage message)
    {
        List<byte> payload = [];
        foreach (DiagnosticField field in message.Fields.OrderBy(field => field.Number))
        {
            bool packedEmpty = field.Repeated && field.Packed && IsPackable(field.Kind);
            int wireType = packedEmpty ? 2 : WireType(field.Kind);
            if (wireType < 0)
            {
                return null;
            }

            WriteVarint(payload, ((ulong)field.Number << 3) | (uint)wireType);
            if (packedEmpty || wireType == 2)
            {
                WriteVarint(payload, 0);
            }
            else if (wireType == 1)
            {
                payload.AddRange(new byte[8]);
            }
            else if (wireType == 5)
            {
                payload.AddRange(new byte[4]);
            }
            else
            {
                WriteVarint(payload, 0);
            }
        }

        return new SyntheticFixtureProposal
        {
            SchemaMessage = message.Name,
            ExpectedStatus = "VALID",
            ExpectedFieldNumbers = message.Fields.OrderBy(field => field.Number).Select(field => field.Number).ToArray(),
            Synthetic = true,
            PayloadHex = Convert.ToHexString(payload.ToArray()).ToLowerInvariant()
        };
    }

    private static bool ContainsPartialNested(MessageDescriptor message, HashSet<string> visited, int depth)
    {
        if (depth > MaxDepth || !visited.Add(message.FullName))
        {
            return false;
        }

        if (IsPartial(message))
        {
            return true;
        }

        return message.Fields.InFieldNumberOrder()
            .Where(field => field.FieldType == FieldType.Message)
            .Any(field => ContainsPartialNested(field.MessageType, visited, depth + 1));
    }

    private static IEnumerable<MessageDescriptor> WalkMessages(IEnumerable<MessageDescriptor> roots)
    {
        foreach (MessageDescriptor message in roots)
        {
            yield return message;
            foreach (MessageDescriptor child in WalkMessages(message.NestedTypes))
            {
                yield return child;
            }
        }
    }

    private static bool IsPartial(MessageDescriptor message)
    {
        return message.GetOptions()?.GetExtension(OptionsExtensions.Partial) is true;
    }

    private static bool IsMapEntry(MessageDescriptor message)
    {
        return message.ToProto().Options?.MapEntry is true;
    }

    private static bool IsBuildExact(DiagnosticSchema schema)
    {
        return !string.IsNullOrWhiteSpace(schema.ClientVersion) && !string.IsNullOrWhiteSpace(schema.SchemaSha256);
    }

    private static bool TokenEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal)
            || string.Equals(SimpleName(left), SimpleName(right), StringComparison.Ordinal);
    }

    private static string SimpleName(string value)
    {
        int separator = value.LastIndexOf('.');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static bool IsDirection(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeDirection(string direction)
    {
        return direction is "client" or "server" ? direction : "unknown";
    }

    private static string DescribeSchema(DiagnosticMessage message)
    {
        return "schema message fields: " + string.Join(",", message.Fields.OrderBy(field => field.Number)
            .Select(field => field.Number + ":" + KindName(field.Kind) + (field.Repeated ? ":repeated" : ":singular")));
    }

    private static string DescribeReadable(MessageDescriptor message)
    {
        return "readable message " + message.FullName + " fields: " + string.Join(",", message.Fields.InFieldNumberOrder()
            .Select(field => field.FieldNumber + ":" + KindName(ToDiagnosticKind(field.FieldType)) + (field.IsRepeated ? ":repeated" : ":singular")));
    }

    private static void AddContradiction(List<ResolverContradiction> contradictions, ResolverContradictionCode code, string detail)
    {
        contradictions.Add(new ResolverContradiction { Code = code, Detail = detail });
    }

    private static double Score(int fieldCount, int matchedCount)
    {
        return fieldCount == 0 ? (matchedCount == 0 ? 1 : 0) : Math.Min(1, (double)matchedCount / fieldCount);
    }

    private static DiagnosticFieldKind ToDiagnosticKind(FieldType type)
    {
        return type switch
        {
            FieldType.Double => DiagnosticFieldKind.DoublePrecision,
            FieldType.Float => DiagnosticFieldKind.SinglePrecision,
            FieldType.Int64 => DiagnosticFieldKind.SignedInteger64,
            FieldType.UInt64 => DiagnosticFieldKind.UnsignedInteger64,
            FieldType.Int32 => DiagnosticFieldKind.SignedInteger32,
            FieldType.Fixed64 => DiagnosticFieldKind.Fixed64,
            FieldType.Fixed32 => DiagnosticFieldKind.Fixed32,
            FieldType.Bool => DiagnosticFieldKind.Boolean,
            FieldType.String => DiagnosticFieldKind.Utf8String,
            FieldType.Group => DiagnosticFieldKind.Group,
            FieldType.Message => DiagnosticFieldKind.Message,
            FieldType.Bytes => DiagnosticFieldKind.Bytes,
            FieldType.UInt32 => DiagnosticFieldKind.UnsignedInteger32,
            FieldType.Enum => DiagnosticFieldKind.Enumeration,
            FieldType.SFixed32 => DiagnosticFieldKind.SignedFixed32,
            FieldType.SFixed64 => DiagnosticFieldKind.SignedFixed64,
            FieldType.SInt32 => DiagnosticFieldKind.SignedZigZag32,
            FieldType.SInt64 => DiagnosticFieldKind.SignedZigZag64,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static string KindName(DiagnosticFieldKind kind)
    {
        return kind switch
        {
            DiagnosticFieldKind.DoublePrecision => "DOUBLE",
            DiagnosticFieldKind.SinglePrecision => "FLOAT",
            DiagnosticFieldKind.SignedInteger64 => "INT64",
            DiagnosticFieldKind.UnsignedInteger64 => "UINT64",
            DiagnosticFieldKind.SignedInteger32 => "INT32",
            DiagnosticFieldKind.Fixed64 => "FIXED64",
            DiagnosticFieldKind.Fixed32 => "FIXED32",
            DiagnosticFieldKind.Boolean => "BOOL",
            DiagnosticFieldKind.Utf8String => "STRING",
            DiagnosticFieldKind.Group => "GROUP",
            DiagnosticFieldKind.Message => "MESSAGE",
            DiagnosticFieldKind.Bytes => "BYTES",
            DiagnosticFieldKind.UnsignedInteger32 => "UINT32",
            DiagnosticFieldKind.Enumeration => "ENUM",
            DiagnosticFieldKind.SignedFixed32 => "SFIXED32",
            DiagnosticFieldKind.SignedFixed64 => "SFIXED64",
            DiagnosticFieldKind.SignedZigZag32 => "SINT32",
            DiagnosticFieldKind.SignedZigZag64 => "SINT64",
            _ => "UNKNOWN"
        };
    }

    private static int WireType(DiagnosticFieldKind kind)
    {
        return kind switch
        {
            DiagnosticFieldKind.DoublePrecision or DiagnosticFieldKind.Fixed64 or DiagnosticFieldKind.SignedFixed64 => 1,
            DiagnosticFieldKind.Utf8String or DiagnosticFieldKind.Message or DiagnosticFieldKind.Bytes => 2,
            DiagnosticFieldKind.Fixed32 or DiagnosticFieldKind.SignedFixed32 or DiagnosticFieldKind.SinglePrecision => 5,
            DiagnosticFieldKind.Group => -1,
            _ => 0
        };
    }

    private static bool IsPackable(DiagnosticFieldKind kind)
    {
        return kind is not (DiagnosticFieldKind.Utf8String or DiagnosticFieldKind.Message or DiagnosticFieldKind.Bytes or DiagnosticFieldKind.Group);
    }

    private static void WriteVarint(List<byte> output, ulong value)
    {
        while (value > 0x7f)
        {
            output.Add((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }

        output.Add((byte)value);
    }

}
