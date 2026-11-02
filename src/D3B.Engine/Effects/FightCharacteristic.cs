namespace D3B.Engine.Effects;

/// <summary>The characteristics the effects' formulas read, by the ids the pack and the server give them (client.md).</summary>
public static class FightCharacteristic
{
    /// <summary>Strength, which raises neutral and earth damage.</summary>
    public const int Strength = 10;

    /// <summary>Chance, which raises water damage.</summary>
    public const int Chance = 13;

    /// <summary>Agility, which raises air damage.</summary>
    public const int Agility = 14;

    /// <summary>Intelligence, which raises fire damage and heals.</summary>
    public const int Intelligence = 15;

    /// <summary>Damage added to every blow.</summary>
    public const int AllDamageBonus = 16;

    /// <summary>Critical hits, in percent added to a level's chance of one; a monster's is often negative (D207).</summary>
    public const int CriticalHits = 18;

    /// <summary>Power, a percentage added to every element's characteristic.</summary>
    public const int Power = 25;

    /// <summary>The client's "Maîtrise d'arme": a weapon's blow raised by it, in percent, once its bonuses are added.</summary>
    public const int WeaponMastery = 31;

    /// <summary>Heals added to every heal.</summary>
    public const int HealBonus = 49;

    /// <summary>Earth's resistance in percent; fire, water, air and neutral follow, 34 to 37.</summary>
    public const int EarthResistPercent = 33;

    /// <summary>Earth's fixed resistance; fire, water, air and neutral follow, 55 to 58.</summary>
    public const int EarthReduction = 54;

    /// <summary>Erosion: the share of a loss of life lost for the rest of the fight, in percent.</summary>
    public const int Erosion = 75;

    /// <summary>Damage added to a push's.</summary>
    public const int PushDamageBonus = 84;

    /// <summary>A push's damage taken off.</summary>
    public const int PushDamageReduction = 85;

    /// <summary>Damage added to a critical hit.</summary>
    public const int CriticalDamageBonus = 86;

    /// <summary>Earth's damage bonus; fire, water, air and neutral follow, 89 to 92.</summary>
    public const int EarthDamageBonus = 88;

    /// <summary>The client's "Bonus de combo de bombe": what an explosion's damage is raised by, in percent (9f3).</summary>
    public const int BombComboBonus = 94;

    /// <summary>The shield's points.</summary>
    public const int Shield = 96;

    /// <summary>The client's "Bonus de puissance pour les armes": a percentage added to a weapon's blow as power is.</summary>
    public const int WeaponPower = 103;

    /// <summary>The damage dealt, in percent of itself: 100 when nothing changes it.</summary>
    public const int DealtDamageMultiplier = 107;

    /// <summary>The damage dealt from a distance, in percent of itself.</summary>
    public const int DealtDamageMultiplierDistance = 120;

    /// <summary>The damage received from a distance, in percent of itself.</summary>
    public const int ReceivedDamageMultiplierDistance = 121;

    /// <summary>The damage dealt by weapons, in percent of itself.</summary>
    public const int DealtDamageMultiplierWeapons = 122;

    /// <summary>The damage dealt by spells, in percent of itself.</summary>
    public const int DealtDamageMultiplierSpells = 123;

    /// <summary>The damage received in melee, in percent of itself.</summary>
    public const int ReceivedDamageMultiplierMelee = 124;

    /// <summary>The damage dealt in melee, in percent of itself.</summary>
    public const int DealtDamageMultiplierMelee = 125;

    /// <summary>The damage received from spells, in percent of itself.</summary>
    public const int ReceivedDamageMultiplierSpells = 141;

    /// <summary>The damage received from weapons, in percent of itself.</summary>
    public const int ReceivedDamageMultiplierWeapons = 142;

    /// <summary>The heals dealt, in percent of themselves.</summary>
    public const int DealtHealMultiplier = 143;
}
