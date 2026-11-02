namespace D3B.Bot.Tests.Settings;

/// <summary>Settings of every kind, as a test activity declares them.</summary>
public sealed class TestSettings
{
    public int Walks { get; set; } = 10;

    public bool UseBank { get; set; } = true;

    public string? Greeting { get; set; } = "Hello";

    public TestFocus Focus { get; set; } = TestFocus.Job;

    public IReadOnlyList<TestFocus> Order { get; set; } = [TestFocus.Area];

    public int? Job { get; set; }

    public IReadOnlyList<int> Jobs { get; set; } = [];

    public IReadOnlyList<int> Resources { get; set; } = [];

    public int? Area { get; set; }

    public IReadOnlyList<int> SubAreas { get; set; } = [];

    public int Lowest { get; set; } = 1;

    public int Highest { get; set; } = 5;

    public int Undeclared { get; set; } = 3;
}
