namespace D3B.Network.Relay;

/// <summary>Controls the admission snapshot for a relay connection's owning game session.</summary>
internal interface IRelayAdmissionModeController
{
    /// <summary>Sets the session version and whether untyped frames are denied.</summary>
    /// <param name="version">The monotonically increasing session admission version.</param>
    /// <param name="requiresTypedAdmission">Whether frames must carry a typed admission predicate.</param>
    void SetAdmissionMode(long version, bool requiresTypedAdmission);
}
