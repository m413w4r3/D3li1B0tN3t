using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Fighting;

/// <summary>Fighting monsters, the activity made as harvesting is (D236): the groups a profile chooses, found and attacked through a zone, each fight played by the AI (D244).</summary>
/// <remarks>Registers <see cref="FightActivity"/> as <c>fight</c>, with a preset, "Automatic fight", which D3B stores once as a profile (D168), and <see cref="FightPreviews"/>, which the app's profile editor asks (D245). Requires the bots module.</remarks>
public sealed class FightingModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "fight";

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services, which hold the installed build's world and tables.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<FightPreviews>();
        services.AddActivity<FightActivity, FightSettings>(ActivityId, FightTexts.Text("Name"), Describe);
    }

    private static void Describe(ActivitySettingsBuilder<FightSettings> settings)
    {
        settings.Section(FightTexts.Text("SectionWhat"));
        settings.Choice(s => s.Mode, FightTexts.Text("Mode"), mode => FightTexts.Text($"Mode{mode}")).Help(FightTexts.Text("ModeHelp"));
        settings.Monsters(s => s.Monsters, FightTexts.Text("Monsters"))
            .Help(FightTexts.Text("MonstersHelp"))
            .VisibleWhen(s => s.Mode is FightMode.Chosen);
        settings.MonsterFamilies(s => s.Families, FightTexts.Text("Families"))
            .Help(FightTexts.Text("FamiliesHelp"))
            .VisibleWhen(s => s.Mode is FightMode.Chosen);
        settings.Monsters(s => s.Excluded, FightTexts.Text("Excluded"))
            .Excluding()
            .Help(FightTexts.Text("ExcludedHelp"));
        settings.Number(s => s.LeastMonsters, FightTexts.Text("LeastMonsters"), 1, 8).Unit(FightTexts.Text("MonstersUnit"));
        settings.Number(s => s.MostMonsters, FightTexts.Text("MostMonsters"), 1, 8).Unit(FightTexts.Text("MonstersUnit"));
        settings.Flag(s => s.OnlyWinnable, FightTexts.Text("OnlyWinnable")).Help(FightTexts.Text("OnlyWinnableHelp"));

        settings.Section(FightTexts.Text("SectionWhere"));
        settings.Choice(s => s.Place, FightTexts.Text("Place"), place => FightTexts.Text($"Place{place}")).Help(FightTexts.Text("PlaceHelp"));
        settings.Area(s => s.Area, FightTexts.Text("Area"))
            .Help(FightTexts.Text("AreaHelp"))
            .VisibleWhen(s => s.Place is FightPlace.Chosen);
        settings.SubAreas(s => s.SubAreas, FightTexts.Text("SubAreas"))
            .Within(s => s.Area)
            .SuggestedByMonsters(ChosenIn)
            .Help(FightTexts.Text("SubAreasHelp"))
            .VisibleWhen(s => s.Place is FightPlace.Chosen);
        settings.Flag(s => s.IsolatedMaps, FightTexts.Text("IsolatedMaps")).Help(FightTexts.Text("IsolatedMapsHelp"));
        settings.Number(s => s.ZaapSaving, FightTexts.Text("ZaapSaving"), 1, 30).Unit(FightTexts.Text("ZaapSavingUnit"));

        settings.Section(FightTexts.Text("SectionUpkeep"));
        settings.Flag(s => s.Heal, FightTexts.Text("Heal"));
        settings.Number(s => s.HealBelow, FightTexts.Text("HealBelow"), 10, 95)
            .Percent()
            .VisibleWhen(s => s.Heal);
        settings.Items(s => s.Food, FightTexts.Text("Food"))
            .Ordered()
            .OfItemTypes([.. HarvestActivity.FoodTypes])
            .Help(FightTexts.Text("FoodHelp"))
            .VisibleWhen(s => s.Heal);

        settings.Section(FightTexts.Text("SectionBank"));
        settings.Number(s => s.FullAt, FightTexts.Text("FullAt"), 50, 100).Percent();
        settings.Choice(s => s.Deposit, FightTexts.Text("Deposit"), deposit => FightTexts.Text($"Deposit{deposit}"));

        settings.Section(FightTexts.Text("SectionAfter"));
        settings.Choice(s => s.AfterLostFight, FightTexts.Text("AfterLostFight"), after => FightTexts.Text($"AfterLostFight{after}"))
            .Help(FightTexts.Text("AfterLostFightHelp"));

        settings.Rule(s => s.Mode is not FightMode.Chosen || ChosenMonsters.Any(s), FightTexts.Text("RuleMonsters"));
        settings.Rule(s => s.Place is FightPlace.Automatic || s.Area is not null || s.SubAreas.Count > 0, FightTexts.Text("RuleWhere"));
        settings.Rule(s => s.LeastMonsters <= s.MostMonsters, FightTexts.Text("RuleSize"));
        settings.Preview((s, services, players) => services.GetRequiredService<FightPreviews>().Of(s, players));

        settings.Preset("auto", FightTexts.Text("PresetAuto"), _ => { });
    }

    private static IReadOnlyList<int> ChosenIn(FightSettings settings, GameReferences game)
    {
        // The monsters chosen and those of the families chosen, whose sub-areas come first; none while the groups are the character's level's.
        if (settings.Mode is not FightMode.Chosen)
            return [];

        return [.. settings.Monsters.Concat(settings.Families.SelectMany(game.MonstersOfFamily)).Distinct()];
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }
}
