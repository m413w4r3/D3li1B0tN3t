using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Replay;

public sealed class DiagnosticFrameEligibilityTests
{
    [Fact]
    public void AcceptsCanonicalBuildMessageToken()
    {
        HashSet<string> buildTokens = new(StringComparer.Ordinal) { "lok" };

        bool eligible = DiagnosticFrameEligibility.IsEligible("type.ankama.com/lok", "lok", buildTokens);

        Assert.True(eligible);
    }

    [Fact]
    public void RejectsNumericIdentifierSuffix()
    {
        HashSet<string> buildTokens = new(StringComparer.Ordinal) { "lok" };

        bool eligible = DiagnosticFrameEligibility.IsEligible(
            "type.ankama.com/123456789",
            "123456789",
            buildTokens);

        Assert.False(eligible);
    }

    [Fact]
    public void RejectsForeignTypeUrlPrefix()
    {
        HashSet<string> buildTokens = new(StringComparer.Ordinal) { "lok" };

        bool eligible = DiagnosticFrameEligibility.IsEligible("foreign.example/lok", "lok", buildTokens);

        Assert.False(eligible);
    }

    [Fact]
    public void RejectsOverlongCarriedType()
    {
        string token = new('a', 129);
        HashSet<string> buildTokens = new(StringComparer.Ordinal) { token };

        bool eligible = DiagnosticFrameEligibility.IsEligible("type.ankama.com/" + token, token, buildTokens);

        Assert.False(eligible);
    }

    [Fact]
    public void RejectsTokenAbsentFromBuildSchema()
    {
        HashSet<string> buildTokens = new(StringComparer.Ordinal) { "lok" };

        bool eligible = DiagnosticFrameEligibility.IsEligible("type.ankama.com/unknown", "unknown", buildTokens);

        Assert.False(eligible);
    }
}
