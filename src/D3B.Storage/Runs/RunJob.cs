namespace D3B.Storage.Runs;

/// <summary>A profession a run's character gained experience in (D146).</summary>
/// <param name="JobId">The profession, as the client's tables name it.</param>
/// <param name="FromLevel">Its level as the run began, or as the bot first saw it.</param>
/// <param name="ToLevel">Its level as the run was last written.</param>
/// <param name="Experience">The experience gained.</param>
public sealed record RunJob(int JobId, int FromLevel, int ToLevel, long Experience);
