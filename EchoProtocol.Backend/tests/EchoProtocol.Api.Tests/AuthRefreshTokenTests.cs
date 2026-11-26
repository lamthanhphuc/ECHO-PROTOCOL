using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Auth;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AuthRefreshTokenTests
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task Login_IssuesRefreshToken_AndDatabaseStoresOnlyHash()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var result = await harness.Service.LoginAsync(
            new LoginRequest
            {
                Username = "player",
                Password = Password
            });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.True(result.Data.RefreshExpiresAt > DateTime.UtcNow);

        var session = await harness.Db.RefreshSessions.SingleAsync();

        Assert.NotEqual(result.Data.RefreshToken, session.TokenHash);
        Assert.Equal(Hash(result.Data.RefreshToken), session.TokenHash);
        Assert.False(session.RevokedAtUtc.HasValue);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRevokesPreviousSession()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var login = await harness.LoginAsync();

        var refreshed = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken
            });

        Assert.True(refreshed.IsSuccess);
        Assert.NotNull(refreshed.Data);
        Assert.NotEqual(
            login.RefreshToken,
            refreshed.Data!.RefreshToken);

        var sessions = await harness.Db.RefreshSessions
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, sessions.Count);

        var oldSession = sessions.Single(
            item => item.TokenHash == Hash(login.RefreshToken));

        var newSession = sessions.Single(
            item => item.TokenHash == Hash(refreshed.Data.RefreshToken));

        Assert.True(oldSession.RevokedAtUtc.HasValue);
        Assert.Equal(newSession.Id, oldSession.ReplacedBySessionId);
        Assert.Equal(oldSession.FamilyId, newSession.FamilyId);
        Assert.False(newSession.RevokedAtUtc.HasValue);
    }

    [Fact]
    public async Task ReusingRotatedRefreshToken_RevokesTokenFamily()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var login = await harness.LoginAsync();

        var refreshed = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken
            });

        Assert.True(refreshed.IsSuccess);

        var replay = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken
            });

        Assert.False(replay.IsSuccess);
        Assert.Equal(
            ErrorCodes.RefreshTokenReused,
            replay.ErrorCode);

        harness.Db.ChangeTracker.Clear();

        var activeInFamily = await harness.Db.RefreshSessions
            .CountAsync(item => item.RevokedAtUtc == null);

        Assert.Equal(0, activeInFamily);

        var replacementUse = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = refreshed.Data!.RefreshToken
            });

        Assert.False(replacementUse.IsSuccess);
        Assert.Equal(
            ErrorCodes.RefreshTokenReused,
            replacementUse.ErrorCode);
    }

    [Fact]
    public async Task Logout_RevokesWholeFamily_AndIsIdempotent()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var login = await harness.LoginAsync();

        var refreshed = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken
            });

        Assert.True(refreshed.IsSuccess);

        var logout = await harness.Service.LogoutAsync(
            new RefreshTokenRequest
            {
                RefreshToken = refreshed.Data!.RefreshToken
            });

        Assert.True(logout.IsSuccess);

        harness.Db.ChangeTracker.Clear();

        Assert.Equal(
            0,
            await harness.Db.RefreshSessions.CountAsync(
                item => item.RevokedAtUtc == null));

        var secondLogout = await harness.Service.LogoutAsync(
            new RefreshTokenRequest
            {
                RefreshToken = refreshed.Data.RefreshToken
            });

        Assert.True(secondLogout.IsSuccess);

        var refreshAfterLogout = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = refreshed.Data.RefreshToken
            });

        Assert.False(refreshAfterLogout.IsSuccess);
        Assert.Equal(
            ErrorCodes.RefreshTokenReused,
            refreshAfterLogout.ErrorCode);
    }

    [Fact]
    public async Task ExpiredRefreshToken_IsRejected()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var login = await harness.LoginAsync();

        var session = await harness.Db.RefreshSessions.SingleAsync();

        var now = DateTime.UtcNow;
        session.CreatedAtUtc = now.AddDays(-2);
        session.ExpiresAtUtc = now.AddDays(-1);

        await harness.Db.SaveChangesAsync();

        var refreshed = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = login.RefreshToken
            });

        Assert.False(refreshed.IsSuccess);
        Assert.Equal(
            ErrorCodes.RefreshTokenExpired,
            refreshed.ErrorCode);
    }

    [Fact]
    public async Task UnknownRefreshToken_IsRejected()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var result = await harness.Service.RefreshAsync(
            new RefreshTokenRequest
            {
                RefreshToken = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(64))
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ErrorCodes.RefreshTokenInvalid,
            result.ErrorCode);
    }

    // TC-MF-01-01-01: registration persists a hashed password, profile, and wallet.
    [Fact]
    public async Task RegisterPersistsHashedPasswordAndOwnProfile()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var result = await harness.Service.RegisterAsync(new RegisterRequest
        {
            Email = "new-player@echo.invalid",
            Username = "new-player",
            Password = Password,
            ConfirmPassword = Password
        });

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        var user = await harness.Db.Users
            .Include(item => item.PlayerProfile)
            .Include(item => item.Wallet)
            .SingleAsync(item => item.Id == result.Data!.Id);
        Assert.Equal("new-player", user.Username);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(user.PasswordHash.Length > 0);
        Assert.NotNull(user.Wallet);
        Assert.Equal(GameConstants.DefaultPlayerWalletBalance, user.Wallet!.Balance);
        Assert.NotNull(user.PlayerProfile);
        Assert.Equal("new-player", user.PlayerProfile!.DisplayName);
        Assert.Equal(1, await harness.Db.Wallets.CountAsync(item => item.UserId == user.Id));

        var profile = await harness.Service.GetCurrentUserAsync(user.Id);
        Assert.True(profile.IsSuccess);
        Assert.Equal(user.Id, profile.Data!.Id);
        Assert.Equal("new-player", profile.Data.Username);

        var login = await harness.Service.LoginAsync(
            new LoginRequest { Username = "new-player", Password = Password });
        Assert.True(login.IsSuccess);
        Assert.Equal(user.Id, login.Data!.User.Id);
        var session = await harness.Db.RefreshSessions.SingleAsync();
        Assert.Equal(Hash(login.Data.RefreshToken), session.TokenHash);
    }

    // TC-MF-01-01-02: a wrong password must not create a session or mutate account state.
    [Fact]
    public async Task InvalidCredentialsDoNotCreateSessionOrMutateAccount()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var balanceBefore = (await harness.Db.Wallets.SingleAsync()).Balance;
        var sessionsBefore = await harness.Db.RefreshSessions.CountAsync();

        // Wrong password: must fail and leave all state unchanged.
        var badLogin = await harness.Service.LoginAsync(
            new LoginRequest { Username = "player", Password = "WrongPassword!" });

        Assert.False(badLogin.IsSuccess);
        Assert.Null(badLogin.Data);

        harness.Db.ChangeTracker.Clear();

        Assert.Equal(sessionsBefore, await harness.Db.RefreshSessions.CountAsync());
        Assert.Equal(balanceBefore, (await harness.Db.Wallets.SingleAsync()).Balance);
        Assert.Equal(1, await harness.Db.Users.CountAsync());
    }

    // TC-MF-01-01-02: a duplicate username must not create an account or wallet.
    [Fact]
    public async Task InvalidRegistrationDoesNotCreateAccountOrWallet()
    {
        await using var harness = await AuthHarness.CreateAsync();

        var usersBefore = await harness.Db.Users.CountAsync();
        var walletsBefore = await harness.Db.Wallets.CountAsync();
        var result = await harness.Service.RegisterAsync(new RegisterRequest
        {
            Email = "another-email@echo.invalid",
            Username = "player",
            Password = Password,
            ConfirmPassword = Password
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UsernameAlreadyExists, result.ErrorCode);
        harness.Db.ChangeTracker.Clear();
        Assert.Equal(usersBefore, await harness.Db.Users.CountAsync());
        Assert.Equal(walletsBefore, await harness.Db.Wallets.CountAsync());
        Assert.Empty(await harness.Db.RefreshSessions.ToListAsync());
    }

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));

    private sealed class AuthHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AuthHarness(
            SqliteConnection connection,
            AppDbContext db,
            AuthService service)
        {
            _connection = connection;
            Db = db;
            Service = service;
        }

        public AppDbContext Db { get; }
        public AuthService Service { get; }

        public static async Task<AuthHarness> CreateAsync()
        {
            var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();

            var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);

            await db.Database.EnsureCreatedAsync();

            var passwordHasher = new BCryptPasswordHasher();

            var jwtSettings = Options.Create(
                new JwtSettings
                {
                    Issuer = "EchoProtocol.Tests",
                    Audience = "EchoProtocol.Tests",
                    SecretKey =
                        "TEST_ONLY_SECRET_KEY_0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                    ExpiryMinutes = 60,
                    RefreshTokenExpiryDays = 14
                });

            var jwt = new JwtTokenService(jwtSettings);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "player@echo.invalid",
                Username = "player",
                PasswordHash =
                    passwordHasher.Hash(Password),
                Role = UserRole.PLAYER,
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PlayerProfile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Player",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                Wallet = new Wallet
                {
                    Id = Guid.NewGuid(),
                    Balance = 500,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var service = new AuthService(
                db,
                passwordHasher,
                jwt,
                jwtSettings);

            return new AuthHarness(
                connection,
                db,
                service);
        }

        public async Task<AuthResponse> LoginAsync()
        {
            var result = await Service.LoginAsync(
                new LoginRequest
                {
                    Username = "player",
                    Password = Password
                });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);

            return result.Data!;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
