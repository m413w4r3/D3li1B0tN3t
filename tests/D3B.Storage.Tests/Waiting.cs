namespace D3B.Storage.Tests;

/// <summary>Waits for what the storage does off the test's thread: a profile started on a bot's connection, a log written.</summary>
internal static class Waiting
{
    public static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
