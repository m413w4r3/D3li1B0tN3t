namespace D3B.Bot.Settings;

/// <summary>Who gathers a resource: the profession, and the lowest level it gathers it from.</summary>
/// <param name="JobId">The profession.</param>
/// <param name="Level">The profession's lowest level that gathers it.</param>
public readonly record struct ResourceSource(int JobId, int Level);
