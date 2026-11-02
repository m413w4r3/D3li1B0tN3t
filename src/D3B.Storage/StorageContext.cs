using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Profiles;
using D3B.Storage.Runs;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage;

/// <summary>D3B's database: the accounts and characters it saw, the profiles, what the bots learned of the maps' graphics, and the fights' placement cells (D100, D138, D275).</summary>
public sealed class StorageContext : DbContext
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<KnownAccount> Accounts => Set<KnownAccount>();

    /// <summary>Gets the characters.</summary>
    public DbSet<KnownCharacter> Characters => Set<KnownCharacter>();

    /// <summary>Gets the profiles.</summary>
    public DbSet<Profile> Profiles => Set<Profile>();

    /// <summary>Gets the presets the activities give, once stored as profiles (D168).</summary>
    public DbSet<StoredPreset> Presets => Set<StoredPreset>();

    /// <summary>Gets the graphics the bots learned.</summary>
    public DbSet<LearnedGraphic> Graphics => Set<LearnedGraphic>();

    /// <summary>Gets how long the resources take to grow back, by the skill that gathers each (D145).</summary>
    public DbSet<LearnedRegrowth> Regrowths => Set<LearnedRegrowth>();

    /// <summary>Gets the fights' placement cells, the map fought on and the one left, as the bots saw fights begin (D275).</summary>
    public DbSet<LearnedPlacement> Placements => Set<LearnedPlacement>();

    /// <summary>Gets the runs of the activities, for the statistics (D146).</summary>
    public DbSet<RunRecord> Runs => Set<RunRecord>();

    /// <summary>Initializes a new instance of the <see cref="StorageContext"/> class.</summary>
    /// <param name="options">The context's options: SQLite, on the database's file.</param>
    public StorageContext(DbContextOptions<StorageContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<KnownAccount>(account =>
        {
            account.ToTable("Accounts");
            account.HasKey(known => known.Id);
            account.Property(known => known.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Profile>(profile =>
        {
            profile.ToTable("Profiles");
            profile.HasKey(known => known.Id);
            profile.Property(known => known.Name).HasMaxLength(Profile.NameLength).UseCollation("NOCASE");
            profile.HasIndex(known => known.Name).IsUnique();
        });

        modelBuilder.Entity<StoredPreset>(preset =>
        {
            preset.ToTable("Presets");
            preset.HasKey(stored => new { stored.ActivityId, stored.Key });
        });

        modelBuilder.Entity<LearnedGraphic>(graphic =>
        {
            graphic.ToTable("Graphics");
            graphic.HasKey(learned => learned.GfxId);
            graphic.Property(learned => learned.GfxId).ValueGeneratedNever();
        });

        modelBuilder.Entity<LearnedRegrowth>(regrowth =>
        {
            regrowth.ToTable("Regrowths");
            regrowth.HasKey(learned => learned.SkillId);
            regrowth.Property(learned => learned.SkillId).ValueGeneratedNever();
        });

        modelBuilder.Entity<LearnedPlacement>(placement =>
        {
            placement.ToTable("Placements");
            placement.HasKey(learned => new { learned.FromMapId, learned.MapId, learned.RedCells, learned.BlueCells });
        });

        modelBuilder.Entity<RunRecord>(run =>
        {
            run.ToTable("Runs");
            run.HasKey(record => record.Id);
            run.Property(record => record.Items).HasConversion(items => RunJson.Write(items), json => RunJson.ReadItems(json), RunJson.ItemsComparer);
            run.Property(record => record.Jobs).HasConversion(jobs => RunJson.Write(jobs), json => RunJson.ReadJobs(json), RunJson.JobsComparer);
        });

        modelBuilder.Entity<KnownCharacter>(character =>
        {
            character.ToTable("Characters");
            character.HasKey(known => new { known.AccountId, known.Id });
            character.Ignore(known => known.Key);

            // Forgetting an account forgets its characters; deleting a profile leaves its characters without one.
            character.HasOne<KnownAccount>().WithMany().HasForeignKey(known => known.AccountId).OnDelete(DeleteBehavior.Cascade);
            character.HasOne<Profile>().WithMany().HasForeignKey(known => known.ProfileId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
