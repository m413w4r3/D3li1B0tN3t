using D3B.App.Localization;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Settings;
using D3B.Data.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

public sealed class GatheringJobsFieldTests
{
    private static (SettingsEditor Editor, GatheringJobsFieldViewModel Field, TestSettings Draft) Edit(TestSettings draft)
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>("test", LocalizedText.Invariant("Test"), settings =>
        {
            settings.Jobs(s => s.Jobs, LocalizedText.Invariant("Professions")).Gathering();
            settings.Rule(s => s.Jobs.Count > 0, LocalizedText.Invariant("Choose one."));
        });

        var definition = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        var jobs = TestReferences.Jobs();
        var gathering = new JobCatalog(jobs.Jobs, jobs.Skills.ToArray().Append(new Skill(9, 309, Job.Base, 311, 1, 1, false)).OrderBy(skill => skill.Id).ToArray());
        var game = new GameReferences(TestReferences.Atlas(), gathering, TestReferences.Items());
        var localizer = new Localizer(Localizer.English);
        var editor = new SettingsEditor(definition.Settings, draft, game, new GameNames(null, localizer), localizer);

        return (editor, Assert.IsType<GatheringJobsFieldViewModel>(Assert.Single(editor.Sections.SelectMany(section => section.Fields))), draft);
    }

    [Fact]
    public void AllTheProfessionsComeFirstThenEachThenNoProfessionLast()
    {
        var (_, field, _) = Edit(new TestSettings { Jobs = [TestReferences.Miner] });

        Assert.Equal("All", field.Chips[0].Name);
        Assert.Equal("No profession", field.Chips[^1].Name);
        Assert.True(field.Chips[^1].IsBase);
        Assert.Equal([null, TestReferences.Lumberjack, TestReferences.Miner, Job.Base], field.Chips.Select(chip => chip.Id));
        Assert.Equal([false, false, true, false], field.Chips.Select(chip => chip.IsSelected));
        Assert.True(field.IsWide);
    }

    [Fact]
    public void AllTicksEveryProfessionThenUnticksThemAndNoProfessionStaysApart()
    {
        var (editor, field, draft) = Edit(new TestSettings { Jobs = [Job.Base] });

        field.Chips[0].ChooseCommand.Execute(null);

        Assert.Equal([TestReferences.Lumberjack, TestReferences.Miner, Job.Base], draft.Jobs);
        Assert.True(field.Chips[0].IsSelected);
        Assert.False(editor.HasProblems);

        field.Chips[0].ChooseCommand.Execute(null);

        Assert.Equal([Job.Base], draft.Jobs);
        Assert.False(field.Chips[0].IsSelected);
    }

    [Fact]
    public void AProfessionIsTickedOrUntickedAloneInTheGamesOrder()
    {
        var (editor, field, draft) = Edit(new TestSettings());

        Assert.True(editor.HasProblems);

        field.Chips[3].ChooseCommand.Execute(null);
        field.Chips[2].ChooseCommand.Execute(null);
        field.Chips[1].ChooseCommand.Execute(null);

        Assert.Equal([TestReferences.Lumberjack, TestReferences.Miner, Job.Base], draft.Jobs);
        Assert.True(field.Chips[0].IsSelected);

        field.Chips[2].ChooseCommand.Execute(null);

        Assert.Equal([TestReferences.Lumberjack, Job.Base], draft.Jobs);
        Assert.False(field.Chips[0].IsSelected);
        Assert.False(editor.HasProblems);
    }
}
