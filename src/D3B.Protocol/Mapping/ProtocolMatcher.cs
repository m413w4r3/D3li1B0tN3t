using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>Pairs every readable message and enum with its counterpart in a client build's obfuscated schema (D11, D43).</summary>
/// <remarks>
/// Evidence is taken strongest first: the build's pinned pairs, then the anchors its captures give, then the fields of
/// every pair, which pair their types, then structural scores for what is left, each clear of every other candidate
/// both ways; every pair a score finds is followed through its fields again. A readable package is matched within the
/// schema's file its scope names only.
/// </remarks>
public sealed class ProtocolMatcher
{
    /// <summary>The lowest structural score a pair is accepted at.</summary>
    public const double MinimumScore = 0.8;

    /// <summary>How far a pair's score must stand above the next candidate's, on both sides.</summary>
    public const double MinimumMargin = 0.1;

    private readonly IReadOnlyList<FileDescriptor> _readable;
    private readonly ProtocolSchema _schema;
    private readonly IReadOnlyDictionary<string, string> _scopes;

    /// <summary>Initializes a new instance of the <see cref="ProtocolMatcher"/> class.</summary>
    /// <param name="readable">The readable protocol's files.</param>
    /// <param name="schema">The build's schema.</param>
    /// <param name="scopes">For each readable package, the schema's file its messages are in.</param>
    public ProtocolMatcher(IReadOnlyList<FileDescriptor> readable, ProtocolSchema schema, IReadOnlyDictionary<string, string> scopes)
    {
        ArgumentNullException.ThrowIfNull(readable);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(scopes);

        _readable = readable;
        _schema = schema;
        _scopes = scopes;
    }

    /// <summary>Accepts the pairs whose score clears the minimum, and every other candidate by the margin, both ways.</summary>
    private static bool Accept<T>(List<T> readable, List<T> obfuscated, Func<T, T, double> score, Func<T, T, double, bool> pair)
        where T : class
    {
        if (readable.Count is 0 || obfuscated.Count is 0)
            return false;

        var scores = new double[readable.Count, obfuscated.Count];

        for (var row = 0; row < readable.Count; row++)
        {
            for (var column = 0; column < obfuscated.Count; column++)
                scores[row, column] = score(readable[row], obfuscated[column]);
        }

        var accepted = false;

        for (var row = 0; row < readable.Count; row++)
        {
            var (column, best, rowNext) = Best(obfuscated.Count, index => scores[row, index]);
            var (bestRow, _, columnNext) = Best(readable.Count, index => scores[index, column]);

            if (bestRow != row || !Clears(best, rowNext) || !Clears(best, columnNext))
                continue;

            accepted |= pair(readable[row], obfuscated[column], best);
        }

        return accepted;
    }

    /// <summary>Tells whether a score clears the minimum, and the next candidate's by the margin.</summary>
    /// <remarks>Scores are shares of fields, so 1.0 − 0.9 must count as 0.1, which in doubles it falls short of.</remarks>
    internal static bool Clears(double best, double next)
    {
        const double Tolerance = 1e-9;

        return best >= MinimumScore - Tolerance && best - next >= MinimumMargin - Tolerance;
    }

    private static (int Index, double Best, double Next) Best(int count, Func<int, double> score)
    {
        var index = -1;
        var best = double.NegativeInfinity;
        var next = 0.0;

        for (var i = 0; i < count; i++)
        {
            var current = score(i);

            if (current > best)
            {
                next = index < 0 ? 0 : best;
                best = current;
                index = i;
            }
            else if (current > next)
            {
                next = current;
            }
        }

        return (index, best, next);
    }

    /// <summary>Matches the readable protocol with the schema.</summary>
    /// <param name="pins">The build's pinned pairs, or null.</param>
    /// <param name="anchors">The anchors measured in the build's captures.</param>
    public MatchResult Match(PinnedPairs? pins, IReadOnlyList<Anchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var state = new MatchState();
        var messages = _readable.SelectMany(file => Descriptors.Messages(file.MessageTypes)).ToList();
        var enums = _readable.SelectMany(Descriptors.Enums).ToList();

        foreach (var file in _readable.Where(file => !_scopes.ContainsKey(file.Package)))
            state.Conflict($"{file.Name}: no file of the schema is named for package {file.Package}.");

        if (pins is not null)
            ApplyPins(state, pins, messages, enums);

        foreach (var anchor in anchors)
        {
            state.Pair(anchor.Readable, anchor.Obfuscated, MatchEvidence.Capture);

            foreach (var (readable, obfuscated) in anchor.Fields)
                state.StateField(anchor.Readable, readable, obfuscated, MatchEvidence.Capture);
        }

        state.Propagate();

        while (Score(state, messages, enums))
            state.Propagate();

        var messageMatches = messages.Where(message => state.CounterpartOf(message) is not null)
            .Select(message =>
            {
                var (evidence, score) = state.EvidenceOf(message);

                return new MessageMatch(message, state.CounterpartOf(message)!, evidence, score, state.FieldsOf(message));
            })
            .OrderBy(match => match.Readable.FullName, StringComparer.Ordinal)
            .ToList();
        var enumMatches = enums.Where(type => state.CounterpartOf(type) is not null)
            .Select(type =>
            {
                var (evidence, score) = state.EvidenceOf(type);

                return new EnumMatch(type, state.CounterpartOf(type)!, evidence, score);
            })
            .OrderBy(match => match.Readable.FullName, StringComparer.Ordinal)
            .ToList();
        var unpaired = messages.Where(message => state.CounterpartOf(message) is null).Select(message => message.FullName)
            .Concat(enums.Where(type => state.CounterpartOf(type) is null).Select(type => type.FullName))
            .Order(StringComparer.Ordinal)
            .ToList();

        return new MatchResult(messageMatches, enumMatches, unpaired, state.Conflicts);
    }

    private void ApplyPins(MatchState state, PinnedPairs pins, List<MessageDescriptor> messages, List<EnumDescriptor> enums)
    {
        foreach (var pin in pins.Messages)
        {
            var readable = messages.FirstOrDefault(message => message.FullName == pin.Readable);
            var obfuscated = _schema.FindMessage(pin.Obfuscated);

            if (readable is null || obfuscated is null)
            {
                state.Conflict(readable is null
                    ? $"A pin names {pin.Readable}, which the readable protocol lacks."
                    : $"A pin names {pin.Obfuscated}, which the schema lacks.");

                continue;
            }

            state.Pair(readable, obfuscated, MatchEvidence.Pin);

            foreach (var field in pin.Fields)
                state.StateField(readable, field.Readable, field.Obfuscated, MatchEvidence.Pin);
        }

        foreach (var pin in pins.Enums)
        {
            var readable = enums.FirstOrDefault(type => type.FullName == pin.Readable);
            var obfuscated = _schema.FindEnum(pin.Obfuscated);

            if (readable is null || obfuscated is null)
            {
                state.Conflict(readable is null
                    ? $"A pin names {pin.Readable}, which the readable protocol lacks."
                    : $"A pin names {pin.Obfuscated}, which the schema lacks.");

                continue;
            }

            state.Pair(readable, obfuscated, MatchEvidence.Pin);
        }
    }

    private bool Score(MatchState state, List<MessageDescriptor> messages, List<EnumDescriptor> enums)
    {
        var accepted = false;

        // Packages matched in one file compete for its types together: each must clear every other's candidates too. A
        // scope naming a package none of the readable files is in has nothing to match.
        var packages = _readable.Select(file => file.Package).ToHashSet(StringComparer.Ordinal);

        foreach (var scope in _scopes.Where(scope => packages.Contains(scope.Key)).GroupBy(scope => scope.Value, StringComparer.Ordinal))
        {
            var file = _schema.FindFile(scope.Key);
            var scoped = scope.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);

            if (file is null)
            {
                state.Conflict($"The schema has no file {scope.Key}, which {string.Join(" and ", scoped.Order(StringComparer.Ordinal))} are matched in.");

                continue;
            }

            var readableMessages = messages.Where(message => scoped.Contains(message.File.Package) && state.CounterpartOf(message) is null && !Descriptors.IsMapEntry(message)).ToList();
            var obfuscatedMessages = Descriptors.Messages(file.MessageTypes).Where(message => state.ReadableOf(message) is null && !Descriptors.IsMapEntry(message)).ToList();

            accepted |= Accept(
                readableMessages,
                obfuscatedMessages,
                (readable, obfuscated) => StructuralScore.Message(readable, obfuscated, state),
                (readable, obfuscated, score) => state.Pair(readable, obfuscated, MatchEvidence.Score, score));

            var readableEnums = enums.Where(type => scoped.Contains(type.File.Package) && state.CounterpartOf(type) is null).ToList();
            var obfuscatedEnums = Descriptors.Enums(file).Where(type => state.ReadableOf(type) is null).ToList();

            accepted |= Accept(
                readableEnums,
                obfuscatedEnums,
                StructuralScore.Enum,
                (readable, obfuscated, score) => state.Pair(readable, obfuscated, MatchEvidence.Score, score));
        }

        return accepted;
    }
}
