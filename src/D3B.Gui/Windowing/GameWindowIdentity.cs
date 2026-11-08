namespace D3B.Gui.Windowing;

/// <summary>Identifies a game window and the status of its process identity check.</summary>
/// <param name="Xid">The numeric X11 window identifier.</param>
/// <param name="ProcessId">The owning process identifier, when exposed by X11.</param>
/// <param name="ExecutablePath">The owning process executable path, when readable.</param>
/// <param name="Title">The window title, which is not identity evidence.</param>
/// <param name="Status">The result of the process identity check.</param>
/// <remarks>A title is never proof of identity. Any status other than <see cref="GameWindowIdentityStatus.Verified"/> forbids future actions.</remarks>
public sealed record GameWindowIdentity(
    long Xid,
    int? ProcessId,
    string? ExecutablePath,
    string? Title,
    GameWindowIdentityStatus Status);
