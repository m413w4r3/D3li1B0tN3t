using D3B.Bot.Hosting;
using D3B.Storage;

return await BotHost.RunAsync(BotHost.CreateBuilder(args).AddModule<StorageModule>(), CancellationToken.None);
