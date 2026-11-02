using System.Globalization;
using D3B.Engine.Criteria;

namespace D3B.Engine.Fights;

/// <summary>Which fighters an effect reaches, its target mask as the client writes it, such as <c>a,A</c> or <c>A,*E7020</c>, parsed once.</summary>
/// <remarks>
/// <para>
/// Measured in the pack (client.md): tokens joined by commas, 71 kinds once their numbers are set aside. A mask first
/// includes fighters by kind, an uppercase letter for the caster's enemies and a lowercase one for its allies, then asks
/// conditions that must all hold, the caster's own led by <c>*</c>. The kinds and conditions read as the references read
/// them (D181), their kinds as Dofus 3 names its fighters:
/// </para>
/// <list type="table">
/// <item><term><c>a</c>, <c>A</c></term><description>Every ally, the caster among them; every enemy. <c>g</c>: every ally but the caster.</description></item>
/// <item><term><c>c</c>, <c>C</c></term><description>The caster in the zone; the caster wherever it stands.</description></item>
/// <item><term><c>h</c>, <c>H</c>; <c>l</c>, <c>L</c></term><description>Characters, no summon; characters and sidekicks.</description></item>
/// <item><term><c>m</c>, <c>M</c></term><description>Monsters, no summon.</description></item>
/// <item><term><c>j</c>, <c>J</c>; <c>i</c>, <c>I</c>; <c>s</c>, <c>S</c></term><description>Summons; those that move; those that do not.</description></item>
/// <item><term><c>d</c>, <c>D</c></term><description>Sidekicks.</description></item>
/// <item><term><c>E</c>, <c>e</c></term><description>In a state, not in it.</description></item>
/// <item><term><c>F</c>, <c>f</c>; <c>B</c>, <c>b</c>; <c>Z</c>, <c>z</c></term><description>A monster, a breed, a sidekick, or not: several of one
/// letter are alternatives.</description></item>
/// <item><term><c>V</c>, <c>v</c></term><description>Life under a share of the most, in percent, or not.</description></item>
/// <item><term><c>P</c>, <c>p</c></term><description>The caster, its summons, its summoner and their other summons, or not.</description></item>
/// <item><term><c>Q</c>, <c>q</c></term><description>As many summons as it may have, or fewer.</description></item>
/// </list>
/// <para>
/// A caster's condition on its kind, such as <c>*h,*m</c>, asks one of them. What the fight's state does not tell, such as
/// a fighter carried (<c>K</c>), a portal (<c>R</c>), a teleport (<c>T</c>, <c>W</c>), a fighter appearing (<c>U</c>),
/// the triggering target (<c>O</c>) or the tokens Dofus 3 added (<c>Atq</c>, <c>Def</c>, <c>Sce</c>, <c>PB</c>, <c>PR</c>,
/// <c>AP</c>, <c>MP</c>, <c>u</c>, <c>x</c>), answers <see cref="CriterionAnswer.Unknown"/>, as an unknown criterion does
/// (D71).
/// </para>
/// </remarks>
public sealed class TargetMask
{
    private const string Kinds = "aAgcChHlLmMjJiIsSdD";
    private const string Conditions = "EeFfBbZzVvPpQq";
    private const string Alternatives = "FfBbZz";

    private readonly Token[] _kinds;
    private readonly Token[][] _groups;
    private readonly bool _includesUnknown;

    /// <summary>Gets the mask's text, as the client wrote it.</summary>
    public string Text { get; }

    /// <summary>Gets whether the mask names the caster wherever it stands, <c>C</c>; <c>c</c> names it in the zone only.</summary>
    /// <remarks>
    /// Measured (D195): 16 buffs of such masks went to a caster outside its effect's zone. Of the 32 casts captured of an
    /// effect of mask <c>c</c> without <c>C</c>, the caster outside its zone, such as a Zobal's Tortoruga of mask
    /// <c>l,m,c</c> on an enemy, none buffed the caster (D226), as the references read <c>C</c> alone outside the zone.
    /// </remarks>
    public bool NamesTheCaster { get; }

    private TargetMask(string text, Token[] tokens)
    {
        Text = text;

        // A search asks a mask for every fighter of every cast it weighs (D200): what it reads is grouped once here.
        _kinds = [.. tokens.Where(token => token.Kind is TokenKind.Kind)];
        _groups = [.. tokens.Select((token, index) => (Token: token, Index: index))
            .Where(pair => pair.Token.Kind is not TokenKind.Kind)
            .GroupBy(pair => Alternative(pair.Token, pair.Index))
            .Select(group => group.Select(pair => pair.Token).ToArray())];
        _includesUnknown = tokens.Any(token => token.Kind is TokenKind.UnknownKind);
        NamesTheCaster = _kinds.Any(token => token.Letter is 'C');
    }

    /// <summary>Reads a mask.</summary>
    /// <param name="text">The mask, as the client writes it.</param>
    /// <returns>The mask: a token it does not know is kept, and answers unknown.</returns>
    public static TargetMask Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<Token>();

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            tokens.Add(Read(part));

        return new TargetMask(text, [.. tokens]);
    }

    private static Token Read(string part)
    {
        var onCaster = part[0] is '*';
        var body = onCaster ? part[1..] : part;

        if (body.Length is 0)
            return new Token(TokenKind.UnknownKind, onCaster, ' ', 0);

        var letter = body[0];
        var hasValue = int.TryParse(body.AsSpan(1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value);
        var bare = body.Length is 1;

        if (Conditions.Contains(letter, StringComparison.Ordinal) && (hasValue || (bare && letter is 'P' or 'p' or 'Q' or 'q')))
            return new Token(TokenKind.Condition, onCaster, letter, value);

        if (bare && Kinds.Contains(letter, StringComparison.Ordinal))
            return new Token(onCaster ? TokenKind.CasterKind : TokenKind.Kind, onCaster, letter, 0);

        // A condition the fight's state does not tell yet answers unknown; a token of none of the references' kinds, such as
        // those Dofus 3 added, may include fighters, so that a mask of it alone is unknown too.
        return new Token(IsUntold(body) || onCaster ? TokenKind.Untold : TokenKind.UnknownKind, onCaster, letter, 0);
    }

    private static bool IsUntold(string body)
    {
        var measured = body.Length > 2 && char.IsAsciiDigit(body[2]) && body[..2] is "AP" or "ap" or "MP" or "mp";

        return measured || body is "K" or "O" or "o" or "R" or "r" or "T" or "U" or "W" or "PB" or "pb" or "PR" or "pr";
    }

    private static (bool OnCaster, char Letter, int Index) Alternative(Token token, int index)
    {
        // Alternatives answer together: a monster among several, a breed among several, the caster's kinds.
        if (token.Kind is TokenKind.CasterKind)
            return (true, '*', -1);

        return Alternatives.Contains(token.Letter, StringComparison.Ordinal) && token.Kind is TokenKind.Condition
            ? (token.OnCaster, token.Letter, -1)
            : (token.OnCaster, token.Letter, index);
    }

    private static CriterionAnswer Any(Token[] group, Combatant caster, Combatant target, IReadOnlyCollection<Combatant> fighters)
    {
        var result = CriterionAnswer.False;

        foreach (var token in group)
        {
            var answer = Condition(token, caster, target, fighters);

            if (answer is CriterionAnswer.True)
                return answer;

            if (answer is CriterionAnswer.Unknown)
                result = answer;
        }

        return result;
    }

    private static CriterionAnswer Of(bool met)
    {
        return met ? CriterionAnswer.True : CriterionAnswer.False;
    }

    private static bool IsKind(char letter, Combatant fighter)
    {
        return char.ToLowerInvariant(letter) switch
        {
            'h' => fighter.IsCharacter,
            'l' => fighter.IsCharacter,
            'm' => !fighter.IsSummon && fighter.MonsterId is not null,
            'j' => fighter.IsSummon,
            'i' => fighter.IsSummon && !fighter.IsStatic,
            's' => fighter.IsStatic,
            'd' => false,
            _ => true,
        };
    }

    private static bool IsFamily(Combatant one, Combatant other)
    {
        return one.Id == other.Id
            || other.SummonerId == one.Id
            || one.SummonerId == other.Id
            || (one.SummonerId is { } summoner && other.SummonerId == summoner);
    }

    private static CriterionAnswer Summons(Combatant fighter, IReadOnlyCollection<Combatant> fighters, bool full)
    {
        if (fighter.MaxSummons is not { } most)
            return CriterionAnswer.Unknown;

        var count = 0;

        foreach (var other in fighters)
        {
            if (other.IsAlive && other.SummonerId == fighter.Id && !other.IsStatic)
                count++;
        }

        return Of(full ? count >= most : count < most);
    }

    private static CriterionAnswer Condition(Token token, Combatant caster, Combatant target, IReadOnlyCollection<Combatant> fighters)
    {
        if (token.Kind is TokenKind.Untold or TokenKind.UnknownKind)
            return CriterionAnswer.Unknown;

        if (token.Kind is TokenKind.CasterKind)
            return Of(token.Letter is 'c' or 'C' or 'a' or 'A' or 'g' || IsKind(token.Letter, caster));

        var fighter = token.OnCaster ? caster : target;

        return token.Letter switch
        {
            'E' => Of(fighter.HasState(token.Value)),
            'e' => Of(!fighter.HasState(token.Value)),
            'F' => Of(fighter.MonsterId == token.Value),
            'f' => Of(fighter.MonsterId != token.Value),
            'B' => Of(fighter.BreedId == token.Value),
            'b' => Of(fighter.BreedId != token.Value),
            'Z' => CriterionAnswer.False,
            'z' => CriterionAnswer.True,
            'V' => fighter.MaxLife > 0 ? Of(fighter.Life * 100L < (long)token.Value * fighter.MaxLife) : CriterionAnswer.Unknown,
            'v' => fighter.MaxLife > 0 ? Of(fighter.Life * 100L >= (long)token.Value * fighter.MaxLife) : CriterionAnswer.Unknown,
            'P' => Of(IsFamily(caster, target)),
            'p' => Of(!IsFamily(caster, target)),
            'Q' => Summons(fighter, fighters, full: true),
            'q' => Summons(fighter, fighters, full: false),
            _ => CriterionAnswer.Unknown,
        };
    }

    /// <summary>Tells whether an effect cast by a fighter reaches another.</summary>
    /// <param name="caster">The caster.</param>
    /// <param name="target">A fighter in the effect's zone.</param>
    /// <param name="fighters">Every fighter of the fight, for the conditions on summons.</param>
    /// <returns>Whether the mask selects it, or unknown when it asks what the fight's state does not tell.</returns>
    public CriterionAnswer Selects(Combatant caster, Combatant target, IReadOnlyCollection<Combatant> fighters)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(fighters);

        var answer = Included(caster, target);

        if (answer is CriterionAnswer.False)
            return answer;

        foreach (var group in _groups)
        {
            var met = Any(group, caster, target, fighters);

            if (met is CriterionAnswer.False)
                return met;

            if (met is CriterionAnswer.Unknown)
                answer = CriterionAnswer.Unknown;
        }

        return answer;
    }

    private CriterionAnswer Included(Combatant caster, Combatant target)
    {
        if (target.Id == caster.Id)
        {
            foreach (var token in _kinds)
            {
                if (token.Letter is 'a' or 'c' or 'C')
                    return CriterionAnswer.True;
            }

            return CriterionAnswer.False;
        }

        var ally = target.Team == caster.Team;

        foreach (var token in _kinds)
        {
            if (token.Letter is 'c' or 'C')
                continue;

            if (char.IsUpper(token.Letter) != ally && IsKind(token.Letter, target))
                return CriterionAnswer.True;
        }

        return _includesUnknown ? CriterionAnswer.Unknown : CriterionAnswer.False;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Text;
    }

    private enum TokenKind
    {
        Kind,
        CasterKind,
        Condition,
        Untold,
        UnknownKind,
    }

    private readonly record struct Token(TokenKind Kind, bool OnCaster, char Letter, int Value);
}
