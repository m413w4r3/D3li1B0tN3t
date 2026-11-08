using System.Collections.Immutable;

namespace D3B.Gui.Safety;

/// <summary>Validates GUI configuration without binding it to a dependency injection package.</summary>
public static class GuiOptionsValidator
{
    /// <summary>Checks all P2N GUI option constraints.</summary>
    /// <param name="options">The options to validate.</param>
    /// <returns>A result containing every validation problem.</returns>
    public static GuiOptionsValidation Validate(GuiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var problems = ImmutableArray.CreateBuilder<string>();

        if (!Enum.IsDefined(options.Backend))
            problems.Add("Gui:Backend must name a supported backend.");

        if (!Enum.IsDefined(options.Mode))
            problems.Add("Gui:Mode must name a supported mode.");

        if (options.AllowRealInput)
            problems.Add("Real input is refused in P2N (Gui:AllowRealInput).");

        if (options.CaptureTimeoutMs is < 100 or > 10_000)
            problems.Add("Gui:CaptureTimeoutMs must be between 100 and 10000.");

        if (options.MaxCaptureBytes is < 1_048_576 or > 67_108_864)
            problems.Add("Gui:MaxCaptureBytes must be between 1048576 and 67108864.");

        if (options.MaxWidth is < 1 or > 7680)
            problems.Add("Gui:MaxWidth must be between 1 and 7680.");

        if (options.MaxHeight is < 1 or > 7680)
            problems.Add("Gui:MaxHeight must be between 1 and 7680.");

        if (options.CalibrationMaxAgeSeconds is < 1 or > 3600)
            problems.Add("Gui:CalibrationMaxAgeSeconds must be between 1 and 3600.");

        return new GuiOptionsValidation(problems.Count is 0, problems.ToImmutable());
    }
}
