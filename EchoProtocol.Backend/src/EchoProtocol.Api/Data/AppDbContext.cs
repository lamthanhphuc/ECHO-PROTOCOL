using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<MatchAuthorityBinding> MatchAuthorityBindings => Set<MatchAuthorityBinding>();
    public DbSet<MatchPlayerBinding> MatchPlayerBindings => Set<MatchPlayerBinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.Username)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.HasOne(e => e.PlayerProfile)
                .WithOne(e => e.User)
                .HasForeignKey<PlayerProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Wallet)
                .WithOne(e => e.User)
                .HasForeignKey<Wallet>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalMatches_NonNegative",
                    "\"TotalMatches\" >= 0");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalWins_NonNegative",
                    "\"TotalWins\" >= 0");
                t.HasCheckConstraint(
                    "CK_PlayerProfiles_TotalWins_Lte_Matches",
                    "\"TotalWins\" <= \"TotalMatches\"");
            });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TotalMatches).HasDefaultValue(0);
            entity.Property(e => e.TotalWins).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_PlayerProfiles_UserId");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Wallets_Balance_NonNegative",
                    "\"Balance\" >= 0");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Balance).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Wallets_UserId");
        });

        modelBuilder.Entity<MatchAuthorityBinding>(entity =>
        {
            entity.ToTable("MatchAuthorityBindings");
            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.FusionSessionName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => new { e.FusionSessionName, e.Status });
            entity.HasIndex(e => e.HostUserId);
            entity.HasOne(e => e.HostUser)
                .WithMany()
                .HasForeignKey(e => e.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayerBinding>(entity =>
        {
            entity.ToTable("MatchPlayerBindings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MatchId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.MatchId, e.FusionActorNumber }).IsUnique();
            entity.HasIndex(e => e.JoinProofId).IsUnique();
            entity.HasOne(e => e.Match)
                .WithMany(e => e.Players)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
