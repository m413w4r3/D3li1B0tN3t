namespace D3B.Gui.Safety;

/// <summary>Describes a request's origin and whether it starts gameplay.</summary>
public sealed record GameInputRequestClassification(GameInputRequestSource Source, bool IsGameplay);
