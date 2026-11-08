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

        if (options.CaptureTimeoutMs is < 100 or > 2000)
            problems.Add("Gui:CaptureTimeoutMs must be between 100 and 2000.");

        if (options.MaxCaptureBytes is < 1_048_576 or > 16_777_216)
            problems.Add("Gui:MaxCaptureBytes must be between 1048576 and 16777216.");

        if (options.MaxWidth is < 1 or > 3840)
            problems.Add("Gui:MaxWidth must be between 1 and 3840.");

        if (options.MaxHeight is < 1 or > 2160)
            problems.Add("Gui:MaxHeight must be between 1 and 2160.");

        if (options.CalibrationMaxAgeSeconds is < 1 or > 30)
            problems.Add("Gui:CalibrationMaxAgeSeconds must be between 1 and 30.");

        return new GuiOptionsValidation(problems.Count is 0, problems.ToImmutable());
    }

    /// <summary>Validates options at a composition boundary and rejects invalid startup configuration.</summary>
    /// <remarks>P2O must also call this validator when the host starts.</remarks>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentException">One or more GUI options violate the P2N policy.</exception>
    public static void ValidateOrThrow(GuiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        GuiOptionsValidation validation = Validate(options);

        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"Invalid GUI options: {string.Join(" ", validation.Problems)}",
                nameof(options));
        }
    }
}
