using Microsoft.EntityFrameworkCore;

namespace BoardGameFanatics.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<PlayerStatus>(null, "PlayerStatus", UpperCaseNameTranslator.Instance);
        modelBuilder.HasPostgresEnum<PlayerRole>(null, "PlayerRole", UpperCaseNameTranslator.Instance);

        modelBuilder.Entity<Player>(b =>
        {
            b.ToTable("Player");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasColumnName("id");
            b.Property(p => p.DisplayName).HasColumnName("displayName");
            b.Property(p => p.Status).HasColumnName("status").HasColumnType("\"PlayerStatus\"");
            b.Property(p => p.Role).HasColumnName("role").HasColumnType("\"PlayerRole\"");
            b.Property(p => p.CreatedAt).HasColumnName("createdAt");
        });

        modelBuilder.Entity<Game>(b =>
        {
            b.ToTable("Game");
            b.HasKey(g => g.BggId);
            b.Property(g => g.BggId).HasColumnName("bggId").ValueGeneratedNever();
            b.Property(g => g.Name).HasColumnName("name");
            b.Property(g => g.YearPublished).HasColumnName("yearPublished");
            b.Property(g => g.MinPlayers).HasColumnName("minPlayers");
            b.Property(g => g.MaxPlayers).HasColumnName("maxPlayers");
            b.Property(g => g.PlayingTimeMinutes).HasColumnName("playingTimeMinutes");
            b.Property(g => g.MinAge).HasColumnName("minAge");
            b.Property(g => g.Description).HasColumnName("description");
            b.Property(g => g.ThumbnailUrl).HasColumnName("thumbnailUrl");
            b.Property(g => g.ImageUrl).HasColumnName("imageUrl");
            b.Property(g => g.CachedAt).HasColumnName("cachedAt");
        });

        modelBuilder.Entity<CollectionEntry>(b =>
        {
            b.ToTable("CollectionEntry");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasColumnName("id");
            b.Property(e => e.PlayerId).HasColumnName("playerId");
            b.Property(e => e.GameId).HasColumnName("gameId");
            b.Property(e => e.CreatedAt).HasColumnName("createdAt");

            b.HasOne(e => e.Player)
                .WithMany(p => p.CollectionEntries)
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Game)
                .WithMany(g => g.CollectionEntries)
                .HasForeignKey(e => e.GameId);

            b.HasIndex(e => new { e.PlayerId, e.GameId }).IsUnique();
        });
    }
}
