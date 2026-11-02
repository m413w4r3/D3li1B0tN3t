using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace D3B.Storage;

/// <summary>The context <c>dotnet ef</c> builds the model of, to write a migration, with no host and no database.</summary>
internal sealed class DesignTimeStorageContext : IDesignTimeDbContextFactory<StorageContext>
{
    /// <inheritdoc/>
    public StorageContext CreateDbContext(string[] args)
    {
        return new StorageContext(new DbContextOptionsBuilder<StorageContext>().UseSqlite("Data Source=design.db").Options);
    }
}
