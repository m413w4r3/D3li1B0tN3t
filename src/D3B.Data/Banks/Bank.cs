using System.Runtime.InteropServices;

namespace D3B.Data.Banks;

/// <summary>A bank, as the world map shows it: one of the client's <c>HintsDataRoot</c> hints.</summary>
/// <remarks>
/// The hint stands on the map in front of the bank. Its door, an interactive transition of the world graph, leads to the
/// bank's inside, at the same coordinates, where a banker stands; which of those maps holds him, the server tells
/// (client.md). Unmanaged and packed, as <see cref="Banker"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Bank
{
    /// <summary>Gets the identifier of the client's hint.</summary>
    public int Id { get; }

    /// <summary>Gets the map in front of the bank.</summary>
    public long MapId { get; }

    /// <summary>Initializes a new instance of the <see cref="Bank"/> struct.</summary>
    /// <param name="id">The identifier of the client's hint.</param>
    /// <param name="mapId">The map in front of the bank.</param>
    public Bank(int id, long mapId)
    {
        Id = id;
        MapId = mapId;
    }
}
