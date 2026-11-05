using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

internal sealed class DiagnosticReportTypeAccumulator
{
    private readonly int _maxProblems;

    public DiagnosticReportTypeAccumulator(string token, int maxProblems)
    {
        Token = token;
        _maxProblems = maxProblems;
        Directions = new Dictionary<string, long>(StringComparer.Ordinal);
        Statuses = [];
        Fields = [];
        Problems = [];
        Variants = new SortedDictionary<string, SortedDictionary<DiagnosticStatus, long>>(StringComparer.Ordinal);
    }

    public string Token { get; }

    public string? Message { get; private set; }

    public long Occurrences { get; private set; }

    public Dictionary<string, long> Directions { get; }

    public SortedDictionary<DiagnosticStatus, long> Statuses { get; }

    public long TypeUrlOkOccurrences { get; private set; }

    public long TypeUrlMismatchOccurrences { get; private set; }

    public Dictionary<int, DiagnosticReportFieldAccumulator> Fields { get; }

    public Dictionary<(DiagnosticProblemCode Code, string? FieldPath), long> Problems { get; }

    public SortedDictionary<string, SortedDictionary<DiagnosticStatus, long>> Variants { get; }

    public long ProblemsOmitted { get; private set; }

    public long VariantsOmitted { get; private set; }

    public long MessageMismatchOccurrences { get; private set; }

    public long DetailTruncatedOccurrences { get; private set; }

    public long FieldDeclarationMismatchOccurrences { get; private set; }

    public int? FirstFieldDeclarationMismatchNumber { get; private set; }

    public bool HasOmittedVariant { get; private set; }

    public int FieldNodeCount { get; set; }

    public void Observe(DiagnosticAnalysis analysis, string direction, DiagnosticStatus status)
    {
        Occurrences = SaturatingAdd(Occurrences, 1);
        Directions[direction] = SaturatingAdd(Directions.GetValueOrDefault(direction), 1);
        Statuses[status] = SaturatingAdd(Statuses.GetValueOrDefault(status), 1);
        if (analysis.TypeUrlOk)
        {
            TypeUrlOkOccurrences = SaturatingAdd(TypeUrlOkOccurrences, 1);
        }
        else
        {
            TypeUrlMismatchOccurrences = SaturatingAdd(TypeUrlMismatchOccurrences, 1);
        }

        if (analysis.DetailTruncated is true)
        {
            DetailTruncatedOccurrences = SaturatingAdd(DetailTruncatedOccurrences, 1);
        }
    }

    public void RecordMessage(string? safeMessage, bool mismatch)
    {
        if (mismatch)
        {
            MessageMismatchOccurrences = SaturatingAdd(MessageMismatchOccurrences, 1);
        }

        if (Message is null && safeMessage is not null)
        {
            Message = safeMessage;
        }
    }

    public void RecordProblem(DiagnosticProblemCode code, string? fieldPath)
    {
        (DiagnosticProblemCode Code, string? FieldPath) key = (code, fieldPath);
        if (Problems.TryGetValue(key, out long current))
        {
            Problems[key] = SaturatingAdd(current, 1);
            return;
        }

        if (Problems.Count >= _maxProblems)
        {
            (DiagnosticProblemCode Code, string? FieldPath) lastKey = Problems.Keys.Aggregate((left, right) => CompareProblems(left, right) > 0 ? left : right);
            if (CompareProblems(key, lastKey) >= 0)
            {
                ProblemsOmitted = SaturatingAdd(ProblemsOmitted, 1);
                return;
            }

            ProblemsOmitted = SaturatingAdd(ProblemsOmitted, Problems[lastKey]);
            Problems.Remove(lastKey);
        }

        Problems.Add(key, 1);
    }

    public void RecordFieldDeclarationMismatch(int fieldNumber)
    {
        FieldDeclarationMismatchOccurrences = SaturatingAdd(FieldDeclarationMismatchOccurrences, 1);
        if (FirstFieldDeclarationMismatchNumber is null || fieldNumber < FirstFieldDeclarationMismatchNumber.Value)
        {
            FirstFieldDeclarationMismatchNumber = fieldNumber;
        }
    }

    public void RecordVariant(string signature, DiagnosticStatus status, int maxVariants)
    {
        if (Variants.TryGetValue(signature, out SortedDictionary<DiagnosticStatus, long>? statuses))
        {
            statuses[status] = SaturatingAdd(statuses.GetValueOrDefault(status), 1);
            return;
        }

        if (Variants.Count >= maxVariants)
        {
            string lastSignature = Variants.Keys.Last();
            if (StringComparer.Ordinal.Compare(signature, lastSignature) >= 0)
            {
                VariantsOmitted = SaturatingAdd(VariantsOmitted, 1);
                HasOmittedVariant = true;
                return;
            }

            VariantsOmitted = SaturatingAdd(VariantsOmitted, Variants[lastSignature].Values.Aggregate(0L, SaturatingAdd));
            Variants.Remove(lastSignature);
            HasOmittedVariant = true;
        }

        SortedDictionary<DiagnosticStatus, long> newStatuses = [];
        newStatuses.Add(status, 1);
        Variants.Add(signature, newStatuses);
    }

    private static int CompareProblems((DiagnosticProblemCode Code, string? FieldPath) left, (DiagnosticProblemCode Code, string? FieldPath) right)
    {
        int codeComparison = left.Code.CompareTo(right.Code);
        if (codeComparison != 0)
        {
            return codeComparison;
        }

        return StringComparer.Ordinal.Compare(left.FieldPath, right.FieldPath);
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
