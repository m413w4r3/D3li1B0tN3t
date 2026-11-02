namespace D3B.Scripts.Extract;

/// <summary>The two layouts the client stores maps in (client.md).</summary>
internal enum ClientMapLayout
{
    /// <summary><c>AleCore.Data.UnityClient.ClientMapData</c>: 15,983 maps of client 3.7.12.12, every one the world graph knows.</summary>
    ClientMapData,

    /// <summary><c>Core.World.Metadata.Maps.MapMetadata</c>: 1,739 maps, in 26 bundles of their own, none in the graph.</summary>
    MapMetadata,
}
