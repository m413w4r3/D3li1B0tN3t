using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestingModuleTests
{
    private static ActivityDefinition Harvest()
    {
        var services = new ServiceCollection();

        HarvestingModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheOwnersListProfileOfBeforeReadsAsAListWithoutAnythingToRedo()
    {
        var settings = Harvest().Settings;

        // Stored by 6d: two resources, Amakna, the guild's chest.
        var reading = settings.Read("""{"Resources":[312,441],"EverythingKnown":false,"Area":0,"SubAreas":[],"IsolatedMaps":false,"FullAt":50,"Deposit":"Harvest","DepositTo":"GuildChest","OpenBags":true,"Heal":true,"HealBelow":80,"Food":[528],"AfterFight":"Resume"}""");
        var read = Assert.IsType<HarvestSettings>(reading.Settings);

        Assert.Empty(reading.Reset);
        Assert.Equal(HarvestMode.List, read.Mode);
        Assert.Equal([312, 441], read.Resources);
        Assert.Equal(HarvestPlace.Chosen, read.Place);
        Assert.Equal(0, read.Area);
        Assert.Equal(HarvestDepositPlace.GuildChest, read.DepositTo);
        Assert.Empty(settings.Check(read));
    }

    [Fact]
    public void AProfileThatHarvestedEverythingEachCharacterCanReadsAsEveryProfessionButBases()
    {
        var read = Assert.IsType<HarvestSettings>(Harvest().Settings.Read("""{"Resources":[],"EverythingKnown":true,"Area":1,"SubAreas":[]}""").Settings);

        Assert.Equal(HarvestMode.ByProfession, read.Mode);
        Assert.Equal(HarvestingModule.Professions, read.Jobs);
        Assert.Equal(HarvestOrder.Nearest, read.Order);
        Assert.Equal(HarvestPlace.Chosen, read.Place);
    }

    [Fact]
    public void TheHarvestGivesAnAutomaticPresetAndOnePerProfessionThatHold()
    {
        var settings = Harvest().Settings;
        var french = CultureInfo.GetCultureInfo("fr");

        Assert.Equal(["auto", "lumberjack", "miner", "alchemist", "farmer", "fisherman"], settings.Presets.Select(preset => preset.Key));
        Assert.Equal(
            ["Récolte automatique", "Bûcheron auto", "Mineur auto", "Alchimiste auto", "Paysan auto", "Pêcheur auto"],
            settings.Presets.Select(preset => preset.Name.In(french)));

        foreach (var preset in settings.Presets)
        {
            var read = Assert.IsType<HarvestSettings>(settings.Read(preset.Json).Settings);

            Assert.Equal((HarvestMode.ByProfession, HarvestOrder.HighestLevel, HarvestPlace.Automatic), (read.Mode, read.Order, read.Place));
            Assert.Empty(settings.Check(read));
        }

        Assert.Equal(HarvestingModule.Professions, Assert.IsType<HarvestSettings>(settings.Read(settings.Presets[0].Json).Settings).Jobs);
        Assert.Equal([2], Assert.IsType<HarvestSettings>(settings.Read(settings.Presets[1].Json).Settings).Jobs);
    }

    [Fact]
    public void ByProfessionAProfessionIsNeededAndAnAutomaticPlaceNeedsNoArea()
    {
        var settings = Harvest().Settings;

        Assert.Single(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Place = HarvestPlace.Automatic }));
        Assert.Empty(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2], Place = HarvestPlace.Automatic }));
        Assert.Single(settings.Check(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [2] }));
    }
}
