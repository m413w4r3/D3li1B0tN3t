namespace D3B.Diagnostic.Replay;

/// <summary>Checks whether an observed frame can be safely sent to diagnostic replay.</summary>
public static class DiagnosticFrameEligibility
{
    private const string TypeUrlPrefix = "type.ankama.com/";

    /// <summary>Returns whether the frame token is a canonical message name from the build schema.</summary>
    /// <param name="typeUrl">The observed envelope type URL.</param>
    /// <param name="carriedType">The message token carried by the envelope.</param>
    /// <param name="buildTokens">The exported build schema message names.</param>
    /// <returns><see langword="true"/> when the frame has a canonical schema message token.</returns>
    public static bool IsEligible(string? typeUrl, string? carriedType, IReadOnlySet<string> buildTokens)
    {
        ArgumentNullException.ThrowIfNull(buildTokens);

        if (carriedType is null
            || carriedType.Length is 0 or > 128
            || !carriedType.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_')
            || !string.Equals(typeUrl, TypeUrlPrefix + carriedType, StringComparison.Ordinal))
        {
            return false;
        }

        return buildTokens.Any(token => string.Equals(token, carriedType, StringComparison.Ordinal));
    }
}
