using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>A characteristic's ratio for a monster, from the client's <c>characRatios</c>: what its characteristic's formula scales by.</summary>
/// <remarks>Unmanaged and packed, as <see cref="MonsterGrade"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MonsterRatio
{
    /// <summary>Gets the characteristic, by identifier.</summary>
    public int CharacteristicId { get; }

    /// <summary>Gets the ratio.</summary>
    public float Ratio { get; }

    /// <summary>Initializes a new instance of the <see cref="MonsterRatio"/> struct.</summary>
    /// <param name="characteristicId">The characteristic.</param>
    /// <param name="ratio">The ratio.</param>
    public MonsterRatio(int characteristicId, float ratio)
    {
        CharacteristicId = characteristicId;
        Ratio = ratio;
    }
}
