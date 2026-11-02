using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>A race of monsters, from the client's <c>MonsterRacesDataRoot</c>, or a super-race, from its <c>MonsterSuperRacesDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="MonsterGrade"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MonsterRace
{
    /// <summary>The <see cref="SuperRaceId"/> of a super-race.</summary>
    public const int NoSuperRace = 0;

    /// <summary>Gets the race's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets its super-race, in <see cref="MonsterCatalog.SuperRaces"/>, or <see cref="NoSuperRace"/> for a super-race.</summary>
    public int SuperRaceId { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="MonsterRace"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="superRaceId">Its super-race, or <see cref="NoSuperRace"/>.</param>
    /// <param name="nameId">The text naming it.</param>
    public MonsterRace(int id, int superRaceId, int nameId)
    {
        Id = id;
        SuperRaceId = superRaceId;
        NameId = nameId;
    }
}
