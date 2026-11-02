namespace D3B.Bot.Inspection;

/// <summary>A field of a readable message, as the protocol inspector unfolds a frame (D115).</summary>
/// <param name="Depth">How deep it lies: 0 for the message's own fields, 1 for those of a message it holds.</param>
/// <param name="Name">Its name, in lower camel case as the readable protocol's JSON names it, with its index in a list.</param>
/// <param name="Value">Its value as text, or <see langword="null"/> for a message, whose fields follow 1 deeper.</param>
/// <param name="IsHidden">Whether its value is a secret, such as the account's ticket, shown as <see cref="FrameFields.Hidden"/>.</param>
public sealed record FrameField(int Depth, string Name, string? Value, bool IsHidden);
