using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Bot.Tests.Network;

/// <summary>Game frames as a build's client and server send them, their messages named by the test's obfuscation, and the translation reading them.</summary>
internal static class TestGame
{
    private static readonly Obfuscation _obfuscation = new(ReadableProtocol.Files);

    public static ProtocolTranslation Translation()
    {
        var (schema, mapping) = TestInstall.Protocol();

        return ProtocolTranslation.Create(ProtocolSchema.Load(schema), ProtocolMapping.Parser.ParseFrom(mapping));
    }

    /// <summary>The type URL of a message, as the build names it.</summary>
    public static string TypeUrl(IMessage message)
    {
        return "type.ankama.com/" + _obfuscation.Of(message.Descriptor.FullName);
    }

    /// <summary>A frame the client sends: a request, with the id every request of the first milestone carries.</summary>
    public static byte[] Request(IMessage content)
    {
        return new GameMessage { Request = new GameRequest { Id = -1, Content = Any(content) } }.ToByteArray();
    }

    /// <summary>A frame the server sends: an event.</summary>
    public static byte[] Event(IMessage content)
    {
        return new GameMessage { Event = new GameEvent { Content = Any(content) } }.ToByteArray();
    }

    /// <summary>A frame the server sends rarely: a response.</summary>
    public static byte[] Response(IMessage content)
    {
        return new GameMessage { Response = new GameResponse { Id = -1, Content = Any(content) } }.ToByteArray();
    }

    private static Any Any(IMessage content)
    {
        return new Any { TypeUrl = TypeUrl(content), Value = content.ToByteString() };
    }
}
