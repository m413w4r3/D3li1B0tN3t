namespace D3B.Data.Monsters;

/// <summary>A monster's flags, the client's <c>m_flags</c>, with the bits its code gives them (client.md).</summary>
[Flags]
public enum MonsterProperties
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>As a summon, it counts among its summoner's summons.</summary>
    UseSummonSlot = 1,

    /// <summary>As a summon, it counts among its summoner's bombs.</summary>
    UseBombSlot = 2,

    /// <summary>It is a boss.</summary>
    IsBoss = 4,

    /// <summary>It is a mini-boss, an archmonster.</summary>
    IsMiniBoss = 8,

    /// <summary>The client's <c>isQuestMonster</c>.</summary>
    IsQuestMonster = 16,

    /// <summary>The client's <c>fastAnimsFun</c>.</summary>
    FastAnimsFun = 32,

    /// <summary>It plays its turns.</summary>
    CanPlay = 64,

    /// <summary>It tackles.</summary>
    CanTackle = 128,

    /// <summary>It can be pushed.</summary>
    CanBePushed = 256,

    /// <summary>It can switch position with another.</summary>
    CanSwitchPos = 512,

    /// <summary>A fighter can switch position with it.</summary>
    CanSwitchPosOnTarget = 1024,

    /// <summary>It can be carried.</summary>
    CanBeCarried = 2048,

    /// <summary>It goes through portals.</summary>
    CanUsePortal = 4096,

    /// <summary>The client's <c>allIdolsDisabled</c>.</summary>
    AllIdolsDisabled = 8192,

    /// <summary>The client's <c>useRaceValues</c>.</summary>
    UseRaceValues = 16384,

    /// <summary>Its soul cannot be captured.</summary>
    SoulCaptureForbidden = 32768,

    /// <summary>The client's <c>hideInBestiary</c>.</summary>
    HideInBestiary = 65536,

    /// <summary>It moves.</summary>
    CanMove = 131072,

    /// <summary>As a summon, the fight's owner controls it: set on 52 monsters of client 3.7.12.12.</summary>
    CanBeControlledByFightOwner = 262144,
}
