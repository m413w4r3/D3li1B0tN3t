using System.Text;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Aggregates analyses on the session pump into a bounded report.</summary>
public sealed class DiagnosticReportAggregator
{
    private readonly DiagnosticSchema _schema;
    private readonly DiagnosticReportOptions _options;
    private readonly DiagnosticReportBuildContext _context;
    private readonly Dictionary<string, DiagnosticMessage> _messages;
    private readonly Dictionary<string, DiagnosticReportTypeAccumulator> _types = new(StringComparer.Ordinal);
    private readonly HashSet<string> _omittedTypes = new(StringComparer.Ordinal);
    private long _unattributedAnalyses;
    private long _typesOmitted;
    private long _fieldsOmitted;
    private long _nestedFieldsOmitted;
    private long _variantsOmitted;
    private long _problemsOmitted;
    private long _shapeValuesOmitted;

    /// <summary>Creates a report aggregator for a schema and session.</summary>
    public DiagnosticReportAggregator(DiagnosticSchema schema, DiagnosticReportOptions options, DiagnosticReportBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        options.Validate();
        _schema = schema;
        _options = options;
        _context = context;
        _messages = schema.Messages
            .Where(message => IsSafeToken(message.Name))
            .GroupBy(message => message.Name, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
    }

    /// <summary>Adds one redacted analysis to the report.</summary>
    public void Observe(DiagnosticAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        DiagnosticMessage? message = ResolveMessage(analysis.CarriedType);
        if (message is null || !IsSafeToken(message.Name))
        {
            _unattributedAnalyses = SaturatingAdd(_unattributedAnalyses, 1);
            return;
        }

        if (!_types.TryGetValue(message.Name, out DiagnosticReportTypeAccumulator? type))
        {
            if (_omittedTypes.Contains(message.Name))
            {
                return;
            }

            if (_types.Count >= _options.MaxTypes)
            {
                string lastToken = _types.Keys.Max(StringComparer.Ordinal)!;
                if (StringComparer.Ordinal.Compare(message.Name, lastToken) >= 0)
                {
                    if (_omittedTypes.Add(message.Name))
                    {
                        _typesOmitted = SaturatingAdd(_typesOmitted, 1);
                    }

                    return;
                }

                _types.Remove(lastToken);
                if (_omittedTypes.Add(lastToken))
                {
                    _typesOmitted = SaturatingAdd(_typesOmitted, 1);
                }
            }

            type = new DiagnosticReportTypeAccumulator(message.Name, _options.MaxProblemsPerType);
            _types.Add(message.Name, type);
        }

        DiagnosticStatus status = Enum.IsDefined(analysis.Status) ? analysis.Status : DiagnosticStatus.Malformed;
        string direction = analysis.Direction switch
        {
            "client" => "client",
            "server" => "server",
            "d3b" => "d3b",
            _ => "unknown",
        };
        type.Observe(analysis, direction, status);

        bool messageMatches = analysis.Message is not null && _messages.ContainsKey(analysis.Message);
        type.RecordMessage(messageMatches ? analysis.Message : null, analysis.Message is not null && !messageMatches);
        foreach (DiagnosticProblem problem in analysis.Problems)
        {
            if (Enum.IsDefined(problem.Code))
            {
                type.RecordProblem(problem.Code, IsSafeFieldPath(problem.FieldPath) ? problem.FieldPath : null);
            }
        }

        long previousVariantsOmitted = type.VariantsOmitted;
        ObserveTopLevelFields(type, message, analysis.Fields);
        string signature = CreateSignature(analysis.Fields);
        type.RecordVariant(signature, status, _options.MaxVariantsPerType);
        _variantsOmitted = SaturatingAdd(_variantsOmitted, type.VariantsOmitted - previousVariantsOmitted);
    }

    /// <summary>Builds the immutable redacted report with terminal session accounting.</summary>
    public DiagnosticReport Build(DiagnosticReportBuildOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        List<string> reasons = [];
        if (_typesOmitted > 0)
        {
            reasons.Add("type-limit");
        }

        if (_fieldsOmitted > 0)
        {
            reasons.Add("field-limit");
        }

        if (_nestedFieldsOmitted > 0)
        {
            reasons.Add("nested-limit");
        }

        if (_variantsOmitted > 0)
        {
            reasons.Add("variant-limit");
        }

        long shapeOmitted = _shapeValuesOmitted;
        long problemsOmitted = _types.Values.Aggregate(0L, (total, type) => SaturatingAdd(total, type.ProblemsOmitted));
        List<DiagnosticReportType> types = [];
        foreach (DiagnosticReportTypeAccumulator type in _types.Values.OrderBy(type => type.Token, StringComparer.Ordinal))
        {
            types.Add(BuildType(type, ref shapeOmitted, ref problemsOmitted));
        }
        _problemsOmitted = problemsOmitted;
        if (_problemsOmitted > 0)
        {
            reasons.Add("problem-limit");
        }
        if (shapeOmitted > 0)
        {
            reasons.Add("shape-limit");
        }

        if (_unattributedAnalyses > 0)
        {
            reasons.Add("unattributed-analysis");
        }

        if (types.Any(type => type.DetailTruncated))
        {
            reasons.Add("detail-truncated");
        }

        if (outcome.DroppedFrames != 0)
        {
            reasons.Add("dropped-frame");
        }

        if (outcome.State != DiagnosticReportSessionState.Completed || outcome.ErrorCode is not null || outcome.Coverage != "complete")
        {
            reasons.Add("session-incomplete");
        }

        reasons = reasons.Distinct(StringComparer.Ordinal).OrderBy(reason => reason, StringComparer.Ordinal).ToList();
        bool complete = outcome.Coverage == "complete"
            && outcome.DroppedFrames == 0
            && outcome.State == DiagnosticReportSessionState.Completed
            && outcome.ErrorCode is null
            && _unattributedAnalyses == 0
            && _typesOmitted == 0
            && _fieldsOmitted == 0
            && _nestedFieldsOmitted == 0
            && _variantsOmitted == 0
            && _problemsOmitted == 0
            && shapeOmitted == 0
            && reasons.Count == 0;

        return new DiagnosticReport
        {
            Kind = "diagnostic-report",
            ReportVersion = 1,
            ProtocolVersion = DiagnosticProtocol.ProtocolVersion,
            ClientVersion = SafeIdentity(_context.ClientVersion, DiagnosticReportJson.IsSafeIdentity),
            SchemaSha256 = GetSchemaHash(),
            Engine = new DiagnosticReportEngine
            {
                ProtocolVersion = _context.Engine.ProtocolVersion,
                Engine = SafeIdentity(_context.Engine.Engine, DiagnosticReportJson.IsSafeEngine),
                EngineVersion = SafeIdentity(_context.Engine.EngineVersion, DiagnosticReportJson.IsSafeEngineVersion),
                SnsCommit = SafeIdentity(_context.Engine.SnsCommit, DiagnosticReportJson.IsSafeSnsCommit),
            },
            Provenance = new DiagnosticReportProvenance
            {
                Source = "live",
                Producer = SafeIdentity(string.IsNullOrEmpty(_context.Producer) ? "D3B.Diagnostic" : _context.Producer, DiagnosticReportJson.IsSafeIdentity),
                ProducerVersion = SafeIdentity(_context.ProducerVersion, DiagnosticReportJson.IsSafeIdentity),
                StartedAtUtc = _context.StartedAtUtc,
                CompletedAtUtc = outcome.CompletedAtUtc,
            },
            Session = new DiagnosticReportSession
            {
                State = Enum.IsDefined(outcome.State) ? outcome.State : DiagnosticReportSessionState.Failed,
                ErrorCode = outcome.ErrorCode is not null && Enum.IsDefined(outcome.ErrorCode.Value) ? outcome.ErrorCode : null,
                EnqueuedFrames = Math.Max(0, outcome.EnqueuedFrames),
                AnalyzedFrames = Math.Max(0, outcome.AnalyzedFrames),
                DroppedFrames = Math.Max(0, outcome.DroppedFrames),
                UnattributedAnalyses = _unattributedAnalyses,
                IncludeReadable = _context.IncludeReadable,
                QueueCapacity = Math.Max(0, _context.QueueCapacity),
                QueueByteCapacity = Math.Max(0, _context.QueueByteCapacity),
            },
            Coverage = complete ? "complete" : "partial",
            Types = types,
            Truncation = new DiagnosticReportTruncation
            {
                TypesOmitted = _typesOmitted,
                FieldsOmitted = _fieldsOmitted,
                NestedFieldsOmitted = _nestedFieldsOmitted,
                VariantsOmitted = _variantsOmitted,
                ProblemsOmitted = _problemsOmitted,
                ShapeValuesOmitted = shapeOmitted,
                Reasons = reasons,
            },
        };
    }

    private DiagnosticMessage? ResolveMessage(string? carriedType)
    {
        if (carriedType is null)
        {
            return null;
        }

        if (_messages.TryGetValue(carriedType, out DiagnosticMessage? exact))
        {
            return exact;
        }

        int lastDot = carriedType.LastIndexOf('.');
        string simpleName = lastDot < 0 ? carriedType : carriedType[(lastDot + 1)..];
        DiagnosticMessage[] matches = _messages.Values
            .Where(message => string.Equals(GetSimpleName(message.Name), simpleName, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string GetSimpleName(string value)
    {
        int lastDot = value.LastIndexOf('.');
        return lastDot < 0 ? value : value[(lastDot + 1)..];
    }

    private void ObserveTopLevelFields(
        DiagnosticReportTypeAccumulator type,
        DiagnosticMessage schemaMessage,
        IReadOnlyList<DiagnosticFieldAnalysis> analyses)
    {
        Dictionary<int, DiagnosticField> schemaFields = schemaMessage.Fields
            .Where(field => field.Number > 0)
            .GroupBy(field => field.Number)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());

        foreach (DiagnosticFieldAnalysis analysis in analyses)
        {
            ObserveField(type, type.Fields, schemaFields, analysis, 0, false);
        }
    }

    private void ObserveField(
        DiagnosticReportTypeAccumulator type,
        Dictionary<int, DiagnosticReportFieldAccumulator> fields,
        Dictionary<int, DiagnosticField> schemaFields,
        DiagnosticFieldAnalysis analysis,
        int depth,
        bool nested)
    {
        if (analysis.Number <= 0 || (nested && depth > _options.MaxNestedDepth))
        {
            CountOmittedField(nested);
            CountOmittedNestedAnalyses(analysis.Nested);
            return;
        }

        schemaFields.TryGetValue(analysis.Number, out DiagnosticField? schemaField);
        if (!fields.TryGetValue(analysis.Number, out DiagnosticReportFieldAccumulator? field))
        {
            if (!nested && fields.Count >= _options.MaxFieldsPerType)
            {
                int lastNumber = fields.Keys.Max();
                if (analysis.Number >= lastNumber)
                {
                    CountOmittedField(false);
                    CountOmittedNestedAnalyses(analysis.Nested);
                    return;
                }

                DiagnosticReportFieldAccumulator removedField = fields[lastNumber];
                fields.Remove(lastNumber);
                long removedNodes = CountFieldNodes(removedField);
                _fieldsOmitted = SaturatingAdd(_fieldsOmitted, 1);
                _nestedFieldsOmitted = SaturatingAdd(_nestedFieldsOmitted, removedNodes - 1);
                type.FieldNodeCount -= checked((int)removedNodes);
            }

            if (type.FieldNodeCount >= _options.MaxFieldNodesPerType)
            {
                CountOmittedField(nested);
                CountOmittedNestedAnalyses(analysis.Nested);
                return;
            }

            field = new DiagnosticReportFieldAccumulator(analysis.Number, schemaField, _options.MaxShapeValues);
            fields.Add(analysis.Number, field);
            type.FieldNodeCount++;
        }

        if (schemaField is null)
        {
            if (analysis.Name is not null || analysis.DeclaredKind is not null)
            {
                field.RecordDeclarationMismatch();
                type.RecordFieldDeclarationMismatch(analysis.Number);
            }
        }
        else if ((analysis.Name is not null && analysis.Name != schemaField.Name)
            || (analysis.DeclaredKind is not null && analysis.DeclaredKind != schemaField.Kind))
        {
            field.RecordDeclarationMismatch();
            type.RecordFieldDeclarationMismatch(analysis.Number);
        }

        long oldShapeOmitted = field.ShapeValuesOmitted;
        field.Observe(analysis);
        _shapeValuesOmitted = SaturatingAdd(_shapeValuesOmitted, field.ShapeValuesOmitted - oldShapeOmitted);

        if (analysis.Nested is { Count: > 0 })
        {
            DiagnosticMessage? nestedMessage = schemaField?.MessageType is not null && _messages.TryGetValue(schemaField.MessageType, out DiagnosticMessage? found)
                ? found
                : null;
            if (depth >= _options.MaxNestedDepth)
            {
                CountOmittedNestedAnalyses(analysis.Nested);
            }
            else
            {
                ObserveNestedFields(type, field, nestedMessage, analysis.Nested, depth + 1);
            }
        }
    }

    private void ObserveNestedFields(
        DiagnosticReportTypeAccumulator type,
        DiagnosticReportFieldAccumulator parent,
        DiagnosticMessage? schemaMessage,
        IReadOnlyList<DiagnosticFieldAnalysis> analyses,
        int depth)
    {
        Dictionary<int, DiagnosticField> schemaFields = schemaMessage?.Fields
            .Where(field => field.Number > 0)
            .GroupBy(field => field.Number)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single()) ?? [];

        foreach (DiagnosticFieldAnalysis analysis in analyses)
        {
            if (depth > _options.MaxNestedDepth)
            {
                CountOmittedField(true);
                CountOmittedNestedAnalyses(analysis.Nested);
                continue;
            }

            schemaFields.TryGetValue(analysis.Number, out DiagnosticField? schemaField);
            if (analysis.Number <= 0 || !parent.NestedFields.TryGetValue(analysis.Number, out DiagnosticReportFieldAccumulator? child))
            {
                if (analysis.Number <= 0 || type.FieldNodeCount >= _options.MaxFieldNodesPerType)
                {
                    CountOmittedField(true);
                    CountOmittedNestedAnalyses(analysis.Nested);
                    continue;
                }

                child = new DiagnosticReportFieldAccumulator(analysis.Number, schemaField, _options.MaxShapeValues);
                parent.NestedFields.Add(analysis.Number, child);
                type.FieldNodeCount++;
            }

            if (schemaField is null)
            {
                if (analysis.Name is not null || analysis.DeclaredKind is not null)
                {
                    child.RecordDeclarationMismatch();
                    type.RecordFieldDeclarationMismatch(analysis.Number);
                }
            }
            else if ((analysis.Name is not null && analysis.Name != schemaField.Name)
                || (analysis.DeclaredKind is not null && analysis.DeclaredKind != schemaField.Kind))
            {
                child.RecordDeclarationMismatch();
                type.RecordFieldDeclarationMismatch(analysis.Number);
            }

            long oldShapeOmitted = child.ShapeValuesOmitted;
            child.Observe(analysis);
            _shapeValuesOmitted = SaturatingAdd(_shapeValuesOmitted, child.ShapeValuesOmitted - oldShapeOmitted);

            if (analysis.Nested is { Count: > 0 })
            {
                DiagnosticMessage? nestedMessage = schemaField?.MessageType is not null && _messages.TryGetValue(schemaField.MessageType, out DiagnosticMessage? found)
                    ? found
                    : null;
                if (depth >= _options.MaxNestedDepth)
                {
                    CountOmittedNestedAnalyses(analysis.Nested);
                }
                else
                {
                    ObserveNestedFields(type, child, nestedMessage, analysis.Nested, depth + 1);
                }
            }
        }
    }

    private void CountOmittedField(bool nested)
    {
        if (nested)
        {
            _nestedFieldsOmitted = SaturatingAdd(_nestedFieldsOmitted, 1);
        }
        else
        {
            _fieldsOmitted = SaturatingAdd(_fieldsOmitted, 1);
        }
    }

    private void CountOmittedNestedAnalyses(IReadOnlyList<DiagnosticFieldAnalysis>? analyses)
    {
        if (analyses is null)
        {
            return;
        }

        foreach (DiagnosticFieldAnalysis analysis in analyses)
        {
            _nestedFieldsOmitted = SaturatingAdd(_nestedFieldsOmitted, 1);
            CountOmittedNestedAnalyses(analysis.Nested);
        }
    }

    private static long CountFieldNodes(DiagnosticReportFieldAccumulator field)
    {
        long count = 1;
        foreach (DiagnosticReportFieldAccumulator nested in field.NestedFields.Values)
        {
            count = SaturatingAdd(count, CountFieldNodes(nested));
        }

        return count;
    }

    private DiagnosticReportType BuildType(DiagnosticReportTypeAccumulator accumulator, ref long shapeOmitted, ref long problemsOmitted)
    {
        List<DiagnosticReportField> fields = [];
        foreach (DiagnosticReportFieldAccumulator field in accumulator.Fields.Values.OrderBy(field => field.Number))
        {
            fields.Add(BuildField(field, ref shapeOmitted, ref problemsOmitted));
        }
        List<DiagnosticReportProblemCount> problems = accumulator.Problems
            .OrderBy(problem => problem.Key.Code)
            .ThenBy(problem => problem.Key.FieldPath, StringComparer.Ordinal)
            .Select(problem => new DiagnosticReportProblemCount
            {
                Code = problem.Key.Code,
                FieldPath = problem.Key.FieldPath,
                Occurrences = problem.Value,
            })
            .ToList();

        List<DiagnosticReportVariant> variants = accumulator.Variants
            .OrderBy(variant => variant.Key, StringComparer.Ordinal)
            .Select(variant => new DiagnosticReportVariant
            {
                Signature = variant.Key,
                Occurrences = variant.Value.Values.Aggregate(0L, SaturatingAdd),
                Statuses = variant.Value.Select(status => new DiagnosticReportStatusCount
                {
                    Status = status.Key,
                    Occurrences = status.Value,
                }).ToArray(),
            })
            .ToList();

        List<DiagnosticReportContradiction> contradictions = [];
        AddContradiction(contradictions, DiagnosticReportContradictionCode.TypeUrlMismatch, accumulator.TypeUrlMismatchOccurrences,
            "TYPE_URL_MISMATCH count=" + accumulator.TypeUrlMismatchOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddContradiction(contradictions, DiagnosticReportContradictionCode.MessageMismatch, accumulator.MessageMismatchOccurrences,
            "MESSAGE_MISMATCH count=" + accumulator.MessageMismatchOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (accumulator.Statuses.Count > 1)
        {
            AddContradiction(contradictions, DiagnosticReportContradictionCode.MixedStatus, accumulator.Occurrences,
                "MIXED_STATUS count=" + accumulator.Statuses.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (variants.Count > 1 || accumulator.HasOmittedVariant)
        {
            AddContradiction(contradictions, DiagnosticReportContradictionCode.ShapeVariation,
                accumulator.VariantsOmitted > 0 ? accumulator.VariantsOmitted : variants.Count,
                "SHAPE_VARIATION count=" + variants.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (accumulator.FieldDeclarationMismatchOccurrences > 0)
        {
            string fieldNumber = accumulator.FirstFieldDeclarationMismatchNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            AddContradiction(contradictions, DiagnosticReportContradictionCode.FieldDeclarationMismatch,
                accumulator.FieldDeclarationMismatchOccurrences,
                "FIELD_DECLARATION_MISMATCH field=" + fieldNumber + " count=" + accumulator.FieldDeclarationMismatchOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (accumulator.DetailTruncatedOccurrences > 0)
        {
            AddContradiction(contradictions, DiagnosticReportContradictionCode.DetailTruncated,
                accumulator.DetailTruncatedOccurrences,
                "DETAIL_TRUNCATED count=" + accumulator.DetailTruncatedOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new DiagnosticReportType
        {
            Token = accumulator.Token,
            Message = accumulator.Message,
            Occurrences = accumulator.Occurrences,
            Directions = accumulator.Directions
                .OrderBy(direction => DirectionOrder(direction.Key))
                .Select(direction => new DiagnosticReportDirectionCount { Direction = direction.Key, Occurrences = direction.Value })
                .ToArray(),
            Statuses = accumulator.Statuses
                .Select(status => new DiagnosticReportStatusCount { Status = status.Key, Occurrences = status.Value })
                .ToArray(),
            TypeUrlOkOccurrences = accumulator.TypeUrlOkOccurrences,
            TypeUrlMismatchOccurrences = accumulator.TypeUrlMismatchOccurrences,
            Fields = fields,
            Problems = problems,
            Variants = variants,
            Contradictions = contradictions,
            DetailTruncated = accumulator.DetailTruncatedOccurrences > 0,
        };
    }

    private DiagnosticReportField BuildField(DiagnosticReportFieldAccumulator accumulator, ref long shapeOmitted, ref long problemsOmitted)
    {
        IReadOnlyList<DiagnosticReportField>? nested = null;
        if (accumulator.NestedFields.Count > 0)
        {
            List<DiagnosticReportField> nestedFields = [];
            foreach (DiagnosticReportFieldAccumulator field in accumulator.NestedFields.Values.OrderBy(field => field.Number))
            {
                nestedFields.Add(BuildField(field, ref shapeOmitted, ref problemsOmitted));
            }

            nested = nestedFields;
        }
        long omitted = 0;
        DiagnosticReportField built = accumulator.Build(_options.MaxProblemsPerType, count => omitted = SaturatingAdd(omitted, count));
        problemsOmitted = SaturatingAdd(problemsOmitted, omitted);
        return built with { Nested = nested };
    }

    private static void AddContradiction(List<DiagnosticReportContradiction> contradictions, DiagnosticReportContradictionCode code, long count, string detail)
    {
        if (count > 0)
        {
            contradictions.Add(new DiagnosticReportContradiction { Code = code, Detail = detail, Occurrences = count });
        }
    }

    private static string CreateSignature(IReadOnlyList<DiagnosticFieldAnalysis> fields)
    {
        StringBuilder signature = new();
        foreach (DiagnosticFieldAnalysis field in fields.OrderBy(field => field.Number))
        {
            IEnumerable<DiagnosticWireType> wires = field.WireTypes.Where(Enum.IsDefined).Distinct().OrderBy(wire => wire);
            DiagnosticWireType[] wireTypes = wires.ToArray();
            if (wireTypes.Length == 0)
            {
                wireTypes = [DiagnosticWireType.Unknown];
            }

            foreach (DiagnosticWireType wire in wireTypes)
            {
                string item = field.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "=" + WireName(wire);
                if (signature.Length > 0)
                {
                    signature.Append(';');
                }

                if (signature.Length + item.Length > 256)
                {
                    return signature.ToString();
                }

                signature.Append(item);
            }
        }

        return signature.ToString();
    }

    private static string WireName(DiagnosticWireType wireType)
    {
        return wireType switch
        {
            DiagnosticWireType.Varint => "VARINT",
            DiagnosticWireType.I64 => "I64",
            DiagnosticWireType.Len => "LEN",
            DiagnosticWireType.Group => "GROUP",
            DiagnosticWireType.I32 => "I32",
            _ => "UNKNOWN",
        };
    }

    private string GetSchemaHash()
    {
        string candidate = IsSha256(_context.SchemaSha256) ? _context.SchemaSha256 : _schema.SchemaSha256;
        if (!IsSha256(candidate))
        {
            throw new ArgumentException("A valid schema SHA-256 is required.", nameof(_context));
        }

        return candidate.ToLowerInvariant();
    }

    private static bool IsSafeToken(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 1024)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeFieldPath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        return value.All(character => character is >= '0' and <= '9' or '.');
    }

    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        return value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    }

    private static string SafeIdentity(string? value, Func<string?, bool> isSafe)
    {
        return isSafe(value) ? value ?? string.Empty : string.Empty;
    }

    private static int DirectionOrder(string direction)
    {
        return direction switch
        {
            "client" => 0,
            "server" => 1,
            "d3b" => 2,
            _ => 3,
        };
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            return long.MaxValue;
        }

        return left + right;
    }
}
