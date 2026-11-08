using System.Collections.Immutable;
using D3B.Gui.Calibration;

namespace D3B.Gui.Safety;

/// <summary>A map element known from protocol state.</summary>
public sealed record GuiKnownElement(
    int ElementId,
    LogicalCell? Cell,
    int InteractiveId,
    int? State,
    ImmutableArray<int> EnabledSkillIds);
