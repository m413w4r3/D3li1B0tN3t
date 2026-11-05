using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

internal sealed class DiagnosticReportFieldAccumulator
{
    private readonly int _maxShapeValues;
    private readonly SortedSet<DiagnosticWireType> _wireTypes = [];
    private readonly SortedSet<int> _varintBytes = [];
    private readonly SortedSet<int> _fixedLength = [];
    private readonly SortedSet<int> _length = [];
    private readonly SortedSet<int> _fieldCount = [];
    private readonly SortedDictionary<DiagnosticProblemCode, long> _problems = [];
    private bool _utf8AnyFalse;
    private bool _utf8Unknown;
    private bool _utf8Seen;
    private bool _lengthCapped;
    private bool _lengthCappedSeen;
    private bool _zeroLength;
    private bool _zeroLengthSeen;
    private bool _truncated;
    private bool _truncatedSeen;

    public DiagnosticReportFieldAccumulator(int number, DiagnosticField? schemaField, int maxShapeValues)
    {
        Number = number;
        Name = IsSafeSchemaFieldName(schemaField?.Name) ? schemaField!.Name : null;
        DeclaredKind = schemaField is not null && Enum.IsDefined(schemaField.Kind) ? schemaField.Kind : null;
        Repeated = schemaField?.Repeated ?? false;
        Packed = schemaField?.Packed ?? false;
        _maxShapeValues = maxShapeValues;
        NestedFields = [];
    }

    public int Number { get; }

    public string? Name { get; }

    public DiagnosticFieldKind? DeclaredKind { get; }

    public bool Repeated { get; }

    public bool Packed { get; }

    public long Occurrences { get; private set; }

    public long ShapeValuesOmitted { get; private set; }

    public long DeclarationMismatchOccurrences { get; private set; }

    public Dictionary<int, DiagnosticReportFieldAccumulator> NestedFields { get; }

    public void Observe(DiagnosticFieldAnalysis analysis)
    {
        Occurrences = SaturatingAdd(Occurrences, Math.Max(0, analysis.Occurrences));
        foreach (DiagnosticWireType wireType in analysis.WireTypes)
        {
            if (Enum.IsDefined(wireType))
            {
                _wireTypes.Add(wireType);
            }
        }

        MergeValues(analysis.Observed.VarintBytes, _varintBytes);
        MergeValues(analysis.Observed.FixedLength, _fixedLength);
        MergeValues(analysis.Observed.Length, _length);
        MergeValues(analysis.Observed.FieldCount, _fieldCount);
        MergeUtf8(analysis.Observed.Utf8);
        MergeOr(analysis.Observed.LengthCapped, ref _lengthCapped, ref _lengthCappedSeen);
        MergeOr(analysis.Observed.ZeroLength, ref _zeroLength, ref _zeroLengthSeen);
        MergeOr(analysis.Observed.Truncated, ref _truncated, ref _truncatedSeen);

        foreach (DiagnosticProblemCode problem in analysis.Problems.Distinct())
        {
            if (Enum.IsDefined(problem))
            {
                long count = Math.Max(1, analysis.Occurrences);
                _problems[problem] = SaturatingAdd(_problems.GetValueOrDefault(problem), count);
            }
        }
    }

    public void RecordDeclarationMismatch()
    {
        DeclarationMismatchOccurrences = SaturatingAdd(DeclarationMismatchOccurrences, 1);
    }

    public DiagnosticReportField Build(int maxProblems, Action<long> recordProblemsOmitted)
    {
        KeyValuePair<DiagnosticProblemCode, long>[] retainedProblems = _problems.Take(maxProblems).ToArray();
        long omittedProblems = _problems.Count - retainedProblems.Length;
        if (omittedProblems > 0)
        {
            recordProblemsOmitted(omittedProblems);
        }

        DiagnosticObservedShape observed = new()
        {
            VarintBytes = ToOptionalList(_varintBytes),
            FixedLength = ToOptionalList(_fixedLength),
            Length = ToOptionalList(_length),
            LengthCapped = _lengthCappedSeen ? _lengthCapped : null,
            Utf8 = GetUtf8Value(),
            FieldCount = ToOptionalList(_fieldCount),
            ZeroLength = _zeroLengthSeen ? _zeroLength : null,
            Truncated = _truncatedSeen ? _truncated : null,
        };

        return new DiagnosticReportField
        {
            Number = Number,
            Name = Name,
            DeclaredKind = DeclaredKind,
            Repeated = Repeated,
            Packed = Packed,
            Occurrences = Occurrences,
            WireTypes = _wireTypes.ToArray(),
            Observed = observed,
            Problems = retainedProblems.Length == 0
                ? null
                : retainedProblems.Select(problem => new DiagnosticReportFieldProblemCount
                {
                    Code = problem.Key,
                    Occurrences = problem.Value,
                }).ToArray(),
        };
    }

    private static bool IsSafeSchemaFieldName(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private void MergeValues(IReadOnlyList<int>? values, SortedSet<int> retained)
    {
        if (values is null)
        {
            return;
        }

        foreach (int value in values)
        {
            if (value < 0)
            {
                ShapeValuesOmitted = SaturatingAdd(ShapeValuesOmitted, 1);
                continue;
            }

            if (retained.Contains(value))
            {
                continue;
            }

            if (retained.Count < _maxShapeValues)
            {
                retained.Add(value);
                continue;
            }

            ShapeValuesOmitted = SaturatingAdd(ShapeValuesOmitted, 1);
            if (value < retained.Max)
            {
                retained.Remove(retained.Max);
                retained.Add(value);
            }
        }
    }

    private void MergeUtf8(bool? value)
    {
        if (value is null)
        {
            _utf8Unknown = true;
        }
        else if (value.Value)
        {
            _utf8Seen = true;
        }
        else
        {
            _utf8AnyFalse = true;
            _utf8Seen = true;
        }
    }

    private bool? GetUtf8Value()
    {
        if (_utf8AnyFalse)
        {
            return false;
        }

        if (!_utf8Seen || _utf8Unknown)
        {
            return null;
        }

        return true;
    }

    private static void MergeOr(bool? value, ref bool current, ref bool seen)
    {
        if (value is not null)
        {
            current |= value.Value;
            seen = true;
        }
    }

    private static int[]? ToOptionalList(SortedSet<int> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        return values.ToArray();
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
