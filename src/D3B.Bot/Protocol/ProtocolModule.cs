using D3B.Bot.Data;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Protocol.Translation;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Protocol;

/// <summary>The protocol of the installed client's build: its schema and mapping, audited before the host starts (D11, D48).</summary>
/// <remarks>
/// <para>
/// Requires the data module, which knows the installed build and where its files are kept. Registers
/// <see cref="ProtocolSource"/> and the build's <see cref="ProtocolTranslation"/>, a singleton every bot shares, made when
/// first asked for.
/// </para>
/// <para>
/// Nothing is read while the module builds. The host asks for the translation before it starts: a build without a mapping
/// that holds stops it, with the commands that map the build.
/// </para>
/// </remarks>
public sealed class ProtocolModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<DataModule>();

        var settings = DataSettings.Read(bot.Configuration);

        bot.Services.AddSingleton(services => new ProtocolSource(settings, services.GetRequiredService<ClientBuild>()));
        bot.Services.AddSingleton(services => services.GetRequiredService<ProtocolSource>().Load());
    }
}
