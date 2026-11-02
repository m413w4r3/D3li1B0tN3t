namespace D3B.Protocol.Mapping;

/// <summary>What auditing a mapping found: failures, which reject it, and notes, which do not.</summary>
public sealed class AuditReport
{
    /// <summary>Gets what does not hold: a readable type or field without a counterpart, or one that does not fit it.</summary>
    public IReadOnlyList<string> Failures { get; }

    /// <summary>Gets what the build has that the readable protocol does not read, which a reader meets as unknown fields.</summary>
    /// <remarks>What a partial message leaves out on purpose is in <see cref="Unread"/> instead: a note is what a build added.</remarks>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Gets, for each partial readable message, how many of its counterpart's fields it leaves out on purpose.</summary>
    public IReadOnlyList<string> Unread { get; }

    /// <summary>Gets the number of readable messages checked.</summary>
    public int Messages { get; }

    /// <summary>Gets the number of readable fields checked.</summary>
    public int Fields { get; }

    /// <summary>Gets the number of readable enums checked.</summary>
    public int Enums { get; }

    /// <summary>Gets the number of readable enum values checked.</summary>
    public int Values { get; }

    /// <summary>Gets whether the mapping holds: no failure.</summary>
    public bool Passed => Failures.Count is 0;

    internal AuditReport(IReadOnlyList<string> failures, IReadOnlyList<string> notes, IReadOnlyList<string> unread, int messages, int fields, int enums, int values)
    {
        Failures = failures;
        Notes = notes;
        Unread = unread;
        Messages = messages;
        Fields = fields;
        Enums = enums;
        Values = values;
    }
}
