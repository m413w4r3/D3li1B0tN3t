using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Bot.Inspection;

/// <summary>What the protocol inspector shows of a frame unfolded: a readable message's fields, the ticket hidden, or the shape of one the readable protocol lacks (D115).</summary>
/// <remarks>
/// <para>
/// Fields are those the message holds, as protobuf's JSON leaves out a value at its default: lists and messages set, scalars
/// not zero or empty. An enumeration's value is its name; bytes are their length, never their content.
/// </para>
/// <para>
/// The account's ticket, which the game connection's first request presents (D52), is never shown: the inspector shows
/// that it is there, hidden, live as in a replay, as no log names it (D65).
/// </para>
/// <para>
/// A message the readable protocol lacks, or that does not read, is shown by its shape alone: its fields' numbers, wire
/// types and lengths, never their values, as <c>scripts/capture.cs --frames</c> shows them (D37).
/// </para>
/// </remarks>
public static class FrameFields
{
    /// <summary>What a secret's value shows as.</summary>
    public const string Hidden = "••••••••";

    /// <summary>The most values of a list of scalars shown on its line; the others are counted.</summary>
    public const int MostListed = 32;

    private static readonly FrozenSet<string> _secrets = FrozenSet.ToFrozenSet(["d3b.game.AuthenticationRequest.ticket"], StringComparer.Ordinal);

    /// <summary>Gets the fields a readable message holds, at every depth.</summary>
    /// <param name="message">The message.</param>
    /// <returns>Its fields in their numbers' order, each message's own fields after it.</returns>
    public static IReadOnlyList<FrameField> Of(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var fields = new List<FrameField>();

        Add(fields, message, 0);

        return fields;
    }

    /// <summary>Gets the shape of the message a frame carries: its fields' numbers, wire types and lengths.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="shape">The fields, as far as the bytes read as fields.</param>
    /// <returns>Whether the frame carries a message, and its bytes read whole as fields.</returns>
    public static bool TryShape(DecodedGameFrame frame, out IReadOnlyList<WireField> shape)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var any = frame.Envelope?.ContentCase switch
        {
            GameMessage.ContentOneofCase.Request => frame.Envelope.Request.Content,
            GameMessage.ContentOneofCase.Response => frame.Envelope.Response.Content,
            GameMessage.ContentOneofCase.Event => frame.Envelope.Event.Content,
            _ => null,
        };

        if (any is null)
        {
            shape = [];

            return false;
        }

        var read = WireReading.TryFields(any.Value.Span, out var fields);

        shape = fields;

        return read;
    }

    private static void Add(List<FrameField> fields, IMessage message, int depth)
    {
        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            var value = field.Accessor.GetValue(message);
            var secret = _secrets.Contains(field.FullName);

            if (field.IsMap)
            {
                foreach (DictionaryEntry entry in (IDictionary)value)
                    AddValue(fields, field.MessageType.FindFieldByNumber(2), $"{field.JsonName}[{entry.Key}]", entry.Value, depth, secret);
            }
            else if (field.IsRepeated)
            {
                AddList(fields, field, (IList)value, depth, secret);
            }
            else if (field.HasPresence ? field.Accessor.HasValue(message) : !IsDefault(value))
            {
                AddValue(fields, field, field.JsonName, value, depth, secret);
            }
        }
    }

    private static void AddList(List<FrameField> fields, FieldDescriptor field, IList list, int depth, bool secret)
    {
        if (list.Count is 0)
            return;

        if (field.FieldType is FieldType.Message)
        {
            for (var index = 0; index < list.Count; index++)
                AddValue(fields, field, $"{field.JsonName}[{index}]", list[index], depth, secret);

            return;
        }

        var shown = list.Cast<object?>().Take(MostListed).Select(item => Text(field, item));
        var more = list.Count > MostListed ? $", … {list.Count - MostListed} more" : "";

        fields.Add(secret ? new FrameField(depth, field.JsonName, Hidden, true) : new FrameField(depth, field.JsonName, $"[{string.Join(", ", shown)}{more}]", false));
    }

    private static void AddValue(List<FrameField> fields, FieldDescriptor field, string name, object? value, int depth, bool secret)
    {
        if (secret)
        {
            fields.Add(new FrameField(depth, name, Hidden, true));

            return;
        }

        if (field.FieldType is FieldType.Message && value is IMessage nested)
        {
            // An Any is shown by the message it names: the game's names are the build's, obfuscated (D56).
            if (nested is Any any)
            {
                fields.Add(new FrameField(depth, name, $"{Any.GetTypeName(any.TypeUrl)}, {any.Value.Length} bytes", false));

                return;
            }

            fields.Add(new FrameField(depth, name, null, false));
            Add(fields, nested, depth + 1);

            return;
        }

        fields.Add(new FrameField(depth, name, Text(field, value), false));
    }

    private static string Text(FieldDescriptor field, object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            ByteString bytes => $"{bytes.Length} bytes",
            bool flag => flag ? "true" : "false",
            System.Enum or int when field.FieldType is FieldType.Enum => field.EnumType.FindValueByNumber(Convert.ToInt32(value, CultureInfo.InvariantCulture))?.Name
                ?? Convert.ToString(value, CultureInfo.InvariantCulture)!,
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static bool IsDefault(object? value)
    {
        return value switch
        {
            null => true,
            string text => text.Length is 0,
            ByteString bytes => bytes.IsEmpty,
            bool flag => !flag,
            System.Enum enumeration => Convert.ToInt64(enumeration, CultureInfo.InvariantCulture) is 0,
            int number => number is 0,
            long number => number is 0,
            uint number => number is 0,
            ulong number => number is 0,
            float number => number is 0,
            double number => number is 0,
            _ => false,
        };
    }
}
