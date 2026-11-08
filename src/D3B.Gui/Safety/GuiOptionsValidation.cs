using System.Collections.Immutable;

namespace D3B.Gui.Safety;

/// <summary>The result of validating GUI options.</summary>
public sealed record GuiOptionsValidation(bool IsValid, ImmutableArray<string> Problems);
