using D3B.Bot.Network;
using D3B.Bot.Protocol;
using D3B.Core.Modules;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Schema;
using D3B.Protocol.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Diagnostics;

/// <summary>Registers optional diagnostic observation for each game connection.</summary>
public sealed partial class DiagnosticModule : IModule
{
    private const string EnabledKey = "Diagnostic:Enabled";
    private const string EnginePathKey = "Diagnostic:EnginePath";
    private const string SocketDirectoryKey = "Diagnostic:SocketDirectory";
    private const string QueueCapacityKey = "Diagnostic:QueueCapacity";
    private const string QueueByteCapacityKey = "Diagnostic:QueueByteCapacity";
    private const string IncludeReadableKey = "Diagnostic:IncludeReadable";
    private const string ReportEnabledKey = "Diagnostic:ReportEnabled";
    private const string ReportDirectoryKey = "Diagnostic:ReportDirectory";
    private const string ReportMaxBytesKey = "Diagnostic:ReportMaxBytes";

    /// <inheritdoc />
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);
        if (!bot.Configuration.GetValue<bool>(EnabledKey))
        {
            return;
        }

        bot.Services.TryAddSingleton<IDiagnosticSidecarFactory, DiagnosticSidecarFactory>();
        if (bot.Configuration.GetValue<bool>(ReportEnabledKey))
        {
            bot.Services.TryAddSingleton<IDiagnosticReportWriter>(services =>
            {
                DiagnosticReportOptions options = CreateReportOptions(bot.Configuration);
                string directory = ResolveReportDirectory(bot.Configuration);
                return new AtomicDiagnosticReportWriter(
                    options,
                    directory,
                    services.GetService<TimeProvider>() ?? TimeProvider.System);
            });
        }

        bot.Services.AddSingleton<DiagnosticObserver>(services => CreateObserver(bot, services));
        bot.Services.AddSingleton<IGameObserver>(services => services.GetRequiredService<DiagnosticObserver>());
    }

    private static DiagnosticObserver CreateObserver(BotBuilder bot, IServiceProvider services)
    {
        ILogger<DiagnosticObserver> log = services.GetRequiredService<ILogger<DiagnosticObserver>>();
        try
        {
            var source = services.GetRequiredService<ProtocolSource>();
            var translation = services.GetRequiredService<D3B.Protocol.Translation.ProtocolTranslation>();
            var schema = DiagnosticSchemaExporter.Export(translation.ClientVersion, ProtocolSchema.Load(source.SchemaPath));
            var options = new SidecarOptions
            {
                EnginePath = bot.Configuration[EnginePathKey] ?? string.Empty,
                SocketDirectory = bot.Configuration[SocketDirectoryKey]
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "diagnostic"),
                QueueCapacity = bot.Configuration.GetValue(QueueCapacityKey, 256),
                QueueByteCapacity = bot.Configuration.GetValue(QueueByteCapacityKey, 16L * 1024 * 1024),
            };

            options.Validate();

            if (string.IsNullOrWhiteSpace(options.EnginePath) || !File.Exists(options.EnginePath))
            {
                throw new FileNotFoundException("The diagnostic engine path is missing.");
            }

            DiagnosticReportOptions? reportOptions = null;
            IDiagnosticReportWriter? reportWriter = null;
            if (bot.Configuration.GetValue<bool>(ReportEnabledKey))
            {
                try
                {
                    reportOptions = CreateReportOptions(bot.Configuration);
                    _ = ResolveReportDirectory(bot.Configuration);
                    reportWriter = services.GetRequiredService<IDiagnosticReportWriter>();
                }
                catch (Exception)
                {
                    reportOptions = null;
                    reportWriter = null;
                    try
                    {
                        LogReportSetupDisabled(log);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            bool includeReadable = bot.Configuration.GetValue<bool>(IncludeReadableKey);

            IDiagnosticSessionFactory sessions = new DiagnosticSink(
                options,
                schema,
                services.GetRequiredService<IDiagnosticSidecarFactory>(),
                services.GetRequiredService<ILoggerFactory>(),
                reportOptions,
                reportWriter,
                services.GetService<TimeProvider>() ?? TimeProvider.System,
                includeReadable);
            return new DiagnosticObserver(
                sessions,
                includeReadable,
                log,
                schema.Messages.Select(message => message.Name).ToHashSet(StringComparer.Ordinal),
                options.QueueCapacity,
                options.QueueByteCapacity);
        }
        catch (Exception)
        {
            try
            {
                LogSetupDisabled(log);
            }
            catch (Exception)
            {
            }

            return new DiagnosticObserver(log);
        }
    }

    private static DiagnosticReportOptions CreateReportOptions(IConfiguration configuration)
    {
        long maxBytes = configuration.GetValue(ReportMaxBytesKey, 4L * 1024 * 1024);
        if (maxBytes is < 65_536 or > 67_108_864)
        {
            throw new InvalidOperationException("The diagnostic report size is outside the supported range.");
        }

        var options = new DiagnosticReportOptions { Enabled = true, MaxBytes = (int)maxBytes };
        options.Validate();
        return options;
    }

    private static string ResolveReportDirectory(IConfiguration configuration)
    {
        string? configuredDirectory = configuration[ReportDirectoryKey];
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return configuredDirectory;
        }

        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The private report directory is unavailable.");
        }

        return Path.Combine(localApplicationData, "D3B", "diagnostic", "reports");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Diagnostic observation is disabled because setup failed.")]
    private static partial void LogSetupDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Diagnostic report export is disabled because report setup failed.")]
    private static partial void LogReportSetupDisabled(ILogger logger);
}
