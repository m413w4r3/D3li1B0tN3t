using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using Microsoft.Extensions.Logging;

namespace D3B.Diagnostic.Observation;

/// <summary>Creates bounded, isolated diagnostic sessions using one exported build schema.</summary>
public sealed class DiagnosticSink : IDiagnosticSessionFactory
{
    private readonly SidecarOptions _options;
    private readonly DiagnosticSchema _schema;
    private readonly IDiagnosticSidecarFactory _sidecarFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Creates the per-connection diagnostic session factory.</summary>
    public DiagnosticSink(
        SidecarOptions options,
        DiagnosticSchema schema,
        IDiagnosticSidecarFactory sidecarFactory,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(sidecarFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options;
        _schema = schema;
        _sidecarFactory = sidecarFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IDiagnosticSink CreateSession()
    {
        return new DiagnosticSession(_options, _schema, _sidecarFactory, _loggerFactory.CreateLogger<DiagnosticSession>());
    }
}
