namespace D3B.Diagnostic.Resolution;

internal sealed record ResolverShapeResult(bool Exact, double Score, IReadOnlyList<ResolverContradiction> Contradictions);
