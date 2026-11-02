namespace D3B.Engine.Criteria;

/// <summary>A criterion in the client's syntax, such as <c>(Qa=610|Qf=610)</c> or <c>PO&gt;31644,0</c>, parsed once, then answered on what a bot knows.</summary>
/// <remarks>
/// <para>
/// Measured on the world graph's 619 criteria (client.md): conditions joined by <c>&amp;</c> and <c>|</c>, grouped by
/// parentheses. Each condition is a two-letter code, an operator and a value, its arguments separated by commas, at times
/// after a context in brackets, <c>[413]MC=0</c>. A group may be led by <c>#</c>, 22 times, before lists of cells; it
/// does not change what the group asks. No group of this build mixes <c>&amp;</c> and <c>|</c>, so precedence decides
/// nothing; <c>&amp;</c> binds tighter, as is usual.
/// </para>
/// <para>
/// Answers are on three values (<see cref="CriterionAnswer"/>): a group of <c>&amp;</c> fails as soon as one condition
/// fails, and is met only when all are; a group of <c>|</c> is met as soon as one is, and fails only when all fail; what
/// remains is unknown. A condition the bot cannot answer is unknown (<see cref="CriterionCodes"/>), and so is a criterion
/// that cannot be read, or a condition with a context, whose meaning is not known.
/// </para>
/// </remarks>
public sealed class Criterion
{
    private readonly Node? _root;

    /// <summary>Gets the criterion's text, as the client wrote it.</summary>
    public string Text { get; }

    /// <summary>Gets whether the text could be read: one that cannot always answers <see cref="CriterionAnswer.Unknown"/>.</summary>
    public bool IsRead => _root is not null;

    private Criterion(string text, Node? root)
    {
        Text = text;
        _root = root;
    }

    /// <summary>Reads a criterion.</summary>
    /// <param name="text">The criterion, in the client's syntax.</param>
    /// <returns>The criterion, read or not (<see cref="IsRead"/>): a text that cannot be read is no error, only unknown.</returns>
    public static Criterion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parser = new Parser(text);
        var root = parser.ReadAny();

        return new Criterion(text, parser.IsAtEnd ? root : null);
    }

    /// <summary>Answers the criterion on what the bot knows.</summary>
    /// <param name="facts">What the bot knows.</param>
    /// <returns>Whether it is met, fails, or cannot be told.</returns>
    public CriterionAnswer Evaluate(CriterionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return _root?.Evaluate(facts) ?? CriterionAnswer.Unknown;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Text;
    }

    private abstract class Node
    {
        public abstract CriterionAnswer Evaluate(CriterionFacts facts);
    }

    private sealed class All : Node
    {
        private readonly Node[] _nodes;

        public All(Node[] nodes)
        {
            _nodes = nodes;
        }

        public override CriterionAnswer Evaluate(CriterionFacts facts)
        {
            var answer = CriterionAnswer.True;

            foreach (var node in _nodes)
            {
                switch (node.Evaluate(facts))
                {
                    case CriterionAnswer.False:
                        return CriterionAnswer.False;
                    case CriterionAnswer.Unknown:
                        answer = CriterionAnswer.Unknown;
                        break;
                }
            }

            return answer;
        }
    }

    private sealed class Any : Node
    {
        private readonly Node[] _nodes;

        public Any(Node[] nodes)
        {
            _nodes = nodes;
        }

        public override CriterionAnswer Evaluate(CriterionFacts facts)
        {
            var answer = CriterionAnswer.False;

            foreach (var node in _nodes)
            {
                switch (node.Evaluate(facts))
                {
                    case CriterionAnswer.True:
                        return CriterionAnswer.True;
                    case CriterionAnswer.Unknown:
                        answer = CriterionAnswer.Unknown;
                        break;
                }
            }

            return answer;
        }
    }

    private sealed class Condition : Node
    {
        private readonly string _code;
        private readonly char _comparison;
        private readonly string[] _arguments;

        public Condition(string code, char comparison, string[] arguments)
        {
            _code = code;
            _comparison = comparison;
            _arguments = arguments;
        }

        public override CriterionAnswer Evaluate(CriterionFacts facts)
        {
            return CriterionCodes.Answer(facts, _code, _comparison, _arguments);
        }
    }

    private sealed class Unread : Node
    {
        public static Unread Instance { get; } = new();

        public override CriterionAnswer Evaluate(CriterionFacts facts)
        {
            return CriterionAnswer.Unknown;
        }
    }

    private ref struct Parser
    {
        private readonly string _text;
        private int _position;
        private bool _failed;

        public readonly bool IsAtEnd => !_failed && _position == _text.Length;

        public Parser(string text)
        {
            _text = text;
        }

        public Node ReadAny()
        {
            var nodes = new List<Node> { ReadAll() };

            while (TrySkip('|'))
                nodes.Add(ReadAll());

            return nodes.Count is 1 ? nodes[0] : new Any([.. nodes]);
        }

        private Node ReadAll()
        {
            var nodes = new List<Node> { ReadOne() };

            while (TrySkip('&'))
                nodes.Add(ReadOne());

            return nodes.Count is 1 ? nodes[0] : new All([.. nodes]);
        }

        private Node ReadOne()
        {
            TrySkip('#');

            if (!TrySkip('('))
                return ReadCondition();

            var group = ReadAny();

            if (!TrySkip(')'))
                _failed = true;

            return group;
        }

        private Node ReadCondition()
        {
            var start = _position;

            while (_position < _text.Length && _text[_position] is not ('&' or '|' or '(' or ')'))
                _position++;

            var condition = _text.AsSpan(start, _position - start).Trim();

            // A context in brackets, such as [413]MC=0, gives the condition a meaning no reference explains.
            if (condition.Length > 0 && condition[0] is '[')
                return Unread.Instance;

            if (condition.Length < 4 || !char.IsAsciiLetter(condition[0]) || !char.IsAsciiLetter(condition[1]))
            {
                _failed = true;

                return Unread.Instance;
            }

            return new Condition(condition[..2].ToString(), condition[2], condition[3..].ToString().Split(','));
        }

        private bool TrySkip(char expected)
        {
            if (_failed || _position >= _text.Length || _text[_position] != expected)
                return false;

            _position++;

            return true;
        }
    }
}
