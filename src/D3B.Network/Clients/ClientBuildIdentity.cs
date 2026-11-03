namespace D3B.Network.Clients;

/// <summary>The build a client's version file names: the version and build date a host was prepared for.</summary>
/// <param name="Version">The client's version, such as <c>3.7.13.17</c>.</param>
/// <param name="BuildDate">When the client was built, when its version file says so.</param>
public readonly record struct ClientBuildIdentity(string Version, DateTimeOffset? BuildDate);
