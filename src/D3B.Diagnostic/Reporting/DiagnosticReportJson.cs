using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Deterministic, bounded JSON operations for diagnostic reports.</summary>
public static class DiagnosticReportJson
{
    private const string SizeLimitMessage = "report exceeds size limit";
    private const string InvalidReportMessage = "report is invalid";
    private static readonly string[] _shapeProperties = ["varintBytes", "fixedLength", "length", "fieldCount"];
    private static readonly Regex _safeEngineVersion = new(
        @"\A[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex _safeProducerVersion = new(
        @"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex _safeClientVersion = new(
        @"\A[0-9]+(?:\.[0-9]+)+\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex _ipv4LikeIdentity = new(
        @"[0-9]{1,3}(?:\.[0-9]{1,3}){3}",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly DiagnosticReportOptions _maximumValidationOptions = new()
    {
        MaxTypes = 20_000,
        MaxFieldsPerType = 4_096,
        MaxFieldNodesPerType = 65_536,
        MaxNestedDepth = 32,
        MaxVariantsPerType = 1_024,
        MaxShapeValues = 4_096,
        MaxProblemsPerType = 4_096,
        MaxBytes = 67_108_864,
    };

    /// <summary>Serializes a report to deterministic compact UTF-8 JSON.</summary>
    public static byte[] SerializeToUtf8Bytes(DiagnosticReport report, DiagnosticReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ValidateIdentity(report);

        DiagnosticReport current = report;
        while (true)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(current, DiagnosticReportJsonContext.Protocol.DiagnosticReport);
            if (bytes.Length <= options.MaxBytes)
            {
                return bytes;
            }

            if (TryRemoveVariants(ref current, options.MaxBytes))
            {
                continue;
            }

            if (TryRemoveNestedFields(ref current, options.MaxBytes))
            {
                continue;
            }

            if (TryRemoveShapeValues(ref current, options.MaxBytes))
            {
                continue;
            }

            if (TryRemoveFields(ref current, options.MaxBytes))
            {
                continue;
            }

            if (TryRemoveTypes(ref current, options.MaxBytes))
            {
                continue;
            }

            throw new DiagnosticReportException(SizeLimitMessage);
        }
    }

    /// <summary>Serializes a report to deterministic compact JSON text.</summary>
    public static string Serialize(DiagnosticReport report, DiagnosticReportOptions options)
    {
        return Encoding.UTF8.GetString(SerializeToUtf8Bytes(report, options));
    }

    private static void ValidateIdentity(DiagnosticReport report)
    {
        if (report.Engine is null
            || report.Provenance is null
            || !IsSafeClientVersion(report.ClientVersion)
            || !IsSafeEngine(report.Engine.Engine)
            || !IsSafeEngineVersion(report.Engine.EngineVersion)
            || !IsSafeSnsCommit(report.Engine.SnsCommit)
            || !IsSafeProducer(report.Provenance.Producer)
            || !IsSafeProducerVersion(report.Provenance.ProducerVersion))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    /// <summary>Loads and validates a bounded report file.</summary>
    public static LoadedDiagnosticReport LoadFile(string path, DiagnosticReportOptions? options = null)
    {
        DiagnosticReportOptions validatedOptions = options ?? new DiagnosticReportOptions();
        validatedOptions.Validate();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DiagnosticReportException("report file is missing");
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > validatedOptions.MaxBytes)
            {
                throw new DiagnosticReportException(SizeLimitMessage);
            }

            byte[] bytes = new byte[validatedOptions.MaxBytes + 1];
            int total = 0;
            while (total < bytes.Length)
            {
                int read = stream.Read(bytes, total, bytes.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total > validatedOptions.MaxBytes)
            {
                throw new DiagnosticReportException(SizeLimitMessage);
            }

            return Load(bytes.AsSpan(0, total).ToArray(), validatedOptions);
        }
        catch (DiagnosticReportException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw new DiagnosticReportException("report file is missing");
        }
        catch (DirectoryNotFoundException)
        {
            throw new DiagnosticReportException("report file is missing");
        }
        catch (IOException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
        catch (UnauthorizedAccessException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
        catch (ArgumentException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    /// <summary>Loads and validates a bounded report byte sequence.</summary>
    public static LoadedDiagnosticReport Load(byte[] bytes, DiagnosticReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        DiagnosticReportOptions validatedOptions = options ?? new DiagnosticReportOptions();
        validatedOptions.Validate();
        if (bytes.Length > validatedOptions.MaxBytes)
        {
            throw new DiagnosticReportException(SizeLimitMessage);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            EnsureNoDuplicateProperties(document.RootElement);
            DiagnosticReport report = JsonSerializer.Deserialize(bytes, DiagnosticReportJsonContext.Protocol.DiagnosticReport)
                ?? throw new DiagnosticReportException(InvalidReportMessage);
            ValidateReport(report, document.RootElement, validatedOptions);
            return new LoadedDiagnosticReport
            {
                Report = report,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            };
        }
        catch (DiagnosticReportException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
        catch (FormatException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
        catch (OverflowException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
        catch (ArgumentException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    internal static bool IsValidReportObject(DiagnosticReport report)
    {
        return IsValidReportObject(report, _maximumValidationOptions);
    }

    internal static bool IsValidReportObject(DiagnosticReport report, DiagnosticReportOptions options)
    {
        try
        {
            if (report is null || options is null || report.Coverage is not ("complete" or "partial"))
            {
                return false;
            }

            options.Validate();

            // Resolver input may be downgraded to partial when complete-session accounting disagrees.
            DiagnosticReport candidate = report with { Coverage = "partial" };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(candidate, DiagnosticReportJsonContext.Protocol.DiagnosticReport);
            if (bytes.Length > options.MaxBytes)
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(bytes);
            ValidateReport(candidate, document.RootElement, options);
            return true;
        }
        catch (Exception exception) when (exception is DiagnosticReportException
            or JsonException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            return false;
        }
    }

    private static void MarkOutputReduction(ref DiagnosticReport report, string reason, Func<DiagnosticReportTruncation, DiagnosticReportTruncation> update)
    {
        DiagnosticReportTruncation truncation = update(report.Truncation) with
        {
            OutputTruncated = true,
            Reasons = report.Truncation.Reasons
                .Append(reason)
                .Append("output-size")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
        };
        report = report with { Coverage = "partial", Truncation = truncation };
    }

    private static bool TryRemoveVariants(ref DiagnosticReport report, int maximumBytes)
    {
        List<(int TypeIndex, string Signature)> removalOrder = [];
        for (int typeIndex = report.Types.Count - 1; typeIndex >= 0; typeIndex--)
        {
            removalOrder.AddRange(report.Types[typeIndex].Variants
                .OrderByDescending(variant => variant.Signature, StringComparer.Ordinal)
                .Select(variant => (typeIndex, variant.Signature)));
        }

        if (removalOrder.Count == 0)
        {
            return false;
        }

        int minimumRemoved = 1;
        int maximumRemoved = removalOrder.Count;
        DiagnosticReport? smallestFittingReport = null;
        while (minimumRemoved <= maximumRemoved)
        {
            int removed = minimumRemoved + ((maximumRemoved - minimumRemoved) / 2);
            HashSet<(int TypeIndex, string Signature)> removedVariants = removalOrder.Take(removed).ToHashSet();
            List<DiagnosticReportType> types = report.Types.Select((type, typeIndex) => type with
            {
                Variants = type.Variants
                    .Where(variant => !removedVariants.Contains((typeIndex, variant.Signature)))
                    .ToArray(),
            }).ToList();
            DiagnosticReport candidate = WithSizeReduction(report, types, "variant-limit", removed);
            int candidateLength = JsonSerializer.SerializeToUtf8Bytes(candidate, DiagnosticReportJsonContext.Protocol.DiagnosticReport).Length;
            if (candidateLength <= maximumBytes)
            {
                smallestFittingReport = candidate;
                maximumRemoved = removed - 1;
            }
            else
            {
                minimumRemoved = removed + 1;
            }
        }

        if (smallestFittingReport is null)
        {
            HashSet<(int TypeIndex, string Signature)> allVariants = removalOrder.ToHashSet();
            List<DiagnosticReportType> types = report.Types.Select((type, typeIndex) => type with
            {
                Variants = type.Variants
                    .Where(variant => !allVariants.Contains((typeIndex, variant.Signature)))
                    .ToArray(),
            }).ToList();
            report = WithSizeReduction(report, types, "variant-limit", removalOrder.Count);
            return true;
        }

        report = smallestFittingReport;
        return true;
    }

    private static bool TryRemoveNestedFields(ref DiagnosticReport report, int maximumBytes)
    {
        List<NestedCandidate> removalOrder = [];
        for (int typeIndex = 0; typeIndex < report.Types.Count; typeIndex++)
        {
            DiagnosticReportType type = report.Types[typeIndex];
            for (int fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++)
            {
                CollectNestedCandidates(type.Fields[fieldIndex], [type.Fields[fieldIndex].Number], typeIndex, type.Token, removalOrder);
            }
        }

        if (removalOrder.Count == 0)
        {
            return false;
        }

        removalOrder.Sort((left, right) => CompareNestedCandidates(right, left));
        DiagnosticReport source = report;
        DiagnosticReport? smallestFittingReport = FindSmallestFittingReduction(removalOrder.Count, maximumBytes, removed =>
        {
            List<DiagnosticReportType> types = source.Types.ToList();
            for (int index = 0; index < removed; index++)
            {
                NestedCandidate candidate = removalOrder[index];
                DiagnosticReportType type = types[candidate.TypeIndex];
                List<DiagnosticReportField> fields = type.Fields.ToList();
                RemoveNestedAt(fields, candidate.Path, 0);
                types[candidate.TypeIndex] = type with { Fields = fields };
            }

            return WithSizeReduction(source, types, "nested-limit", removed);
        });
        report = smallestFittingReport ?? BuildReduction(removalOrder.Count, removed =>
        {
            List<DiagnosticReportType> types = source.Types.ToList();
            for (int index = 0; index < removed; index++)
            {
                NestedCandidate candidate = removalOrder[index];
                DiagnosticReportType type = types[candidate.TypeIndex];
                List<DiagnosticReportField> fields = type.Fields.ToList();
                RemoveNestedAt(fields, candidate.Path, 0);
                types[candidate.TypeIndex] = type with { Fields = fields };
            }

            return WithSizeReduction(source, types, "nested-limit", removed);
        });
        return true;
    }

    private static DiagnosticReport? FindSmallestFittingReduction(
        int maximumRemoved,
        int maximumBytes,
        Func<int, DiagnosticReport> buildCandidate)
    {
        int minimum = 1;
        int maximum = maximumRemoved;
        DiagnosticReport? smallestFittingReport = null;
        while (minimum <= maximum)
        {
            int removed = minimum + ((maximum - minimum) / 2);
            DiagnosticReport candidate = buildCandidate(removed);
            int candidateLength = JsonSerializer.SerializeToUtf8Bytes(candidate, DiagnosticReportJsonContext.Protocol.DiagnosticReport).Length;
            if (candidateLength <= maximumBytes)
            {
                smallestFittingReport = candidate;
                maximum = removed - 1;
            }
            else
            {
                minimum = removed + 1;
            }
        }

        return smallestFittingReport;
    }

    private static DiagnosticReport BuildReduction(int count, Func<int, DiagnosticReport> buildCandidate)
    {
        return buildCandidate(count);
    }

    private static void CollectNestedCandidates(
        DiagnosticReportField field,
        int[] path,
        int typeIndex,
        string token,
        List<NestedCandidate> candidates)
    {
        if (field.Nested is null)
        {
            return;
        }

        foreach (DiagnosticReportField nested in field.Nested)
        {
            int[] nestedPath = [.. path, nested.Number];
            candidates.Add(new NestedCandidate(typeIndex, token, nestedPath, nestedPath.Length));
            CollectNestedCandidates(nested, nestedPath, typeIndex, token, candidates);
        }
    }

    private static int CompareNestedCandidates(NestedCandidate left, NestedCandidate right)
    {
        int depthComparison = left.Depth.CompareTo(right.Depth);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        int tokenComparison = StringComparer.Ordinal.Compare(left.Token, right.Token);
        if (tokenComparison != 0)
        {
            return tokenComparison;
        }

        int typeIndexComparison = left.TypeIndex.CompareTo(right.TypeIndex);
        if (typeIndexComparison != 0)
        {
            return typeIndexComparison;
        }

        int pathLength = Math.Min(left.Path.Length, right.Path.Length);
        for (int index = 0; index < pathLength; index++)
        {
            int pathComparison = left.Path[index].CompareTo(right.Path[index]);
            if (pathComparison != 0)
            {
                return pathComparison;
            }
        }

        return left.Path.Length.CompareTo(right.Path.Length);
    }

    private static void RemoveNestedAt(List<DiagnosticReportField> fields, int[] path, int pathIndex)
    {
        int fieldIndex = fields.FindIndex(field => field.Number == path[pathIndex]);
        if (fieldIndex < 0)
        {
            return;
        }

        DiagnosticReportField field = fields[fieldIndex];
        if (pathIndex == path.Length - 1)
        {
            fields.RemoveAt(fieldIndex);
            return;
        }

        List<DiagnosticReportField> nested = field.Nested?.ToList() ?? [];
        RemoveNestedAt(nested, path, pathIndex + 1);
        fields[fieldIndex] = field with { Nested = nested.Count == 0 ? null : nested };
    }

    private static bool TryRemoveShapeValues(ref DiagnosticReport report, int maximumBytes)
    {
        List<ShapeCandidate> removalOrder = [];
        for (int typeIndex = report.Types.Count - 1; typeIndex >= 0; typeIndex--)
        {
            DiagnosticReportType type = report.Types[typeIndex];
            foreach (DiagnosticReportField field in type.Fields.OrderByDescending(field => field.Number))
            {
                CollectShapeCandidates(field, [field.Number], typeIndex, removalOrder);
            }
        }

        if (removalOrder.Count == 0)
        {
            return false;
        }

        DiagnosticReport source = report;
        DiagnosticReport? smallestFittingReport = FindSmallestFittingReduction(removalOrder.Count, maximumBytes, removed =>
        {
            List<DiagnosticReportType> types = source.Types.ToList();
            for (int index = 0; index < removed; index++)
            {
                ShapeCandidate candidate = removalOrder[index];
                DiagnosticReportType type = types[candidate.TypeIndex];
                List<DiagnosticReportField> fields = type.Fields.ToList();
                RemoveShapeAt(fields, candidate.Path, 0, candidate.Property, candidate.Value);
                types[candidate.TypeIndex] = type with { Fields = fields };
            }

            return WithSizeReduction(source, types, "shape-limit", removed);
        });
        report = smallestFittingReport ?? BuildReduction(removalOrder.Count, removed =>
        {
            List<DiagnosticReportType> types = source.Types.ToList();
            for (int index = 0; index < removed; index++)
            {
                ShapeCandidate candidate = removalOrder[index];
                DiagnosticReportType type = types[candidate.TypeIndex];
                List<DiagnosticReportField> fields = type.Fields.ToList();
                RemoveShapeAt(fields, candidate.Path, 0, candidate.Property, candidate.Value);
                types[candidate.TypeIndex] = type with { Fields = fields };
            }

            return WithSizeReduction(source, types, "shape-limit", removed);
        });
        return true;
    }

    private static void CollectShapeCandidates(DiagnosticReportField field, int[] path, int typeIndex, List<ShapeCandidate> candidates)
    {
        foreach (string property in _shapeProperties)
        {
            IReadOnlyList<int>? values = GetShapeValues(field.Observed, property);
            if (values is not null)
            {
                foreach (int value in values.OrderByDescending(value => value))
                {
                    candidates.Add(new ShapeCandidate(typeIndex, path, property, value));
                }
            }
        }

        if (field.Nested is not null)
        {
            foreach (DiagnosticReportField nested in field.Nested.OrderByDescending(field => field.Number))
            {
                CollectShapeCandidates(nested, [.. path, nested.Number], typeIndex, candidates);
            }
        }
    }

    private static void RemoveShapeAt(List<DiagnosticReportField> fields, int[] path, int pathIndex, string property, int value)
    {
        int fieldIndex = fields.FindIndex(field => field.Number == path[pathIndex]);
        if (fieldIndex < 0)
        {
            return;
        }

        DiagnosticReportField field = fields[fieldIndex];
        if (pathIndex < path.Length - 1)
        {
            List<DiagnosticReportField> nested = field.Nested?.ToList() ?? [];
            RemoveShapeAt(nested, path, pathIndex + 1, property, value);
            fields[fieldIndex] = field with { Nested = nested.Count == 0 ? null : nested };
            return;
        }

        int[] retained = (GetShapeValues(field.Observed, property) ?? Array.Empty<int>()).Where(item => item != value).ToArray();
        DiagnosticObservedShape observed = property switch
        {
            "varintBytes" => field.Observed with { VarintBytes = retained.Length == 0 ? null : retained },
            "fixedLength" => field.Observed with { FixedLength = retained.Length == 0 ? null : retained },
            "length" => field.Observed with { Length = retained.Length == 0 ? null : retained },
            _ => field.Observed with { FieldCount = retained.Length == 0 ? null : retained },
        };
        fields[fieldIndex] = field with { Observed = observed };
    }

    private static IReadOnlyList<int>? GetShapeValues(DiagnosticObservedShape observed, string property)
    {
        return property switch
        {
            "varintBytes" => observed.VarintBytes,
            "fixedLength" => observed.FixedLength,
            "length" => observed.Length,
            _ => observed.FieldCount,
        };
    }

    private static bool TryRemoveFields(ref DiagnosticReport report, int maximumBytes)
    {
        List<(int TypeIndex, int Number)> removalOrder = [];
        for (int typeIndex = report.Types.Count - 1; typeIndex >= 0; typeIndex--)
        {
            removalOrder.AddRange(report.Types[typeIndex].Fields
                .OrderByDescending(field => field.Number)
                .Select(field => (typeIndex, field.Number)));
        }

        if (removalOrder.Count == 0)
        {
            return false;
        }

        DiagnosticReport source = report;
        DiagnosticReport? smallestFittingReport = FindSmallestFittingReduction(removalOrder.Count, maximumBytes, removed =>
        {
            HashSet<(int TypeIndex, int Number)> removedFields = removalOrder.Take(removed).ToHashSet();
            List<DiagnosticReportType> types = source.Types.Select((type, typeIndex) => type with
            {
                Fields = type.Fields.Where(field => !removedFields.Contains((typeIndex, field.Number))).ToArray(),
            }).ToList();
            return WithSizeReduction(source, types, "field-limit", removed);
        });
        report = smallestFittingReport ?? BuildReduction(removalOrder.Count, removed =>
        {
            HashSet<(int TypeIndex, int Number)> removedFields = removalOrder.Take(removed).ToHashSet();
            List<DiagnosticReportType> types = source.Types.Select((type, typeIndex) => type with
            {
                Fields = type.Fields.Where(field => !removedFields.Contains((typeIndex, field.Number))).ToArray(),
            }).ToList();
            return WithSizeReduction(source, types, "field-limit", removed);
        });
        return true;
    }

    private static bool TryRemoveTypes(ref DiagnosticReport report, int maximumBytes)
    {
        if (report.Types.Count == 0)
        {
            return false;
        }

        (DiagnosticReportType Type, int Index)[] removalOrder = report.Types
            .Select((type, index) => (type, index))
            .OrderBy(item => item.type.Occurrences)
            .ThenByDescending(item => item.type.Token, StringComparer.Ordinal)
            .Select(item => (item.type, item.index))
            .ToArray();

        int minimumRemoved = 1;
        int maximumRemoved = removalOrder.Length;
        DiagnosticReport? smallestFittingReport = null;
        while (minimumRemoved <= maximumRemoved)
        {
            int removed = minimumRemoved + ((maximumRemoved - minimumRemoved) / 2);
            HashSet<int> removedIndexes = removalOrder.Take(removed).Select(item => item.Index).ToHashSet();
            List<DiagnosticReportType> retainedTypes = report.Types
                .Where((_, index) => !removedIndexes.Contains(index))
                .ToList();
            DiagnosticReport candidate = WithSizeReduction(report, retainedTypes, "type-limit", removed);
            int candidateLength = JsonSerializer.SerializeToUtf8Bytes(candidate, DiagnosticReportJsonContext.Protocol.DiagnosticReport).Length;
            if (candidateLength <= maximumBytes)
            {
                smallestFittingReport = candidate;
                maximumRemoved = removed - 1;
            }
            else
            {
                minimumRemoved = removed + 1;
            }
        }

        if (smallestFittingReport is null)
        {
            HashSet<int> allIndexes = removalOrder.Select(item => item.Index).ToHashSet();
            List<DiagnosticReportType> noTypes = report.Types.Where((_, index) => !allIndexes.Contains(index)).ToList();
            report = WithSizeReduction(report, noTypes, "type-limit", removalOrder.Length);
            return true;
        }

        report = smallestFittingReport;
        return true;
    }

    private static DiagnosticReport WithSizeReduction(DiagnosticReport report, IReadOnlyList<DiagnosticReportType> types, string reason, int removed)
    {
        DiagnosticReportTruncation truncation = report.Truncation with
        {
            OutputTruncated = true,
            TypesOmitted = reason == "type-limit" ? SaturatingAdd(report.Truncation.TypesOmitted, removed) : report.Truncation.TypesOmitted,
            FieldsOmitted = reason == "field-limit" ? SaturatingAdd(report.Truncation.FieldsOmitted, removed) : report.Truncation.FieldsOmitted,
            NestedFieldsOmitted = reason == "nested-limit" ? SaturatingAdd(report.Truncation.NestedFieldsOmitted, removed) : report.Truncation.NestedFieldsOmitted,
            VariantsOmitted = reason == "variant-limit" ? SaturatingAdd(report.Truncation.VariantsOmitted, removed) : report.Truncation.VariantsOmitted,
            ShapeValuesOmitted = reason == "shape-limit" ? SaturatingAdd(report.Truncation.ShapeValuesOmitted, removed) : report.Truncation.ShapeValuesOmitted,
            Reasons = report.Truncation.Reasons
                .Append(reason)
                .Append("output-size")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
        };
        return report with { Types = types, Coverage = "partial", Truncation = truncation };
    }

    private static void EnsureNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new DiagnosticReportException(InvalidReportMessage);
                }

                EnsureNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(item);
            }
        }
    }

    private static void ValidateReport(DiagnosticReport report, JsonElement root, DiagnosticReportOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object
            || report.Kind != "diagnostic-report"
            || report.ReportVersion != 1
            || report.ProtocolVersion != DiagnosticProtocol.ProtocolVersion
            || !IsSha256(report.SchemaSha256)
            || !IsSafeClientVersion(report.ClientVersion)
            || report.Coverage is not ("complete" or "partial")
            || report.Engine is null
            || report.Provenance is null
            || report.Session is null
            || report.Truncation is null
            || report.Types is null)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        ValidateRequiredProperties(root, "kind", "reportVersion", "protocolVersion", "clientVersion", "schemaSha256", "engine", "provenance", "session", "coverage", "types", "truncation");
        ValidateEngine(report.Engine, root.GetProperty("engine"));
        ValidateProvenance(report.Provenance, root.GetProperty("provenance"));
        ValidateSession(report.Session, root.GetProperty("session"));
        ValidateTruncation(report.Truncation, root.GetProperty("truncation"));
        if (report.Types.Count > options.MaxTypes)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        HashSet<string> tokens = new(StringComparer.Ordinal);
        JsonElement[] typeElements = root.GetProperty("types").EnumerateArray().ToArray();
        for (int index = 0; index < report.Types.Count; index++)
        {
            DiagnosticReportType type = report.Types[index];
            ValidateType(type, typeElements[index], options, report.Truncation);
            if (!tokens.Add(type.Token))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }
        }

        if (HasInconsistentCompleteCoverage(report))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static bool HasInconsistentCompleteCoverage(DiagnosticReport report)
    {
        if (report.Coverage != "complete")
        {
            return false;
        }

        DiagnosticReportSession session = report.Session;
        DiagnosticReportTruncation truncation = report.Truncation;
        long accountedAnalyses = session.UnattributedAnalyses;
        foreach (DiagnosticReportType type in report.Types)
        {
            accountedAnalyses = CheckedCountAdd(accountedAnalyses, type.Occurrences);
        }

        return accountedAnalyses != session.AnalyzedFrames
            || session.State != DiagnosticReportSessionState.Completed
            || session.ErrorCode is not null
            || session.EnqueuedFrames != session.AnalyzedFrames
            || session.DroppedFrames != 0
            || session.UnattributedAnalyses != 0
            || truncation.OutputTruncated
            || truncation.TypesOmitted != 0
            || truncation.FieldsOmitted != 0
            || truncation.NestedFieldsOmitted != 0
            || truncation.VariantsOmitted != 0
            || truncation.ProblemsOmitted != 0
            || truncation.ShapeValuesOmitted != 0
            || truncation.Reasons.Count > 0
            || report.Types.Any(type => type.DetailTruncated
                || type.Contradictions.Any(contradiction => contradiction.Code == DiagnosticReportContradictionCode.DetailTruncated));
    }

    private static void ValidateEngine(DiagnosticReportEngine engine, JsonElement element)
    {
        ValidateRequiredProperties(element, "protocolVersion", "engine", "engineVersion", "snsCommit");
        if (engine.ProtocolVersion < 0
            || !IsSafeEngine(engine.Engine)
            || !IsSafeEngineVersion(engine.EngineVersion)
            || !IsSafeSnsCommit(engine.SnsCommit))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static void ValidateProvenance(DiagnosticReportProvenance provenance, JsonElement element)
    {
        ValidateRequiredProperties(element, "source", "producer", "producerVersion", "startedAtUtc", "completedAtUtc");
        if (provenance.Source is not ("live" or "synthetic")
            || !IsSafeProducer(provenance.Producer)
            || !IsSafeProducerVersion(provenance.ProducerVersion))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static void ValidateSession(DiagnosticReportSession session, JsonElement element)
    {
        ValidateRequiredProperties(element, "state", "enqueuedFrames", "analyzedFrames", "droppedFrames", "unattributedAnalyses", "includeReadable", "queueCapacity", "queueByteCapacity");
        if (!Enum.IsDefined(session.State)
            || (session.ErrorCode is not null && !Enum.IsDefined(session.ErrorCode.Value))
            || session.EnqueuedFrames < 0
            || session.AnalyzedFrames < 0
            || session.DroppedFrames < 0
            || session.UnattributedAnalyses < 0
            || session.QueueCapacity < 0
            || session.QueueByteCapacity < 0)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static void ValidateTruncation(DiagnosticReportTruncation truncation, JsonElement element)
    {
        ValidateRequiredProperties(element, "outputTruncated", "typesOmitted", "fieldsOmitted", "nestedFieldsOmitted", "variantsOmitted", "problemsOmitted", "shapeValuesOmitted", "reasons");
        if (truncation.TypesOmitted < 0
            || truncation.FieldsOmitted < 0
            || truncation.NestedFieldsOmitted < 0
            || truncation.VariantsOmitted < 0
            || truncation.ProblemsOmitted < 0
            || truncation.ShapeValuesOmitted < 0
            || truncation.Reasons is null
            || truncation.Reasons.Count > 32
            || truncation.Reasons.Any(reason => !_allowedReasons.Contains(reason)))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static readonly HashSet<string> _allowedReasons = new(StringComparer.Ordinal)
    {
        "type-limit", "field-limit", "nested-limit", "variant-limit", "problem-limit", "shape-limit",
        "unattributed-analysis", "detail-truncated", "output-size", "session-incomplete", "dropped-frame",
        "summary-mismatch", "aggregation-failed",
    };

    private static void ValidateType(DiagnosticReportType type, JsonElement element, DiagnosticReportOptions options, DiagnosticReportTruncation truncation)
    {
        ValidateRequiredProperties(element, "token", "occurrences", "directions", "statuses", "typeUrlOkOccurrences", "typeUrlMismatchOccurrences", "fields", "problems", "variants", "contradictions", "detailTruncated");
        if (type is null
            || type.Directions is null
            || type.Statuses is null
            || type.Fields is null
            || type.Problems is null
            || type.Variants is null
            || type.Contradictions is null
            || !IsSafeToken(type.Token)
            || (type.Message is not null && !IsSafeToken(type.Message))
            || type.Occurrences < 0
            || type.TypeUrlOkOccurrences < 0
            || type.TypeUrlMismatchOccurrences < 0
            || CheckedCountAdd(type.TypeUrlOkOccurrences, type.TypeUrlMismatchOccurrences) != type.Occurrences
            || type.Directions.Count > 4
            || type.Statuses.Count > Enum.GetValues<DiagnosticStatus>().Length
            || type.Fields.Count > options.MaxFieldsPerType
            || type.Variants.Count > options.MaxVariantsPerType
            || type.Problems.Count > options.MaxProblemsPerType
            || type.Contradictions.Count > Enum.GetValues<DiagnosticReportContradictionCode>().Length)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        HashSet<string> directions = new(StringComparer.Ordinal);
        long directionTotal = 0;
        JsonElement[] directionElements = element.GetProperty("directions").EnumerateArray().ToArray();
        for (int index = 0; index < type.Directions.Count; index++)
        {
            DiagnosticReportDirectionCount direction = type.Directions[index];
            ValidateRequiredProperties(directionElements[index], "direction", "occurrences");
            if (direction.Direction is not ("client" or "server" or "d3b" or "unknown")
                || direction.Occurrences < 0
                || !directions.Add(direction.Direction))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            directionTotal = CheckedCountAdd(directionTotal, direction.Occurrences);
        }

        if (directionTotal != type.Occurrences)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        HashSet<DiagnosticStatus> statuses = [];
        long statusTotal = 0;
        JsonElement[] statusElements = element.GetProperty("statuses").EnumerateArray().ToArray();
        for (int index = 0; index < type.Statuses.Count; index++)
        {
            DiagnosticReportStatusCount status = type.Statuses[index];
            ValidateRequiredProperties(statusElements[index], "status", "occurrences");
            if (!Enum.IsDefined(status.Status) || status.Occurrences < 0 || !statuses.Add(status.Status))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            statusTotal = CheckedCountAdd(statusTotal, status.Occurrences);
        }

        if (statusTotal != type.Occurrences)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        HashSet<(DiagnosticProblemCode, string?)> problemKeys = [];
        JsonElement[] problemElements = element.GetProperty("problems").EnumerateArray().ToArray();
        for (int index = 0; index < type.Problems.Count; index++)
        {
            DiagnosticReportProblemCount problem = type.Problems[index];
            ValidateRequiredProperties(problemElements[index], "code", "occurrences");
            if (!Enum.IsDefined(problem.Code)
                || problem.Occurrences < 0
                || (problem.FieldPath is not null && !IsSafeFieldPath(problem.FieldPath))
                || !problemKeys.Add((problem.Code, problem.FieldPath)))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }
        }

        HashSet<string> variants = new(StringComparer.Ordinal);
        long variantTotal = 0;
        JsonElement[] variantElements = element.GetProperty("variants").EnumerateArray().ToArray();
        for (int index = 0; index < type.Variants.Count; index++)
        {
            DiagnosticReportVariant variant = type.Variants[index];
            JsonElement variantElement = variantElements[index];
            ValidateRequiredProperties(variantElement, "signature", "occurrences", "statuses");
            if (!IsSafeSignature(variant.Signature)
                || variant.Statuses is null
                || variant.Occurrences < 0
                || !variants.Add(variant.Signature)
                || variant.Statuses.Count > Enum.GetValues<DiagnosticStatus>().Length)
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            long variantStatusTotal = 0;
            HashSet<DiagnosticStatus> variantStatuses = [];
            JsonElement[] variantStatusElements = variantElement.GetProperty("statuses").EnumerateArray().ToArray();
            for (int statusIndex = 0; statusIndex < variant.Statuses.Count; statusIndex++)
            {
                DiagnosticReportStatusCount status = variant.Statuses[statusIndex];
                ValidateRequiredProperties(variantStatusElements[statusIndex], "status", "occurrences");
                if (!Enum.IsDefined(status.Status) || status.Occurrences < 0 || !variantStatuses.Add(status.Status))
                {
                    throw new DiagnosticReportException(InvalidReportMessage);
                }

                variantStatusTotal = CheckedCountAdd(variantStatusTotal, status.Occurrences);
            }

            if (variantStatusTotal != variant.Occurrences)
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            variantTotal = CheckedCountAdd(variantTotal, variant.Occurrences);
        }

        bool aggregationFailed = truncation.Reasons.Contains("aggregation-failed", StringComparer.Ordinal);
        if (variantTotal > type.Occurrences
            || (!aggregationFailed && truncation.VariantsOmitted == 0 && variantTotal != type.Occurrences))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        HashSet<DiagnosticReportContradictionCode> contradictionCodes = [];
        JsonElement[] contradictionElements = element.GetProperty("contradictions").EnumerateArray().ToArray();
        for (int index = 0; index < type.Contradictions.Count; index++)
        {
            DiagnosticReportContradiction contradiction = type.Contradictions[index];
            ValidateRequiredProperties(contradictionElements[index], "code", "detail", "occurrences");
            if (!Enum.IsDefined(contradiction.Code)
                || contradiction.Occurrences < 0
                || !IsSafeContradictionDetail(contradiction)
                || !contradictionCodes.Add(contradiction.Code))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }
        }

        HashSet<int> fieldNumbers = [];
        int fieldNodes = 0;
        JsonElement[] fieldElements = element.GetProperty("fields").EnumerateArray().ToArray();
        for (int index = 0; index < type.Fields.Count; index++)
        {
            ValidateRequiredProperties(fieldElements[index], "number", "repeated", "packed", "occurrences", "wireTypes", "observed");
            DiagnosticReportField field = type.Fields[index];
            if (!fieldNumbers.Add(field.Number))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            ValidateField(field, fieldElements[index], options, 0, ref fieldNodes);
        }
    }

    private static void ValidateField(DiagnosticReportField field, JsonElement element, DiagnosticReportOptions options, int depth, ref int nodes)
    {
        ValidateRequiredProperties(element, "number", "repeated", "packed", "occurrences", "wireTypes", "observed");
        nodes++;
        if (nodes > options.MaxFieldNodesPerType
            || field.Number <= 0
            || field.Occurrences < 0
            || (field.Name is not null && (!field.Name.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_') || field.Name.Length > 128))
            || (field.DeclaredKind is not null && !Enum.IsDefined(field.DeclaredKind.Value))
            || field.WireTypes is null
            || field.WireTypes.Count > Enum.GetValues<DiagnosticWireType>().Length
            || field.WireTypes.Any(wire => !Enum.IsDefined(wire))
            || field.WireTypes.Distinct().Count() != field.WireTypes.Count)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        ValidateShape(field.Observed, options);
        if (field.Problems is { Count: > 0 })
        {
            if (field.Problems.Count > options.MaxProblemsPerType
                || field.Problems.Any(problem => problem is null || !Enum.IsDefined(problem.Code) || problem.Occurrences < 0)
                || field.Problems.Select(problem => problem.Code).Distinct().Count() != field.Problems.Count)
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }
        }

        if (field.Nested is not null)
        {
            if (depth >= options.MaxNestedDepth)
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }

            HashSet<int> nestedNumbers = [];
            JsonElement[] nestedElements = element.GetProperty("nested").EnumerateArray().ToArray();
            for (int index = 0; index < field.Nested.Count; index++)
            {
                ValidateRequiredProperties(nestedElements[index], "number", "repeated", "packed", "occurrences", "wireTypes", "observed");
                DiagnosticReportField nested = field.Nested[index];
                if (!nestedNumbers.Add(nested.Number))
                {
                    throw new DiagnosticReportException(InvalidReportMessage);
                }

                ValidateField(nested, nestedElements[index], options, depth + 1, ref nodes);
            }
        }
    }

    private static void ValidateShape(DiagnosticObservedShape shape, DiagnosticReportOptions options)
    {
        if (shape is null
            || !IsBoundedShapeList(shape.VarintBytes, options.MaxShapeValues)
            || !IsBoundedShapeList(shape.FixedLength, options.MaxShapeValues)
            || !IsBoundedShapeList(shape.Length, options.MaxShapeValues)
            || !IsBoundedShapeList(shape.FieldCount, options.MaxShapeValues))
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static bool IsBoundedShapeList(IReadOnlyList<int>? values, int maximum)
    {
        return values is null || (values.Count <= maximum && values.All(value => value >= 0) && values.Distinct().Count() == values.Count);
    }

    private static void ValidateRequiredProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }

        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out _))
            {
                throw new DiagnosticReportException(InvalidReportMessage);
            }
        }
    }

    internal static bool IsSafeClientVersion(string? value)
    {
        return value is { Length: <= 128 } && _safeClientVersion.IsMatch(value);
    }

    internal static bool IsSafeProducer(string? value)
    {
        return value is "" or "D3B.Diagnostic";
    }

    internal static bool IsSafeProducerVersion(string? value)
    {
        return value == string.Empty || (value is { Length: <= 128 } && IsSafeVersion(value, _safeProducerVersion));
    }

    internal static bool IsSafeEngine(string? value)
    {
        return value is "" or "d3b-diagnostic";
    }

    internal static bool IsSafeEngineVersion(string? value)
    {
        return IsSafeVersion(value, _safeEngineVersion);
    }

    internal static bool IsSafeSnsCommit(string? value)
    {
        return value == string.Empty
            || (value is { Length: >= 7 and <= 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    internal static bool HasValidOccurrenceAccounting(DiagnosticReport report)
    {
        if (report.Session is null
            || report.Truncation is null
            || report.Types is null
            || report.Session.AnalyzedFrames < 0
            || report.Session.UnattributedAnalyses < 0)
        {
            return false;
        }

        try
        {
            long frameTotal = report.Session.UnattributedAnalyses;
            foreach (DiagnosticReportType? type in report.Types)
            {
                if (type is null
                    || type.Occurrences < 0
                    || type.TypeUrlOkOccurrences < 0
                    || type.TypeUrlMismatchOccurrences < 0
                    || type.Directions is null
                    || type.Statuses is null
                    || type.Variants is null
                    || CheckedCountAdd(type.TypeUrlOkOccurrences, type.TypeUrlMismatchOccurrences) != type.Occurrences)
                {
                    return false;
                }

                frameTotal = CheckedCountAdd(frameTotal, type.Occurrences);
                long directionTotal = 0;
                foreach (DiagnosticReportDirectionCount? direction in type.Directions)
                {
                    if (direction is null || direction.Occurrences < 0)
                    {
                        return false;
                    }

                    directionTotal = CheckedCountAdd(directionTotal, direction.Occurrences);
                }

                long statusTotal = 0;
                foreach (DiagnosticReportStatusCount? status in type.Statuses)
                {
                    if (status is null || status.Occurrences < 0)
                    {
                        return false;
                    }

                    statusTotal = CheckedCountAdd(statusTotal, status.Occurrences);
                }

                if (directionTotal != type.Occurrences || statusTotal != type.Occurrences)
                {
                    return false;
                }

                long variantTotal = 0;
                foreach (DiagnosticReportVariant? variant in type.Variants)
                {
                    if (variant is null || variant.Occurrences < 0 || variant.Statuses is null)
                    {
                        return false;
                    }

                    long variantStatusTotal = 0;
                    foreach (DiagnosticReportStatusCount? status in variant.Statuses)
                    {
                        if (status is null || status.Occurrences < 0)
                        {
                            return false;
                        }

                        variantStatusTotal = CheckedCountAdd(variantStatusTotal, status.Occurrences);
                    }

                    if (variantStatusTotal != variant.Occurrences)
                    {
                        return false;
                    }

                    variantTotal = CheckedCountAdd(variantTotal, variant.Occurrences);
                }

                bool aggregationFailed = report.Truncation.Reasons?.Contains("aggregation-failed", StringComparer.Ordinal) == true;
                bool variantsOmitted = report.Truncation.VariantsOmitted > 0;
                if (variantTotal > type.Occurrences
                    || (!aggregationFailed && !variantsOmitted && variantTotal != type.Occurrences))
                {
                    return false;
                }
            }

            return true;
        }
        catch (DiagnosticReportException)
        {
            return false;
        }
    }

    internal static bool HasMatchingCompleteFrameAccounting(DiagnosticReport report)
    {
        if (report.Coverage != "complete")
        {
            return true;
        }

        DiagnosticReportSession session = report.Session;
        DiagnosticReportTruncation truncation = report.Truncation;
        if (session.State != DiagnosticReportSessionState.Completed
            || session.ErrorCode is not null
            || session.EnqueuedFrames != session.AnalyzedFrames
            || session.DroppedFrames != 0
            || session.UnattributedAnalyses != 0
            || truncation.OutputTruncated
            || truncation.TypesOmitted != 0
            || truncation.FieldsOmitted != 0
            || truncation.NestedFieldsOmitted != 0
            || truncation.VariantsOmitted != 0
            || truncation.ProblemsOmitted != 0
            || truncation.ShapeValuesOmitted != 0
            || truncation.Reasons.Count != 0
            || report.Types.Any(type => type.DetailTruncated
                || type.Contradictions.Any(contradiction => contradiction.Code == DiagnosticReportContradictionCode.DetailTruncated)))
        {
            return false;
        }

        try
        {
            long frameTotal = session.UnattributedAnalyses;
            foreach (DiagnosticReportType type in report.Types)
            {
                frameTotal = CheckedCountAdd(frameTotal, type.Occurrences);
            }

            return frameTotal == session.AnalyzedFrames;
        }
        catch (DiagnosticReportException)
        {
            return false;
        }
    }

    private static long CheckedCountAdd(long left, long right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            throw new DiagnosticReportException(InvalidReportMessage);
        }
    }

    private static bool IsSafeVersion(string? value, Regex format)
    {
        return value == string.Empty
            || (value is { Length: <= 128 } && format.IsMatch(value) && !_ipv4LikeIdentity.IsMatch(value));
    }

    private static bool IsSafeToken(string? value)
    {
        return value is not null && value.Length is >= 1 and <= 1024 && value.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-');
    }

    private static bool IsSafeFieldPath(string? value)
    {
        return value is not null && value.Length is >= 1 and <= 128 && value.All(character => character is >= '0' and <= '9' or '.');
    }

    private static bool IsSafeSignature(string? value)
    {
        return value is not null && value.Length <= 256 && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '=' or ';' or '.' or '-' or ',');
    }

    private static bool IsSha256(string? value)
    {
        return value is not null && value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool IsSafeContradictionDetail(DiagnosticReportContradiction contradiction)
    {
        if (contradiction.Detail is null || contradiction.Detail.Length is 0 or > 256)
        {
            return false;
        }

        string occurrenceCount = contradiction.Occurrences.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string detail = contradiction.Detail;
        return contradiction.Code switch
        {
            DiagnosticReportContradictionCode.TypeUrlMismatch => detail == "TYPE_URL_MISMATCH count=" + occurrenceCount,
            DiagnosticReportContradictionCode.MessageMismatch => detail == "MESSAGE_MISMATCH count=" + occurrenceCount,
            DiagnosticReportContradictionCode.MixedStatus => HasCount(detail, "MIXED_STATUS count=", 2),
            DiagnosticReportContradictionCode.ShapeVariation => HasCount(detail, "SHAPE_VARIATION count=", 1),
            DiagnosticReportContradictionCode.FieldDeclarationMismatch => HasFieldMismatchDetail(detail, occurrenceCount),
            DiagnosticReportContradictionCode.DetailTruncated => detail == "DETAIL_TRUNCATED count=" + occurrenceCount,
            _ => false,
        };
    }

    private static bool HasCount(string detail, string prefix, int minimum)
    {
        if (!detail.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string value = detail[prefix.Length..];
        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count)
            && count >= minimum
            && count.ToString(System.Globalization.CultureInfo.InvariantCulture) == value;
    }

    private static bool HasFieldMismatchDetail(string detail, string occurrenceCount)
    {
        const string prefix = "FIELD_DECLARATION_MISMATCH field=";
        if (!detail.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        int countDelimiter = detail.IndexOf(" count=", prefix.Length, StringComparison.Ordinal);
        if (countDelimiter < 0 || detail[(countDelimiter + 7)..] != occurrenceCount)
        {
            return false;
        }

        string fieldNumber = detail[prefix.Length..countDelimiter];
        return int.TryParse(fieldNumber, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number)
            && number >= 0
            && number.ToString(System.Globalization.CultureInfo.InvariantCulture) == fieldNumber;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            return long.MaxValue;
        }

        return left + right;
    }

    private sealed record NestedCandidate(int TypeIndex, string Token, int[] Path, int Depth);

    private sealed record ShapeCandidate(int TypeIndex, int[] Path, string Property, int Value);
}
