using D3B.Bot.Hosting;
using D3B.Storage;
using Microsoft.Extensions.DependencyInjection;

var builder = BotHost.CreateBuilder(args).AddModule<StorageModule>().AddModule<SupervisedMoveModule>();
GuiHostCapability capability;

try
{
    capability = GuiHostStartup.Validate(builder.Configuration);
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
{
    Console.Error.WriteLine($"Invalid GUI configuration: {exception.Message}");

    return 1;
}

builder.Services.AddSingleton(capability);

return await BotHost.RunAsync(builder, CancellationToken.None);
