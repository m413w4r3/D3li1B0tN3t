using System.CommandLine;
using System.Net;
using System.Net.NetworkInformation;
using D3B.Network.Clients;
using D3B.Network.Relay;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Client;

/// <summary>The commands that check where the client's configuration sends it, and give it Ankama's back.</summary>
internal static class ClientCommand
{
    private const int Done = 0;
    private const int NotDone = 1;
    private const int Unreadable = 2;

    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client's directory. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
            Recursive = true,
        };
        var directory = new Option<DirectoryInfo?>("--redirect-directory")
        {
            Description = @"Where D3B keeps the client's configuration, as the host's Network:Redirect:Directory names it. Defaults to %LOCALAPPDATA%\D3B\client.",
            Recursive = true,
        };
        var address = new Option<string>("--address")
        {
            Description = "D3B's loopback address, as the host's Network:Address names it.",
            DefaultValueFactory = _ => RelaySettings.DefaultAddress.ToString(),
            Recursive = true,
        };
        var check = new Command("check", "Says which configuration the client reads, Ankama's or D3B's, and whether D3B listens. The default.");
        var restore = new Command("restore", "Gives the client Ankama's configuration back, as the host does when it stops: after a crash, clients then play without D3B.");
        var command = new RootCommand("Checks where the Dofus 3 client's configuration sends it, and gives it Ankama's back (D35).")
        {
            client,
            directory,
            address,
            check,
            restore,
        };

        command.SetAction(result => Run(result.GetValue(client), result.GetValue(directory), result.GetValue(address)!, false));
        check.SetAction(result => Run(result.GetValue(client), result.GetValue(directory), result.GetValue(address)!, false));
        restore.SetAction(result => Run(result.GetValue(client), result.GetValue(directory), result.GetValue(address)!, true));

        return command;
    }

    private static int Run(DirectoryInfo? clientDirectory, DirectoryInfo? redirectDirectory, string address, bool restore)
    {
        try
        {
            var versionFile = ClientVersionFile.PathIn(ClientInstall.Locate(clientDirectory).Root);
            var directory = redirectDirectory?.FullName ?? RelaySettings.DefaultRedirectDirectory;

            return restore ? Restore(versionFile, directory) : Check(versionFile, directory, address);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);

            return Unreadable;
        }
    }

    private static int Check(string versionFile, string directory, string address)
    {
        var current = ClientVersionFile.ReadConfigUrl(versionFile) ?? throw new InvalidDataException($"{versionFile} has no ConfigUrl line.");

        if (current != ClientRedirectFiles.ConfigUrl(directory))
        {
            Console.WriteLine($"{versionFile} names Ankama's configuration: clients connect straight to Ankama.");

            return Done;
        }

        var configFile = Path.Combine(directory, ClientRedirectFiles.ConfigFileName);
        var hosts = File.Exists(configFile) ? ClientConfiguration.Parse(File.ReadAllText(configFile)).ConnectionHosts : [];
        var host = hosts.Count > 0 ? hosts[0].Host : null;

        Console.WriteLine($"{versionFile} names D3B's configuration, {configFile}, which sends clients to {host ?? "nowhere: the file is missing"}.");
        Console.WriteLine(ClientRedirectFiles.OriginalUrl(directory) is { } original ? $"Ankama's configuration, kept: {original}" : "Ankama's configuration URL is not kept: repair the client from the launcher.");

        // Listeners are listed without connecting: a connection would be relayed to Ankama.
        var listening = IPAddress.TryParse(address, out var parsed)
            && IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(listener => listener.Address.Equals(parsed));

        if (listening)
        {
            Console.WriteLine($"D3B listens on {address}: clients connect through it.");

            return Done;
        }

        Console.WriteLine($"Nothing listens on {address}: clients cannot connect. Start D3B's host, or give the client Ankama's configuration back: dotnet run --file scripts/client.cs -- restore");

        return NotDone;
    }

    private static int Restore(string versionFile, string directory)
    {
        if (ClientVersionFile.ReadConfigUrl(versionFile) != ClientRedirectFiles.ConfigUrl(directory))
        {
            Console.WriteLine($"Nothing to do: {versionFile} already names Ankama's configuration.");

            return Done;
        }

        if (ClientRedirectFiles.OriginalUrl(directory) is not { } original)
        {
            Console.Error.WriteLine($"Ankama's configuration URL is not kept in {directory}: repair the client from the launcher, which writes {versionFile} anew.");

            return NotDone;
        }

        ClientVersionFile.WriteConfigUrl(versionFile, original);
        Console.WriteLine($"Restored {versionFile}: clients connect straight to Ankama.");

        return Done;
    }
}
