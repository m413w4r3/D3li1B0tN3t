using D3B.Diagnostic.Contract;
using D3B.Protocol.Readable;
using Google.Protobuf.Reflection;

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
        DiagnosticSummary? observationSummary = null)
    {
        ArgumentNullException.ThrowIfNull(buildSchema);
        ArgumentNullException.ThrowIfNull(analyses);

        Dictionary<string, DiagnosticMessage> schemaMessages = buildSchema.Messages
            .OrderBy(message => message.Name, StringComparer.Ordinal)
            .ToDictionary(message => message.Name, StringComparer.Ordinal);
        DiagnosticMessage[] candidateSchemaMessages = schemaMessages.Values
            .Where(message => !message.MapEntry)
            .ToArray();
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

        if (selected.Count == 0 && candidateSchemaMessages.Length > 0)
        {
            DiagnosticMessage fallback = candidateSchemaMessages[0];
            selected.Add(new ResolverSchemaPair(fallback, null, null, null));
        }

        CandidateObservationSummary observations = MakeObservationSummary(analyses, observationSummary);
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
                    candidates.Add(CreateUnknown(buildSchema, schemaMessage, analyses, targetDirection));
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
                    observations,
                    schemaMessages));
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
            Observations = observations,
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
        IReadOnlyDictionary<string, DiagnosticMessage> schemaMessages)
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
        bool hasValidObservation = tokenAnalyses.Any(IsValidObservation);
        string? direction = tokenAnalyses.Select(analysis => analysis.Direction).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? targetDirection;
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

        string simpleName = SimpleName(readable.Name);
        if ((simpleName.EndsWith("Request", StringComparison.Ordinal) && tokenAnalyses.Any(analysis => IsDirection(analysis.Direction, "server")))
            || ((simpleName.EndsWith("Event", StringComparison.Ordinal) || simpleName.EndsWith("Response", StringComparison.Ordinal))
                && tokenAnalyses.Any(analysis => IsDirection(analysis.Direction, "client"))))
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

    private static MappingCandidate CreateUnknown(DiagnosticSchema buildSchema, DiagnosticMessage schema, IReadOnlyList<DiagnosticAnalysis> analyses, string? targetDirection)
    {
        string? direction = FindAnalyses(analyses, schema.Name).Select(analysis => analysis.Direction).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? targetDirection;
        return new MappingCandidate
        {
            Token = schema.Name,
            Direction = direction,
            Classification = CandidateClassification.Unknown,
            ClientVersion = buildSchema.ClientVersion,
            SchemaSha256 = buildSchema.SchemaSha256,
            BuildExact = IsBuildExact(buildSchema),
            Score = new HeuristicScore { Value = 0, Kind = "heuristic" }
        };
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
