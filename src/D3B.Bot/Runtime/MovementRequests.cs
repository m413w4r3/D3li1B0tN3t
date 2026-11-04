using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Runtime;

internal static class MovementRequests
{
    public static bool IsMovementCapable(IMessage request)
    {
        return request is MapMovementRequest
            or MapChangeRequest
            or TeleportRequest
            or HavenBagEnterRequest
            or InteractiveUseRequest
            or NpcActionRequest
            or NpcDialogReplyRequest
            or ObjectUseRequest
            or ObjectUseMultipleRequest;
    }
}
