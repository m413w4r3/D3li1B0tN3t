using Microsoft.Data.Sqlite;

namespace D3B.Storage;

/// <summary>Where D3B's database lives, which the app and <c>D3B.Host</c> share (D100).</summary>
public sealed class StorageSettings
{
    /// <summary>The setting naming the database's file.</summary>
    public const string PathKey = "Storage:Path";

    /// <summary>Gets the database's file by default: <c>%LOCALAPPDATA%\D3B\d3b.db</c>, beside the packs, captures and the client's configuration.</summary>
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "d3b.db");

    /// <summary>Gets the database's file, a full path.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the connection string SQLite opens the file with.</summary>
    /// <remarks>
    /// No pool: a connection closes its file once done with, so that the file is not held while D3B is idle; foreign keys
    /// on, which forgetting an account and deleting a profile rely on.
    /// </remarks>
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false, ForeignKeys = true }.ToString();
}
