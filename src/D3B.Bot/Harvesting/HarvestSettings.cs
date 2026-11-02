namespace D3B.Bot.Harvesting;

/// <summary>The harvesting activity's settings: what it harvests, where, and when it banks (D127, D165 to D167).</summary>
public sealed class HarvestSettings
{
    /// <summary>Gets or sets how the resources are chosen: by profession, or as a list (D165).</summary>
    public HarvestMode Mode { get; set; } = HarvestMode.List;

    /// <summary>Gets or sets the professions whose resources to harvest by profession, <c>Base</c>'s among them, each character's own levels deciding.</summary>
    public IReadOnlyList<int> Jobs { get; set; } = [];

    /// <summary>Gets or sets the resources left out by profession, by their items' ids.</summary>
    public IReadOnlyList<int> Excluded { get; set; } = [];

    /// <summary>Gets or sets which resource of a map is harvested first by profession.</summary>
    public HarvestOrder Order { get; set; } = HarvestOrder.Nearest;

    /// <summary>Gets or sets the resources to harvest as a list, by their items' ids, of any profession, the first harvested first on each map.</summary>
    public IReadOnlyList<int> Resources { get; set; } = [];

    /// <summary>Gets or sets how the place is chosen: by the activity, for each character's levels, or as the area and sub-areas (D166).</summary>
    public HarvestPlace Place { get; set; } = HarvestPlace.Chosen;

    /// <summary>Gets or sets whether the place the activity chooses is never above the character's level, so that its monsters do not overwhelm it.</summary>
    public bool SafeLevel { get; set; } = true;

    /// <summary>Gets or sets the area to harvest in: alone, its sub-areas where the resources grow.</summary>
    public int? Area { get; set; }

    /// <summary>Gets or sets the sub-areas to harvest in, of any area when no area is chosen.</summary>
    public IReadOnlyList<int> SubAreas { get; set; } = [];

    /// <summary>Gets or sets whether to go through the zone's isolated maps too, last, those reached only far round (D149).</summary>
    public bool IsolatedMaps { get; set; }

    /// <summary>Gets or sets how many map changes a zaap must save to be taken, on every journey (D167).</summary>
    public int ZaapSaving { get; set; } = 5;

    /// <summary>Gets or sets how full the character is, in percent of its pods, once it banks.</summary>
    public int FullAt { get; set; } = 90;

    /// <summary>Gets or sets what goes to the bank.</summary>
    public HarvestDeposit Deposit { get; set; } = HarvestDeposit.Harvest;

    /// <summary>Gets or sets where what goes to the bank is deposited (D147).</summary>
    public HarvestDepositPlace DepositTo { get; set; } = HarvestDepositPlace.OwnChest;

    /// <summary>Gets or sets whether the AI plays the fights the harvest meets; else the player does (D235).</summary>
    public bool AiFights { get; set; } = true;

    /// <summary>Gets or sets what the activity does once a fight cut one of its steps (D144).</summary>
    public HarvestAfterFight AfterFight { get; set; } = HarvestAfterFight.Resume;

    /// <summary>Gets or sets what the activity does once a fight the AI played is lost: resumed once the character regained <see cref="HealBelow"/> of its life, the owner's choice (D198).</summary>
    public HarvestAfterFight AfterLostFight { get; set; } = HarvestAfterFight.Resume;

    /// <summary>Gets or sets whether to open each resource bag as soon as a harvest gives one, so that what it holds is banked with the rest (D148).</summary>
    public bool OpenBags { get; set; }

    /// <summary>Gets or sets whether to heal under a share of life (D148).</summary>
    public bool Heal { get; set; }

    /// <summary>Gets or sets the share of its life under which the character heals, in percent: eating until its life is whole, or regenerating back to the share.</summary>
    public int HealBelow { get; set; } = 50;

    /// <summary>Gets or sets the food to eat, by item, the first carried eaten first; none chosen, what the inventory holds that only heals, the smallest heal first; with none left the life regenerates by itself up to the share.</summary>
    public IReadOnlyList<int> Food { get; set; } = [];
}
