namespace D3B.App.ViewModels;

/// <summary>A profile a character may play, or none.</summary>
/// <param name="Id">The profile's id, or <see langword="null"/> for none.</param>
/// <param name="Name">Its name, or what the app says for none.</param>
internal sealed record ProfileOptionViewModel(int? Id, string Name);
