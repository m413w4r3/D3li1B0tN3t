using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Characters;

/// <summary>The whole values of the characteristics the server gives, in a fight and out of one alike.</summary>
public static class CharacteristicTotals
{
    /// <summary>Gets a characteristic's whole value: the sum of its numbers, whichever way the server gave it.</summary>
    /// <param name="characteristic">The characteristic.</param>
    /// <returns>Its value, or <see langword="null"/> when the server gave none.</returns>
    public static long? Of(Characteristic characteristic)
    {
        ArgumentNullException.ThrowIfNull(characteristic);

        return characteristic.KindCase switch
        {
            Characteristic.KindOneofCase.None => null,
            Characteristic.KindOneofCase.Usable => (long)characteristic.Usable.Number1 + characteristic.Usable.Number2 + characteristic.Usable.Number3 + characteristic.Usable.Number4 + characteristic.Usable.Number5 + characteristic.Usable.Number6,
            Characteristic.KindOneofCase.Value => characteristic.Value.Total,
            Characteristic.KindOneofCase.Detailed => (long)characteristic.Detailed.Number1 + characteristic.Detailed.Number2 + characteristic.Detailed.Number3 + characteristic.Detailed.Number4 + characteristic.Detailed.Number5 + characteristic.Detailed.Number7,
            _ => 0,
        };
    }

    /// <summary>Gets the whole values of characteristics, by id, the last one given winning.</summary>
    /// <param name="characteristics">The characteristics.</param>
    /// <returns>Each one's value, those without one left out.</returns>
    public static ImmutableSortedDictionary<int, int> Of(IEnumerable<Characteristic> characteristics)
    {
        ArgumentNullException.ThrowIfNull(characteristics);

        var values = ImmutableSortedDictionary.CreateBuilder<int, int>();

        foreach (var characteristic in characteristics)
        {
            if (Of(characteristic) is { } value)
                values[characteristic.Id] = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        }

        return values.ToImmutable();
    }
}
