using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Interactives;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Data.Packs;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3B.Bot.Harvesting;

/// <summary>Harvesting and banking, the first milestone (D16, D127): the activity the app starts, with its settings and their preview.</summary>
/// <remarks>
/// Registers <see cref="HarvestActivity"/> as <c>harvest</c>, <see cref="HarvestSightings"/>, which every bot of the host
/// shares (D128), and <see cref="HarvestPreviews"/>, which the app's profile editor asks. Requires the bots module. Gives
/// an automatic harvest and one by profession, which D3B stores once as profiles (D168).
/// </remarks>
public sealed class HarvestingModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "harvest";

    /// <summary>Gets the game's professions that gather, but <c>Base</c>: lumberjack, miner, alchemist, farmer and fisherman, by id, as the pack of 3.7.12.12 numbers them.</summary>
    /// <remarks>What "everything each character can harvest" meant before D165, and what the presets harvest (D168).</remarks>
    public static IReadOnlyList<int> Professions { get; } = [2, 24, 26, 28, 36];

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services, which hold the installed build's world and tables.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The bots module teaches it; a test's host without one gets it empty.
        services.TryAddSingleton<GraphicSkills>();
        services.TryAddSingleton(services => new GraphicPlaces(services.GetRequiredService<GamePack>(), services.GetRequiredService<GameWorld>()));
        services.AddSingleton<HarvestSightings>();

        // Whatever the bots do, every map they are on teaches how long its resources take to grow back (D145).
        services.TryAddSingleton<Regrowths>();
        services.AddSingleton<IGameObserver>(services => new RegrowthLearner(services.GetRequiredService<Regrowths>(), services.GetRequiredService<GameTables>(), services.GetRequiredService<TimeProvider>()));
        services.AddSingleton<HarvestPreviews>();
        services.AddActivity<HarvestActivity, HarvestSettings>(ActivityId, HarvestTexts.Text("Name"), Describe);
    }

    private static void Describe(ActivitySettingsBuilder<HarvestSettings> settings)
    {
        settings.Section(HarvestTexts.Text("SectionWhat"));
        settings.Choice(s => s.Mode, HarvestTexts.Text("Mode"), mode => HarvestTexts.Text($"Mode{mode}")).Help(HarvestTexts.Text("ModeHelp"));
        settings.Jobs(s => s.Jobs, HarvestTexts.Text("Jobs"))
            .Gathering()
            .Help(HarvestTexts.Text("JobsHelp"))
            .VisibleWhen(s => s.Mode is HarvestMode.ByProfession);
        settings.Resources(s => s.Excluded, HarvestTexts.Text("Excluded"))
            .Excluding()
            .Help(HarvestTexts.Text("ExcludedHelp"))
            .VisibleWhen(s => s.Mode is HarvestMode.ByProfession);
        settings.Choice(s => s.Order, HarvestTexts.Text("Order"), order => HarvestTexts.Text($"Order{order}"))
            .Help(HarvestTexts.Text("OrderHelp"))
            .VisibleWhen(s => s.Mode is HarvestMode.ByProfession);
        settings.Resources(s => s.Resources, HarvestTexts.Text("Resources"))
            .Ordered()
            .Help(HarvestTexts.Text("ResourcesHelp"))
            .VisibleWhen(s => s.Mode is HarvestMode.List);

        settings.Section(HarvestTexts.Text("SectionWhere"));
        settings.Choice(s => s.Place, HarvestTexts.Text("Place"), place => HarvestTexts.Text($"Place{place}")).Help(HarvestTexts.Text("PlaceHelp"));
        settings.Flag(s => s.SafeLevel, HarvestTexts.Text("SafeLevel"))
            .Help(HarvestTexts.Text("SafeLevelHelp"))
            .VisibleWhen(s => s.Place is HarvestPlace.Automatic);
        settings.Area(s => s.Area, HarvestTexts.Text("Area"))
            .Help(HarvestTexts.Text("AreaHelp"))
            .VisibleWhen(s => s.Place is HarvestPlace.Chosen);
        settings.SubAreas(s => s.SubAreas, HarvestTexts.Text("SubAreas"))
            .Within(s => s.Area)
            .SuggestedBy(Wanted)
            .Help(HarvestTexts.Text("SubAreasHelp"))
            .VisibleWhen(s => s.Place is HarvestPlace.Chosen);
        settings.Flag(s => s.IsolatedMaps, HarvestTexts.Text("IsolatedMaps")).Help(HarvestTexts.Text("IsolatedMapsHelp"));
        settings.Number(s => s.ZaapSaving, HarvestTexts.Text("ZaapSaving"), 1, 30)
            .Unit(HarvestTexts.Text("ZaapSavingUnit"))
            .Help(HarvestTexts.Text("ZaapSavingHelp"));

        settings.Section(HarvestTexts.Text("SectionBank"));
        settings.Number(s => s.FullAt, HarvestTexts.Text("FullAt"), 50, 100).Percent();
        settings.Choice(s => s.Deposit, HarvestTexts.Text("Deposit"), deposit => HarvestTexts.Text(deposit is HarvestDeposit.Harvest ? "DepositHarvest" : "DepositAll"));

        settings.Choice(s => s.DepositTo, HarvestTexts.Text("DepositTo"), place => HarvestTexts.Text($"DepositTo{place}"))
            .Help(HarvestTexts.Text("DepositToHelp"));

        settings.Section(HarvestTexts.Text("SectionUpkeep"));
        settings.Flag(s => s.OpenBags, HarvestTexts.Text("OpenBags")).Help(HarvestTexts.Text("OpenBagsHelp"));
        settings.Flag(s => s.Heal, HarvestTexts.Text("Heal"));
        settings.Number(s => s.HealBelow, HarvestTexts.Text("HealBelow"), 10, 95)
            .Percent()
            .VisibleWhen(s => s.Heal);
        settings.Items(s => s.Food, HarvestTexts.Text("Food"))
            .Ordered()
            .OfItemTypes([.. HarvestActivity.FoodTypes])
            .Help(HarvestTexts.Text("FoodHelp"))
            .VisibleWhen(s => s.Heal);

        settings.Section(HarvestTexts.Text("SectionFight"));
        settings.Flag(s => s.AiFights, HarvestTexts.Text("AiFights")).Help(HarvestTexts.Text("AiFightsHelp"));
        settings.Choice(s => s.AfterFight, HarvestTexts.Text("AfterFight"), after => HarvestTexts.Text($"AfterFight{after}"))
            .Help(HarvestTexts.Text("AfterFightHelp"));
        settings.Choice(s => s.AfterLostFight, HarvestTexts.Text("AfterLostFight"), after => HarvestTexts.Text($"AfterLostFight{after}"))
            .Help(HarvestTexts.Text("AfterLostFightHelp"))
            .VisibleWhen(s => s.AiFights);

        settings.Rule(s => s.Mode is not HarvestMode.ByProfession || s.Jobs.Count > 0, HarvestTexts.Text("RuleJobs"));
        settings.Rule(s => s.Mode is not HarvestMode.List || s.Resources.Count > 0, HarvestTexts.Text("RuleWhat"));
        settings.Rule(s => s.Place is HarvestPlace.Automatic || s.Area is not null || s.SubAreas.Count > 0, HarvestTexts.Text("RuleWhere"));
        settings.Preview((s, services, players) => services.GetRequiredService<HarvestPreviews>().Of(s, players));
        settings.ReadsEarlier(ReadEarlier);

        settings.Preset("auto", HarvestTexts.Text("PresetAuto"), s => Automatic(s, Professions));
        settings.Preset("lumberjack", HarvestTexts.Text("PresetLumberjack"), s => Automatic(s, [2]));
        settings.Preset("miner", HarvestTexts.Text("PresetMiner"), s => Automatic(s, [24]));
        settings.Preset("alchemist", HarvestTexts.Text("PresetAlchemist"), s => Automatic(s, [26]));
        settings.Preset("farmer", HarvestTexts.Text("PresetFarmer"), s => Automatic(s, [28]));
        settings.Preset("fisherman", HarvestTexts.Text("PresetFisherman"), s => Automatic(s, [36]));
    }

    private static IReadOnlyList<int> Wanted(HarvestSettings settings, GameReferences game)
    {
        if (settings.Mode is HarvestMode.List)
            return settings.Resources;

        var excluded = settings.Excluded.ToHashSet();
        var resources = settings.Jobs.SelectMany(job => job == Job.Base
            ? game.Options(SettingKind.Resource).Where(resource => game.IsBase(resource.Id))
            : game.Options(SettingKind.Resource, job));

        return [.. resources.Select(resource => resource.Id).Where(resource => !excluded.Contains(resource)).Distinct()];
    }

    // Before D165, "everything each character can harvest" was a flag, every profession but Base's: a list stays a list.
    private static void ReadEarlier(HarvestSettings settings, JsonObject stored)
    {
        if (!stored.ContainsKey(nameof(HarvestSettings.Mode))
            && stored.TryGetPropertyValue("EverythingKnown", out var node)
            && node is JsonValue value
            && value.TryGetValue(out bool everything)
            && everything)
        {
            settings.Mode = HarvestMode.ByProfession;
            settings.Jobs = Professions;
        }
    }

    private static void Automatic(HarvestSettings settings, IReadOnlyList<int> jobs)
    {
        settings.Mode = HarvestMode.ByProfession;
        settings.Jobs = jobs;
        settings.Order = HarvestOrder.HighestLevel;
        settings.Place = HarvestPlace.Automatic;
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }
}
