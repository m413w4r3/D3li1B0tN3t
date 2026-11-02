using D3B.Bot.Characters;
using D3B.Data.Items;

namespace D3B.Bot.Inventory;

/// <summary>Which stack of the inventory a character eats to heal (D148, D256).</summary>
public static class Food
{
    /// <summary>Finds the stack to eat: the food a profile chose, else what the inventory holds that only heals.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="chosen">The food the profile chose, by item, the first carried eaten first.</param>
    /// <param name="items">The pack's items, which say what heals.</param>
    /// <param name="unusable">The stacks a use failed on, never eaten again.</param>
    /// <returns>The stack, not worn, or <see langword="null"/> when there is nothing to eat.</returns>
    /// <remarks>
    /// The pack keeps the life an item heals only when its use does nothing else, so that a potion changing a class or a
    /// bread launching a fight is never eaten for its life. The smallest heal goes first: the server eats only as much of a
    /// stack as the life lacks (D156), and the small heals are the cheap foods.
    /// </remarks>
    public static InventoryItem? StackToEat(CharacterState state, IReadOnlyList<int> chosen, ItemCatalog items, IReadOnlySet<int> unusable)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(unusable);

        var stacks = state.Inventory.Values.Where(stack => !stack.IsWorn && !unusable.Contains(stack.Uid));

        if (chosen.Count > 0)
            return chosen.Select(item => stacks.FirstOrDefault(stack => stack.ItemId == item)).FirstOrDefault(stack => stack is not null);

        return stacks
            .Select(stack => (Stack: stack, Found: items.TryGetItem(stack.ItemId, out var item), Item: item))
            .Where(candidate => candidate.Found && candidate.Item.Heals)
            .OrderBy(candidate => candidate.Item.HealMax)
            .ThenBy(candidate => candidate.Item.HealMin)
            .ThenBy(candidate => candidate.Stack.ItemId)
            .ThenBy(candidate => candidate.Stack.Uid)
            .Select(candidate => candidate.Stack)
            .FirstOrDefault();
    }
}
