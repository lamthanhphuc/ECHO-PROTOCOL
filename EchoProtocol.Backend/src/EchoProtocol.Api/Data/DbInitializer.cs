using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        AppDbContext db,
        IOptions<AdminSeedSettings> adminSeedOptions,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var settings = adminSeedOptions.Value;

        if (string.IsNullOrWhiteSpace(settings.Username))
        {
            logger.LogWarning("Admin seed skipped: username not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("Admin seed skipped: password not configured.");
            return;
        }

        if (settings.InitialWalletBalance < 0)
        {
            throw new InvalidOperationException(
                "Admin seed configuration error: InitialWalletBalance must be non-negative.");
        }

        var normalizedAdmin = UsernameNormalizer.Normalize(settings.Username);
        var displayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? settings.Username.Trim()
            : settings.DisplayName.Trim();

        if (await db.Users.AnyAsync(u => u.Username == normalizedAdmin, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = normalizedAdmin,
            PasswordHash = passwordHasher.Hash(settings.Password),
            Role = UserRole.ADMIN,
            Status = UserStatus.ACTIVE,
            CreatedAt = now,
            UpdatedAt = now,
            PlayerProfile = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = displayName,
                TotalMatches = 0,
                TotalWins = 0,
                CreatedAt = now,
                UpdatedAt = now
            },
            Wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Balance = settings.InitialWalletBalance,
                UpdatedAt = now
            }
        };

        db.Users.Add(admin);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Admin user seeded successfully.");
        }
        catch (DbUpdateException ex) when (IsUsernameUniqueViolation(ex))
        {
            logger.LogInformation("Admin already exists (race).");
        }
    }

    private static bool IsUsernameUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == "IX_Users_Username";
    }
}
