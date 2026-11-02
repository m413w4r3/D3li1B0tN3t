using System.Globalization;
using D3B.Data.Maps;

namespace D3B.Engine.Criteria;

/// <summary>The conditions a bot answers, by their two-letter code, and the others it does not know.</summary>
/// <remarks>
/// <para>
/// The client names 47 codes in the world graph's criteria (client.md), most of them quests and the server's own. A bot
/// answers those its state holds, read as the client's criteria classes name them, as the references read them: their
/// meaning is not measured on the server. Every other code, an operator other than <c>=</c>, <c>!</c>, <c>&gt;</c> and
/// <c>&lt;</c>, or a value that is not a number, answers <see cref="CriterionAnswer.Unknown"/>.
/// </para>
/// <list type="table">
/// <item><term><c>PL</c></term><description>The character's level.</description></item>
/// <item><term><c>PO</c></term><description>An object held: <c>PO=item</c> at least one, <c>PO!item</c> none, <c>PO&gt;item,n</c> more than n.</description></item>
/// <item><term><c>PJ</c>, <c>Pj</c></term><description>A profession: <c>PJ=job</c> had, <c>Pj&gt;job,level</c> above a level.</description></item>
/// <item><term><c>PZ</c></term><description>The account subscribed, 1, or not, 0.</description></item>
/// <item><term><c>Pm</c>, <c>PB</c>, <c>Po</c></term><description>The map, sub-area or area the character acts on.</description></item>
/// <item><term><c>PC</c></term><description>The cell it acts from (<see cref="CriterionFacts.Cell"/>).</description></item>
/// <item><term><c>HS</c></term><description>A fighter's state: <c>HS=state</c> in it, <c>HS!state</c> not; the only code of the spell levels'
/// criteria (client.md).</description></item>
/// </list>
/// </remarks>
internal static class CriterionCodes
{
    public static CriterionAnswer Answer(CriterionFacts facts, string code, char comparison, IReadOnlyList<string> arguments)
    {
        return code switch
        {
            "PL" => Compare(facts.Level, comparison, arguments),
            "PO" => Held(facts.Items, comparison, arguments),
            "PJ" or "Pj" => Held(facts.JobLevels, comparison, arguments),
            "PZ" => Compare(facts.Subscribed is { } subscribed ? (subscribed ? 1 : 0) : null, comparison, arguments),
            "Pm" => Compare(facts.MapId, comparison, arguments),
            "PB" => Compare(facts.SubAreaId, comparison, arguments),
            "Po" => Compare(facts.AreaId, comparison, arguments),
            "PC" => facts.Cell is { } cell ? Compare(cell, comparison, arguments) : AnyCell(comparison, arguments),
            "HS" => InState(facts.States, comparison, arguments),
            _ => CriterionAnswer.Unknown,
        };
    }

    private static CriterionAnswer Compare(long? known, char comparison, IReadOnlyList<string> arguments)
    {
        if (known is not { } value || arguments.Count is not 1 || !TryParse(arguments[0], out var wanted))
            return CriterionAnswer.Unknown;

        return Compare(value, comparison, wanted);
    }

    private static CriterionAnswer Compare(long value, char comparison, long wanted)
    {
        return comparison switch
        {
            '=' => Of(value == wanted),
            '!' => Of(value != wanted),
            '>' => Of(value > wanted),
            '<' => Of(value < wanted),
            _ => CriterionAnswer.Unknown,
        };
    }

    private static CriterionAnswer Held(IReadOnlyDictionary<int, int>? counts, char comparison, IReadOnlyList<string> arguments)
    {
        if (counts is null || arguments.Count is not (1 or 2) || !TryParse(arguments[0], out var id) || id is < int.MinValue or > int.MaxValue)
            return CriterionAnswer.Unknown;

        var count = counts.GetValueOrDefault((int)id);

        if (arguments.Count is 2)
            return TryParse(arguments[1], out var wanted) ? Compare(count, comparison, wanted) : CriterionAnswer.Unknown;

        // Alone, the id asks whether the character has any.
        return comparison switch
        {
            '=' => Of(count > 0),
            '!' => Of(count is 0),
            _ => CriterionAnswer.Unknown,
        };
    }

    private static CriterionAnswer InState(IReadOnlyCollection<int>? states, char comparison, IReadOnlyList<string> arguments)
    {
        if (states is null || arguments.Count is not 1 || !TryParse(arguments[0], out var state))
            return CriterionAnswer.Unknown;

        return comparison switch
        {
            '=' => Of(states.Contains((int)state)),
            '!' => Of(!states.Contains((int)state)),
            _ => CriterionAnswer.Unknown,
        };
    }

    private static CriterionAnswer AnyCell(char comparison, IReadOnlyList<string> arguments)
    {
        // The cell is chosen later: a condition some cell of a map meets counts as met.
        if (arguments.Count is not 1 || !TryParse(arguments[0], out var cell))
            return CriterionAnswer.Unknown;

        var met = comparison switch
        {
            '=' => cell is >= 0 and < MapData.CellCount,
            '!' => true,
            '>' => cell < MapData.CellCount - 1,
            '<' => cell > 0,
            _ => (bool?)null,
        };

        return met is { } value ? Of(value) : CriterionAnswer.Unknown;
    }

    private static bool TryParse(string text, out long value)
    {
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static CriterionAnswer Of(bool met)
    {
        return met ? CriterionAnswer.True : CriterionAnswer.False;
    }
}
