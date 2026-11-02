using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Data.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Settings;

public sealed class ActivitySettingsTests
{
    private static LocalizedText Text(string text)
    {
        return LocalizedText.Invariant(text);
    }

    private static ActivityDefinition Register(Action<ActivitySettingsBuilder<TestSettings>> describe)
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>("test", Text("Test"), describe);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    private static ActivitySettings Every()
    {
        return Register(settings =>
        {
            settings.Section(Text("Pace"));
            settings.Number(s => s.Walks, Text("Walks"), 1, 100).Help(Text("How many walks"));
            settings.Flag(s => s.UseBank, Text("Use the bank"));
            settings.Text(s => s.Greeting, Text("Greeting"), 10);
            settings.Section(Text("Focus"));
            settings.Choice(s => s.Focus, Text("Focus"), focus => Text($"On {focus}"));
            settings.Choices(s => s.Order, Text("Order"), focus => Text($"On {focus}")).Ordered();
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Focus == TestFocus.Job);
            settings.Resources(s => s.Resources, Text("Resources")).Within(s => s.Job).Ordered();
            settings.Area(s => s.Area, Text("Area"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Area);
            settings.Number(s => s.Lowest, Text("Lowest"), 0, 10);
            settings.Number(s => s.Highest, Text("Highest"), 0, 10);
            settings.Rule(s => s.Lowest <= s.Highest, Text("The lowest is above the highest."));
        }).Settings;
    }

    private static TestSettings Read(ActivitySettings settings, string json, out IReadOnlyList<string> reset)
    {
        var reading = settings.Read(json);

        reset = [.. reading.Reset.Select(setting => setting.Key)];

        return Assert.IsType<TestSettings>(reading.Settings);
    }

    [Fact]
    public void AModuleDeclaresEachSettingInOrderUnderItsSection()
    {
        var settings = Every();
        var invariant = CultureInfo.InvariantCulture;

        Assert.Equal(typeof(TestSettings), settings.Type);
        Assert.Equal(
            ["Walks", "UseBank", "Greeting", "Focus", "Order", "Job", "Resources", "Area", "SubAreas", "Lowest", "Highest"],
            settings.All.Select(setting => setting.Key));
        Assert.Equal(
            [SettingKind.Number, SettingKind.Flag, SettingKind.Text, SettingKind.Choice, SettingKind.Choice, SettingKind.Job, SettingKind.Resource, SettingKind.Area, SettingKind.SubArea],
            settings.All.Take(9).Select(setting => setting.Kind));
        Assert.Equal(["Pace", "Pace", "Pace", "Focus"], settings.All.Take(4).Select(setting => setting.Section?.In(invariant)));

        var walks = settings.All[0];

        Assert.Equal((1, 100), (walks.Minimum, walks.Maximum));
        Assert.Equal("How many walks", walks.Help?.In(invariant));
        Assert.Equal(10, settings.All[2].MaximumLength);
        Assert.Equal(["On Job", "On Area", "On Nothing"], settings.All[3].Options.Select(option => option.Text.In(invariant)));
        Assert.Equal([TestFocus.Job, TestFocus.Area, TestFocus.Nothing], settings.All[3].Options.Select(option => (TestFocus)option.Value));
        Assert.True(settings.All[4].IsList && settings.All[4].IsOrdered);
        Assert.True(settings.All[5].IsConditional);
        Assert.Same(settings.All[5], settings.All[6].Within);
        Assert.Same(settings.All[7], settings.All[8].Within);
        Assert.False(settings.All[8].IsOrdered);
    }

    [Fact]
    public void TheDefaultsAreTheClassOwnValues()
    {
        var settings = Assert.IsType<TestSettings>(Every().CreateDefault());

        Assert.Equal((10, true, "Hello", TestFocus.Job), (settings.Walks, settings.UseBank, settings.Greeting, settings.Focus));
        Assert.Equal([TestFocus.Area], settings.Order);
        Assert.Null(settings.Job);
        Assert.Empty(settings.Resources);
    }

    [Fact]
    public void SettingsWrittenAreReadBackAsTheyWere()
    {
        var declared = Every();
        var settings = new TestSettings
        {
            Walks = 42,
            UseBank = false,
            Greeting = "Salut",
            Focus = TestFocus.Area,
            Order = [TestFocus.Nothing, TestFocus.Job],
            Job = TestReferences.Lumberjack,
            Resources = [TestReferences.Chestnut, TestReferences.Ash],
            Area = TestReferences.Amakna,
            SubAreas = [TestReferences.Hills],
            Undeclared = 99,
        };

        var json = declared.Write(settings);
        var read = Read(declared, json, out var reset);

        Assert.Empty(reset);
        Assert.Equal(
            """{"Walks":42,"UseBank":false,"Greeting":"Salut","Focus":"Area","Order":["Nothing","Job"],"Job":2,"Resources":[304,303],"Area":1,"SubAreas":[11],"Lowest":1,"Highest":5}""",
            json);
        Assert.Equal((42, false, "Salut", TestFocus.Area), (read.Walks, read.UseBank, read.Greeting, read.Focus));
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], read.Order);
        Assert.Equal(TestReferences.Lumberjack, read.Job);
        Assert.Equal([TestReferences.Chestnut, TestReferences.Ash], read.Resources);
        Assert.Equal([TestReferences.Hills], read.SubAreas);

        // What the module does not declare is not a setting: it keeps the class's own value.
        Assert.Equal(3, read.Undeclared);
    }

    [Fact]
    public void AStoredValueThatNoLongerReadsKeepsItsDefaultAndIsReported()
    {
        var declared = Every();
        var read = Read(declared, """{"Walks":"many","Greeting":7,"Focus":"Dungeon","Order":["Job","Dungeon"],"Job":2,"Resources":[303,"ash"],"Gone":true}""", out var reset);

        Assert.Equal(["Walks", "Greeting", "Focus", "Order", "Resources"], reset);
        Assert.Equal((10, "Hello", TestFocus.Job), (read.Walks, read.Greeting, read.Focus));

        // What still reads of a list is kept.
        Assert.Equal([TestFocus.Job], read.Order);
        Assert.Equal([TestReferences.Ash], read.Resources);
        Assert.Equal(TestReferences.Lumberjack, read.Job);
    }

    [Fact]
    public void StoredSettingsThatAreNoJsonObjectAreAllReset()
    {
        var declared = Every();

        Read(declared, "not json", out var corrupt);
        Read(declared, "[1, 2]", out var array);
        Read(declared, "", out var empty);

        Assert.Equal(declared.All.Count, corrupt.Count);
        Assert.Equal(declared.All.Count, array.Count);
        Assert.Empty(empty);
    }

    [Fact]
    public void EachSettingThatShowsIsCheckedAgainstItsBounds()
    {
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Text(s => s.Greeting, Text("Greeting"), 5).Required();
            settings.Choices(s => s.Order, Text("Order"), focus => Text($"{focus}")).Required();
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Focus == TestFocus.Job);
            settings.Area(s => s.Area, Text("Area")).Required();
            settings.Number(s => s.Lowest, Text("Lowest"), 0, 10);
            settings.Number(s => s.Highest, Text("Highest"), 0, 10);
            settings.Rule(s => s.Lowest <= s.Highest, Text("The lowest is above the highest."));
        }).Settings;
        var settings = new TestSettings { Walks = 101, Greeting = "  ", Order = [], Area = TestReferences.Amakna, Lowest = 6, Highest = 4 };

        Assert.True(declared.All.Single(setting => setting.Key == "Job").IsVisible(settings));
        Assert.False(declared.IsVisible(declared.All[3], new TestSettings { Focus = TestFocus.Area }));

        var problems = declared.Check(settings);

        Assert.Equal(
            [("Walks", SettingProblemKind.OutOfRange), ("Greeting", SettingProblemKind.Required), ("Order", SettingProblemKind.Required), (null, SettingProblemKind.Rule)],
            problems.Select(problem => (problem.Setting?.Key, problem.Kind)));
        Assert.Equal("The lowest is above the highest.", problems[^1].Message?.In(CultureInfo.InvariantCulture));

        settings.Greeting = "Bonjour";
        settings.Area = null;
        settings.Walks = 0;

        Assert.Equal(
            [("Walks", SettingProblemKind.OutOfRange), ("Greeting", SettingProblemKind.TooLong), ("Order", SettingProblemKind.Required), ("Area", SettingProblemKind.Required), (null, SettingProblemKind.Rule)],
            declared.Check(settings).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void AHiddenSettingIsNotChecked()
    {
        var declared = Every();
        var settings = new TestSettings { Focus = TestFocus.Area, Job = 999 };

        Assert.Empty(declared.Check(settings, TestReferences.Game()));

        settings.Focus = TestFocus.Job;

        Assert.Equal([("Job", SettingProblemKind.Unknown)], declared.Check(settings, TestReferences.Game()).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void TheGamesValuesAreCheckedAgainstThePackAndWithinTheirParent()
    {
        var declared = Every();
        var game = TestReferences.Game();
        var settings = new TestSettings
        {
            Job = TestReferences.Miner,
            Resources = [TestReferences.Ash],
            Area = TestReferences.Astrub,
            SubAreas = [TestReferences.Forest, 404],
        };

        // Without the pack, ids are not checked; with it, an unknown one comes before one of another parent.
        Assert.Empty(declared.Check(settings));
        Assert.Equal(
            [("Resources", SettingProblemKind.NotWithin), ("SubAreas", SettingProblemKind.Unknown)],
            declared.Check(settings, game).Select(problem => (problem.Setting?.Key, problem.Kind)));

        settings.SubAreas = [TestReferences.Forest];
        settings.Job = TestReferences.Lumberjack;

        Assert.Equal([("SubAreas", SettingProblemKind.NotWithin)], declared.Check(settings, game).Select(problem => (problem.Setting?.Key, problem.Kind)));

        // No parent chosen: every value of the game is within it.
        settings.Area = null;

        Assert.Empty(declared.Check(settings, game));
    }

    [Fact]
    public void AListHoldsEachValueOnceInOrder()
    {
        var declared = Every();
        var resources = declared.All.Single(setting => setting.Key == "Resources");
        var order = declared.All.Single(setting => setting.Key == "Order");
        var settings = new TestSettings();

        resources.SetValue(settings, new List<int> { TestReferences.Iron, TestReferences.Ash, TestReferences.Iron });
        order.SetValue(settings, new object[] { TestFocus.Nothing, TestFocus.Job, TestFocus.Nothing });

        Assert.Equal([TestReferences.Iron, TestReferences.Ash], settings.Resources);
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], settings.Order);
        Assert.Equal([TestFocus.Nothing, TestFocus.Job], order.GetValues(settings).Cast<TestFocus>());

        settings.Resources = [TestReferences.Ash, TestReferences.Ash];

        Assert.Equal([("Resources", SettingProblemKind.Repeated)], declared.Check(settings).Select(problem => (problem.Setting?.Key, problem.Kind)));
    }

    [Fact]
    public void ASettingRefusesAValueItDoesNotHold()
    {
        var declared = Every();
        var settings = new TestSettings();

        Assert.Throws<ArgumentException>(() => declared.All[0].SetValue(settings, "ten"));
        Assert.Throws<ArgumentException>(() => declared.All[3].SetValue(settings, 1));
        Assert.Throws<ArgumentException>(() => declared.All[6].SetValue(settings, 303));
        Assert.Throws<ArgumentException>(() => declared.All[6].SetValue(settings, new List<string> { "ash" }));

        declared.All[2].SetValue(settings, null);
        declared.All[5].SetValue(settings, null);

        Assert.Equal("", settings.Greeting);
        Assert.Equal("", declared.All[2].GetValue(new TestSettings { Greeting = null }));
    }

    [Fact]
    public void ACopyHoldsTheSameValuesApart()
    {
        var declared = Every();
        var settings = new TestSettings { Walks = 7, Resources = [TestReferences.Ash] };
        var copy = Assert.IsType<TestSettings>(declared.Copy(settings));

        copy.Walks = 8;

        Assert.Equal(7, settings.Walks);
        Assert.Equal([TestReferences.Ash], copy.Resources);
        Assert.NotSame(settings.Resources, copy.Resources);
    }

    [Fact]
    public void AnActivitysRuleOrConditionThatThrowsNeverThrowsInTheApp()
    {
        var declared = Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job")).VisibleWhen(s => s.Greeting!.Length > 0);
            settings.Rule(s => s.Greeting!.Length > 0, Text("A greeting"));
        }).Settings;
        var settings = new TestSettings { Greeting = "Hi" };

        settings.Greeting = null;

        Assert.True(declared.IsVisible(declared.All[0], settings));
        Assert.Equal([SettingProblemKind.Rule], declared.Check(settings).Select(problem => problem.Kind));
    }

    [Fact]
    public void DefaultsThatDoNotHoldStopTheModuleFromRegisteringItsActivity()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Number(s => s.Walks, Text("Walks"), 1, 5)));

        Assert.Equal("The default settings of TestSettings do not hold: Walks (OutOfRange).", exception.Message);
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Text(s => s.Greeting, Text("Greeting"), 3)));
    }

    [Fact]
    public void AValueTheUserMustGiveNeedsNoDefault()
    {
        var declared = Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job")).Required();
            settings.Rule(s => s.Job is not null, Text("Choose a profession."));
        }).Settings;

        Assert.Equal([SettingProblemKind.Required, SettingProblemKind.Rule], declared.Check(declared.CreateDefault()).Select(problem => problem.Kind));
    }

    [Fact]
    public void ASettingIsAPublicPropertyDeclaredOnce()
    {
        Assert.Throws<ArgumentException>(() => Register(settings => settings.Number(s => s.Walks + 1, Text("Walks"), 1, 100)));
        Assert.Throws<ArgumentException>(() => Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Number(s => s.Walks, Text("Walks again"), 1, 100);
        }));
    }

    [Fact]
    public void AValueOfTheGameIsSoughtWithinAParentDeclaredBeforeOfItsKind()
    {
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Area)));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Within(s => s.Job);
        }));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Area(s => s.Area, Text("Area"));
            settings.Job(s => s.Job, Text("Job")).Within(s => s.Area);
        }));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Flag(s => s.UseBank, Text("Bank")).Required()));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Job(s => s.Job, Text("Job")).Ordered()));
    }

    [Fact]
    public void AnActivityReceivesItsSettingsThroughItsConstructor()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddActivity<SettingsActivity, TestSettings>("test", Text("Test"), settings => settings.Number(s => s.Walks, Text("Walks"), 1, 100));

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(provider.GetServices<ActivityDefinition>());
        var settings = new TestSettings { Walks = 3 };
        var activity = Assert.IsType<SettingsActivity>(definition.Create(provider, settings));
        var byDefault = Assert.IsType<SettingsActivity>(definition.Create(provider));

        Assert.Same(settings, activity.Settings);
        Assert.Same(TimeProvider.System, activity.Time);
        Assert.Equal(10, byDefault.Settings.Walks);
        Assert.Throws<ArgumentException>(() => definition.Create(provider, null));
        Assert.Throws<ArgumentException>(() => definition.Create(provider, "settings"));
    }

    [Fact]
    public void AnActivityWithNoSettingDeclaresNone()
    {
        var definition = new ActivityDefinition("test", "Test", _ => new SettingsActivity(new TestSettings(), TimeProvider.System));

        Assert.Same(ActivitySettings.None, definition.Settings);
        Assert.True(definition.Settings.IsEmpty);
        Assert.Null(definition.Settings.CreateDefault());
        Assert.Equal("{}", definition.Settings.Write(null));
        Assert.Null(definition.Settings.Read("""{"Walks":3}""").Settings);
        Assert.Empty(definition.Settings.Check(null));
        Assert.Throws<ArgumentException>(() => definition.Create(new ServiceCollection().BuildServiceProvider(), new TestSettings()));
    }

    [Fact]
    public void AStoredListOfAnotherShapeIsReset()
    {
        var declared = Every();
        var json = new JsonObject { ["Resources"] = 303, ["SubAreas"] = new JsonArray(10, 11) }.ToJsonString();
        var read = Read(declared, json, out var reset);

        Assert.Equal(["Resources"], reset);
        Assert.Empty(read.Resources);
        Assert.Equal([TestReferences.Forest, TestReferences.Hills], read.SubAreas);
    }

    [Fact]
    public void SubAreasMayBeSuggestedByResourcesAndANumberMayBeAPercentage()
    {
        var declared = Register(settings =>
        {
            settings.Resources(s => s.Resources, Text("Resources"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources);
            settings.Number(s => s.Walks, Text("Walks"), 0, 100).Percent();
        }).Settings;

        Assert.Same(declared.All[0], declared.All[1].SuggestedBy);
        Assert.True(declared.All[2].IsPercent);
        Assert.False(declared.All[0].IsPercent);
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources)));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Resources(s => s.Resources, Text("Resources"));
            settings.Resources(s => s.SubAreas, Text("Other")).SuggestedBy(s => s.Resources);
        }));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Number(s => s.Walks, Text("Walks"), 1, 1_000).Percent()));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Flag(s => s.UseBank, Text("Bank")).Percent()));
    }

    [Fact]
    public void AnActivitySaysWhatItsSettingsLeadToAndAReckoningThatThrowsGivesNothing()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var preview = new SettingsPreview { LandmarkMapId = 7 };
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Preview((s, _) => s.Walks > 5 ? preview : null);
        }).Settings;
        var failing = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Preview((_, _) => throw new InvalidOperationException("broken"));
        }).Settings;

        Assert.True(declared.HasPreview);
        Assert.Same(preview, declared.Preview(new TestSettings { Walks = 6 }, services));
        Assert.Null(declared.Preview(new TestSettings { Walks = 3 }, services));
        Assert.Null(failing.Preview(new TestSettings(), services));
        Assert.False(Every().HasPreview);
        Assert.Null(Every().Preview(new TestSettings(), services));
    }

    [Fact]
    public void AnActivityMaySayWhatItsSettingsLeadToForEachCharacterThatPlaysThem()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Preview((_, _, players) => new SettingsPreview
            {
                PlayerNotes = [.. players.Select(player => new PreviewPlayerNote(player.Name, GameText.Of(Text("Level {0}"), GameTextValue.Number(player.Level))))],
            });
        }).Settings;

        var note = Assert.Single(declared.Preview(new TestSettings(), services, [new PreviewPlayer("Ortie", 60, new Dictionary<int, int>(), null)])!.PlayerNotes);

        Assert.Equal("Ortie", note.Name);
        Assert.Empty(declared.Preview(new TestSettings(), services)!.PlayerNotes);
    }

    [Fact]
    public void WhatAnEarlierVersionStoredUnderAKeyNoLongerDeclaredIsReadOnceTheRestIs()
    {
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Flag(s => s.UseBank, Text("Bank"));
            settings.ReadsEarlier((s, stored) =>
            {
                if (!stored.ContainsKey("UseBank") && stored["Banks"] is JsonValue banks && banks.TryGetValue(out int count))
                    s.UseBank = count > 0;
            });
        }).Settings;

        var earlier = Read(declared, """{"Walks":4,"Banks":0}""", out var reset);
        var now = Read(declared, """{"Walks":4,"UseBank":true,"Banks":0}""", out _);

        Assert.Equal(4, earlier.Walks);
        Assert.False(earlier.UseBank);
        Assert.Empty(reset);
        Assert.True(now.UseBank);
        Assert.DoesNotContain("Banks", declared.Write(earlier), StringComparison.Ordinal);
    }

    [Fact]
    public void APresetIsWrittenAsAProfileStoresItAndMustHoldWhole()
    {
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Resources(s => s.Resources, Text("Resources")).Required();
            settings.Preset(
                "many",
                Text("Many walks"),
                s =>
                {
                    s.Walks = 50;
                    s.Resources = [TestReferences.Ash];
                });
        }).Settings;

        var preset = Assert.Single(declared.Presets);

        Assert.Equal("many", preset.Key);
        Assert.Equal("Many walks", preset.Name.In(CultureInfo.InvariantCulture));
        Assert.Equal(50, Read(declared, preset.Json, out _).Walks);
        Assert.Empty(Every().Presets);
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Resources(s => s.Resources, Text("Resources")).Required();
            settings.Preset("empty", Text("Empty"), _ => { });
        }));
        Assert.Throws<ArgumentException>(() => Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100);
            settings.Preset("twice", Text("One"), _ => { });
            settings.Preset("twice", Text("Two"), _ => { });
        }));
    }

    [Fact]
    public void AProfessionSettingMayOfferThoseThatGatherWithoutAProfessionLast()
    {
        var jobs = TestReferences.Jobs();
        var gathering = new JobCatalog(jobs.Jobs, jobs.Skills.ToArray().Append(new Skill(9, 309, Job.Base, 311, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var game = new GameReferences(TestReferences.Atlas(), gathering, TestReferences.Items());
        var declared = Register(settings => settings.Jobs(s => s.Jobs, Text("Professions")).Gathering()).Settings;
        var plain = Register(settings => settings.Jobs(s => s.Jobs, Text("Professions"))).Settings;

        Assert.True(declared.All[0].IsGathering);
        Assert.Equal([TestReferences.Lumberjack, TestReferences.Miner, Job.Base], game.GatheringJobs.Select(job => job.Id));
        Assert.Empty(declared.Check(new TestSettings { Jobs = [Job.Base, TestReferences.Lumberjack] }, game));
        Assert.Equal(SettingProblemKind.Unknown, Assert.Single(declared.Check(new TestSettings { Jobs = [99] }, game)).Kind);
        Assert.Equal(SettingProblemKind.Unknown, Assert.Single(plain.Check(new TestSettings { Jobs = [Job.Base] }, game)).Kind);
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Resources(s => s.Resources, Text("Resources")).Gathering()));
    }

    [Fact]
    public void SubAreasMayBeSuggestedByMonstersAndNoneWhileTheyAreHidden()
    {
        var game = TestReferences.Game();
        var declared = Register(settings =>
        {
            settings.Monsters(s => s.Resources, Text("Monsters")).VisibleWhen(s => s.UseBank);
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources);
        }).Settings;
        var byResources = Register(settings =>
        {
            settings.Resources(s => s.Resources, Text("Resources"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources);
        }).Settings;

        Assert.Same(declared.All[0], declared.All[1].SuggestedBy);
        Assert.Equal(SettingKind.Monster, declared.All[1].SuggestedKind);
        Assert.Equal(SettingKind.Resource, byResources.All[1].SuggestedKind);
        Assert.Equal([TestReferences.Larva], declared.All[1].Suggestions(new TestSettings { Resources = [TestReferences.Larva] }, game));

        // Hidden, as when the activity fights the groups at the character's level: no monster stands for every one.
        Assert.Empty(declared.All[1].Suggestions(new TestSettings { UseBank = false, Resources = [TestReferences.Larva] }, game));
        Assert.Throws<InvalidOperationException>(() => Register(settings =>
        {
            settings.Items(s => s.Resources, Text("Items"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources);
        }));
    }

    [Fact]
    public void SubAreasMayBeSuggestedByResourcesTheActivityReckonsFromItsSettings()
    {
        var game = TestReferences.Game();
        var declared = Register(settings =>
        {
            settings.Job(s => s.Job, Text("Job"));
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy((s, game) => [.. game.Options(SettingKind.Resource, s.Job).Select(resource => resource.Id)]);
        }).Settings;
        var failing = Register(settings => settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy((_, _) => throw new InvalidOperationException("broken"))).Settings;
        var byResources = Register(settings =>
        {
            settings.Resources(s => s.Resources, Text("Resources")).VisibleWhen(s => s.UseBank);
            settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).SuggestedBy(s => s.Resources);
        }).Settings;

        Assert.True(declared.All[1].IsSuggested);
        Assert.Null(declared.All[1].SuggestedBy);
        Assert.Equal([TestReferences.Ash, TestReferences.Chestnut], declared.All[1].Suggestions(new TestSettings { Job = TestReferences.Lumberjack }, game));
        Assert.Empty(failing.All[0].Suggestions(new TestSettings(), game));
        Assert.Equal([TestReferences.Iron], byResources.All[1].Suggestions(new TestSettings { Resources = [TestReferences.Iron] }, game));
        Assert.Equal(3, byResources.All[1].Suggestions(new TestSettings { UseBank = false }, game).Count);
        Assert.Empty(Every().All[8].Suggestions(new TestSettings(), game));
    }

    [Fact]
    public void ANumberMaySayWhatItCountsAndAListOfResourcesMayLeaveThemOut()
    {
        var declared = Register(settings =>
        {
            settings.Number(s => s.Walks, Text("Walks"), 1, 100).Unit(Text("maps"));
            settings.Resources(s => s.Resources, Text("Left out")).Excluding();
        }).Settings;

        Assert.Equal("maps", declared.All[0].Unit?.In(CultureInfo.InvariantCulture));
        Assert.True(declared.All[1].IsExcluding);
        Assert.Null(Every().All[0].Unit);
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.Flag(s => s.UseBank, Text("Bank")).Unit(Text("maps"))));
        Assert.Throws<InvalidOperationException>(() => Register(settings => settings.SubAreas(s => s.SubAreas, Text("Sub-areas")).Excluding()));
    }
}
