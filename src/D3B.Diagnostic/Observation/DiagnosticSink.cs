using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Reporting;
using Microsoft.Extensions.Logging;

namespace D3B.Diagnostic.Observation;

/// <summary>Creates bounded, isolated diagnostic sessions using one exported build schema.</summary>
public sealed class DiagnosticSink : IDiagnosticSessionFactory
{
    private readonly SidecarOptions _options;
    private readonly DiagnosticSchema _schema;
    private readonly IDiagnosticSidecarFactory _sidecarFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DiagnosticReportOptions? _reportOptions;
    private readonly IDiagnosticReportWriter? _reportWriter;
    private readonly TimeProvider? _timeProvider;
    private readonly bool _includeReadable;

    /// <summary>Creates the per-connection diagnostic session factory.</summary>
    public DiagnosticSink(
        SidecarOptions options,
        DiagnosticSchema schema,
        IDiagnosticSidecarFactory sidecarFactory,
        ILoggerFactory loggerFactory,
        DiagnosticReportOptions? reportOptions = null,
        IDiagnosticReportWriter? reportWriter = null,
        TimeProvider? timeProvider = null,
        bool includeReadable = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(sidecarFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options;
        _schema = schema;
        _sidecarFactory = sidecarFactory;
        _loggerFactory = loggerFactory;
        _reportOptions = reportOptions;
        _reportWriter = reportWriter;
        _timeProvider = timeProvider;
        _includeReadable = includeReadable;
    }

    /// <inheritdoc/>
    public IDiagnosticSink CreateSession()
    {
        return new DiagnosticSession(
            _options,
            _schema,
            _sidecarFactory,
            _loggerFactory.CreateLogger<DiagnosticSession>(),
            _reportOptions,
            _reportWriter,
            _timeProvider,
            _includeReadable);
    }
}
