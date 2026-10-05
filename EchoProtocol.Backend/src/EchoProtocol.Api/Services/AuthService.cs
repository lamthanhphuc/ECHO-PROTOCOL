using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Auth;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public class AuthService : IAuthService
{
    private const int MaxRefreshTokenLength = 512;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<ServiceResult<UserSummaryResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > 255
            || !new EmailAddressAttribute().IsValid(request.Email))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (PasswordPolicy.IsTooShort(request.Password))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (PasswordPolicy.ExceedsMaxUtf8ByteLength(request.Password))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Password must not exceed 72 UTF-8 bytes",
                ErrorCodes.PasswordTooLong);
        }

        if (request.Password != request.ConfirmPassword)
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Password confirmation does not match",
                ErrorCodes.PasswordConfirmationMismatch);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = UsernameNormalizer.Normalize(request.Username);
        var displayName = request.Username.Trim();

        if (await _db.Users.AnyAsync(
                user => user.Email == normalizedEmail,
                cancellationToken))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Email already exists",
                ErrorCodes.EmailAlreadyExists);
        }

        if (await _db.Users.AnyAsync(
                user => user.Username == normalizedUsername,
                cancellationToken))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Username already exists",
                ErrorCodes.UsernameAlreadyExists);
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = normalizedUsername,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.PLAYER,
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
                Balance = GameConstants.DefaultPlayerWalletBalance,
                UpdatedAt = now
            }
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            IsUniqueViolation(ex, "IX_Users_Email"))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Email already exists",
                ErrorCodes.EmailAlreadyExists);
        }
        catch (DbUpdateException ex) when (
            IsUniqueViolation(ex, "IX_Users_Username"))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Username already exists",
                ErrorCodes.UsernameAlreadyExists);
        }

        return ServiceResult<UserSummaryResponse>.Success(
            new UserSummaryResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role.ToString()
            },
            "Register successfully");
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (PasswordPolicy.ExceedsMaxUtf8ByteLength(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Password must not exceed 72 UTF-8 bytes",
                ErrorCodes.PasswordTooLong);
        }

        var normalized = UsernameNormalizer.Normalize(request.Username);

        var user = await _db.Users
            .Include(item => item.Wallet)
            .FirstOrDefaultAsync(
                item => item.Username == normalized,
                cancellationToken);

        if (user is null
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Invalid username or password",
                ErrorCodes.InvalidCredentials);
        }

        if (user.Status == UserStatus.LOCKED)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Account is locked",
                ErrorCodes.AccountLocked);
        }

        if (user.Wallet is null)
        {
            throw new InvalidOperationException(
                $"Data integrity error: wallet missing for user {user.Id}");
        }

        var now = DateTime.UtcNow;
        var refresh = CreateRefreshSession(
            user.Id,
            Guid.NewGuid(),
            now);

        _db.RefreshSessions.Add(refresh.Session);
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAtUtc) =
            _jwtTokenService.GenerateToken(user);

        return ServiceResult<AuthResponse>.Success(
            BuildAuthResponse(
                user,
                accessToken,
                expiresAtUtc,
                refresh.Token,
                refresh.Session.ExpiresAtUtc),
            "Login successfully");
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsRefreshTokenValidShape(request.RefreshToken))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Refresh token is invalid",
                ErrorCodes.RefreshTokenInvalid);
        }

        var tokenHash = HashRefreshToken(request.RefreshToken);

        var session = await _db.RefreshSessions
            .Include(item => item.User)
            .ThenInclude(user => user.Wallet)
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash,
                cancellationToken);

        if (session is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Refresh token is invalid",
                ErrorCodes.RefreshTokenInvalid);
        }

        var now = DateTime.UtcNow;

        if (session.RevokedAtUtc.HasValue)
        {
            var familyId = session.FamilyId;

            _db.ChangeTracker.Clear();

            await RevokeFamilyAsync(
                familyId,
                now,
                cancellationToken);

            return ServiceResult<AuthResponse>.Failure(
                "Refresh token reuse detected",
                ErrorCodes.RefreshTokenReused);
        }

        if (session.ExpiresAtUtc <= now)
        {
            session.RevokedAtUtc = now;
            session.ConcurrencyToken = Guid.NewGuid();

            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult<AuthResponse>.Failure(
                "Refresh token has expired",
                ErrorCodes.RefreshTokenExpired);
        }

        var user = session.User;

        if (user.Status == UserStatus.LOCKED)
        {
            var familyId = session.FamilyId;

            _db.ChangeTracker.Clear();

            await RevokeFamilyAsync(
                familyId,
                now,
                cancellationToken);

            return ServiceResult<AuthResponse>.Failure(
                "Account is locked",
                ErrorCodes.AccountLocked);
        }

        if (user.Wallet is null)
        {
            throw new InvalidOperationException(
                $"Data integrity error: wallet missing for user {user.Id}");
        }

        var replacement = CreateRefreshSession(
            user.Id,
            session.FamilyId,
            now);

        session.LastUsedAtUtc = now;
        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = replacement.Session.Id;
        session.ConcurrencyToken = Guid.NewGuid();

        _db.RefreshSessions.Add(replacement.Session);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var familyId = session.FamilyId;

            _db.ChangeTracker.Clear();

            await RevokeFamilyAsync(
                familyId,
                DateTime.UtcNow,
                cancellationToken);

            return ServiceResult<AuthResponse>.Failure(
                "Refresh token reuse detected",
                ErrorCodes.RefreshTokenReused);
        }

        var (accessToken, accessExpiresAt) =
            _jwtTokenService.GenerateToken(user);

        return ServiceResult<AuthResponse>.Success(
            BuildAuthResponse(
                user,
                accessToken,
                accessExpiresAt,
                replacement.Token,
                replacement.Session.ExpiresAtUtc),
            "Token refreshed successfully");
    }

    public async Task<ServiceResult<bool>> LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsRefreshTokenValidShape(request.RefreshToken))
        {
            return ServiceResult<bool>.Failure(
                "Refresh token is invalid",
                ErrorCodes.RefreshTokenInvalid);
        }

        var tokenHash = HashRefreshToken(request.RefreshToken);

        var session = await _db.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash,
                cancellationToken);

        if (session is null)
        {
            // Logout is intentionally idempotent and does not reveal
            // whether a refresh token ever existed.
            return ServiceResult<bool>.Success(
                true,
                "Logout successfully");
        }

        await RevokeFamilyAsync(
            session.FamilyId,
            DateTime.UtcNow,
            cancellationToken);

        return ServiceResult<bool>.Success(
            true,
            "Logout successfully");
    }

    public async Task<ServiceResult<MeResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(item => item.PlayerProfile)
            .Include(item => item.Wallet)
            .FirstOrDefaultAsync(
                item => item.Id == userId,
                cancellationToken);

        if (user is null)
        {
            return ServiceResult<MeResponse>.Failure(
                "User not found",
                ErrorCodes.NotFound);
        }

        if (user.Status == UserStatus.LOCKED)
        {
            return ServiceResult<MeResponse>.Failure(
                "Account is locked",
                ErrorCodes.AccountLocked);
        }

        if (user.PlayerProfile is null)
        {
            throw new InvalidOperationException(
                $"Data integrity error: player profile missing for user {user.Id}");
        }

        if (user.Wallet is null)
        {
            throw new InvalidOperationException(
                $"Data integrity error: wallet missing for user {user.Id}");
        }

        return ServiceResult<MeResponse>.Success(
            new MeResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role.ToString(),
                DisplayName = user.PlayerProfile.DisplayName,
                WalletBalance = user.Wallet.Balance
            },
            "Current user loaded");
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var activeSessions = await _db.RefreshSessions
                .Where(item =>
                    item.FamilyId == familyId
                    && item.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            if (activeSessions.Count == 0)
            {
                return;
            }

            foreach (var activeSession in activeSessions)
            {
                activeSession.RevokedAtUtc = revokedAtUtc;
                activeSession.ConcurrencyToken = Guid.NewGuid();
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    private (RefreshSession Session, string Token) CreateRefreshSession(
        Guid userId,
        Guid familyId,
        DateTime nowUtc)
    {
        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        return (
            new RefreshSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FamilyId = familyId,
                TokenHash = HashRefreshToken(token),
                CreatedAtUtc = nowUtc,
                ExpiresAtUtc = nowUtc.AddDays(
                    _jwtSettings.RefreshTokenExpiryDays),
                ConcurrencyToken = Guid.NewGuid()
            },
            token);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToHexString(bytes);
    }

    private static bool IsRefreshTokenValidShape(string refreshToken) =>
        !string.IsNullOrWhiteSpace(refreshToken)
        && refreshToken.Length <= MaxRefreshTokenLength;

    private static AuthResponse BuildAuthResponse(
        User user,
        string accessToken,
        DateTime accessExpiresAt,
        string refreshToken,
        DateTime refreshExpiresAt)
    {
        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken,
            RefreshExpiresAt = refreshExpiresAt,
            User = new UserSummaryResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role.ToString()
            },
            Wallet = new WalletSummaryResponse
            {
                Balance = user.Wallet!.Balance
            }
        };
    }

    private static bool IsUniqueViolation(
        DbUpdateException ex,
        string constraintName)
    {
        return ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == constraintName;
    }
}
