using System.Runtime.InteropServices;

namespace D3B.Data.Breeds;

/// <summary>What a point of a characteristic costs a breed from a threshold on, from the client's <c>statsPointsFor</c> lists.</summary>
/// <remarks>Each of the client's lists holds pairs of a threshold and a cost, in ascending order of threshold. Unmanaged and packed, as <see cref="Breed"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct BreedStatCost
{
    /// <summary>Gets the characteristic, by identifier: 10 strength, 11 vitality, 12 wisdom, 13 chance, 14 agility or 15 intelligence.</summary>
    public int CharacteristicId { get; }

    /// <summary>Gets the characteristic's base value from which the cost applies.</summary>
    public int Threshold { get; }

    /// <summary>Gets how many characteristic points one point costs.</summary>
    public int Cost { get; }

    /// <summary>Initializes a new instance of the <see cref="BreedStatCost"/> struct.</summary>
    /// <param name="characteristicId">The characteristic.</param>
    /// <param name="threshold">The base value from which the cost applies.</param>
    /// <param name="cost">What one point costs.</param>
    public BreedStatCost(int characteristicId, int threshold, int cost)
    {
        CharacteristicId = characteristicId;
        Threshold = threshold;
        Cost = cost;
    }
}
