using Kestrelle.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kestrelle.Models.Data;

public sealed class KestrelleDbContext : DbContext
{
    public KestrelleDbContext(DbContextOptions<KestrelleDbContext> options) : base(options) { }

    public DbSet<DiscordGuild> Guilds => Set<DiscordGuild>();
    public DbSet<DiscordUser> Users => Set<DiscordUser>();
    public DbSet<Sound> Sounds => Set<Sound>();
    public DbSet<DiscordOAuthToken> DiscordOAuthTokens => Set<DiscordOAuthToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("kestrelle");

        modelBuilder.Entity<DiscordGuild>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Name);

            e.HasMany(x => x.Sounds).WithOne(x => x.Guild).HasForeignKey(x => x.GuildId);
        });

        modelBuilder.Entity<DiscordUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(128);
            e.HasIndex(x => x.Username);
        });

        modelBuilder.Entity<Sound>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128);

            e.HasIndex(x => new { x.GuildId, x.DisplayName });

            e.HasOne(x => x.UploadedByUser)
                .WithMany(x => x.UploadedSounds)
                .HasForeignKey(x => x.UploadedByUserId);
        });

        modelBuilder.Entity<DiscordOAuthToken>(b =>
        {
            b.HasKey(x => x.DiscordUserId);
            b.Property(x => x.AccessToken).IsRequired();
            b.Property(x => x.RefreshToken).IsRequired();
            b.Property(x => x.ExpiresAtUtc).IsRequired();
        });
    }
}
