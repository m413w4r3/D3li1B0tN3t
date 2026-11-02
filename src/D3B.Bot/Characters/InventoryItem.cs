namespace D3B.Bot.Characters;

/// <summary>A stack of objects the character carries: an item of its inventory.</summary>
/// <param name="Uid">The stack's own id, which the requests moving it name.</param>
/// <param name="ItemId">The item's id in the client's tables (D29).</param>
/// <param name="Quantity">How many objects the stack holds.</param>
/// <param name="Position">Where the stack is: <see cref="NotWorn"/>, or where it is worn.</param>
public sealed record InventoryItem(int Uid, int ItemId, int Quantity, int Position)
{
    /// <summary>The position of a stack that is not worn, as every stack obtained or put in the bank is, in the captures.</summary>
    public const int NotWorn = 63;

    /// <summary>The position of the weapon worn: of the 75 inventories captured, the 63 holding a worn weapon held it there, and nothing else (D218).</summary>
    public const int Weapon = 1;

    /// <summary>Gets whether the stack is worn: the inventory lists the worn objects first, one of each, at other positions.</summary>
    public bool IsWorn => Position != NotWorn;
}
