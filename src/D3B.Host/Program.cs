using D3B.Bot.Hosting;
using D3B.Storage;

var builder = BotHost.CreateBuilder(args).AddModule<StorageModule>().AddModule<SupervisedMoveModule>().AddModule<SupervisedElementUseModule>();

try
{
    GuiHostStartup.Register(builder);
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
{
    Console.Error.WriteLine($"Invalid GUI configuration: {exception.Message}");

    return 1;
}

return await BotHost.RunAsync(builder, CancellationToken.None);
