namespace D3B.App.Tests;

/// <summary>Waits for what the app reads off its own thread, as its window does by refreshing.</summary>
internal static class Waiting
{
    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
