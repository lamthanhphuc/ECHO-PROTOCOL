using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Auth;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ServiceResult<UserSummaryResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (request.Password.Length < 6)
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (request.Password != request.ConfirmPassword)
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Password confirmation does not match",
                ErrorCodes.PasswordConfirmationMismatch);
        }

        var normalized = UsernameNormalizer.Normalize(request.Username);
        var displayName = request.Username.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == normalized, cancellationToken))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Username already exists",
                ErrorCodes.UsernameAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = normalized,
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
        catch (DbUpdateException ex) when (IsUsernameUniqueViolation(ex))
        {
            return ServiceResult<UserSummaryResponse>.Failure(
                "Username already exists",
                ErrorCodes.UsernameAlreadyExists);
        }

        return ServiceResult<UserSummaryResponse>.Success(
            new UserSummaryResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString()
            },
            "Register successfully");
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<AuthResponse>.Failure(
                "Validation failed",
                ErrorCodes.ValidationError);
        }

        var normalized = UsernameNormalizer.Normalize(request.Username);

        var user = await _db.Users
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        if (user is null)
        {
            return ServiceResult<AuthResponse>.Failure(
                "Invalid username or password",
                ErrorCodes.InvalidCredentials);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
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

        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateToken(user);

        return ServiceResult<AuthResponse>.Success(
            new AuthResponse
            {
                AccessToken = accessToken,
                ExpiresAt = expiresAtUtc,
                User = new UserSummaryResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Role = user.Role.ToString()
                },
                Wallet = new WalletSummaryResponse
                {
                    Balance = user.Wallet.Balance
                }
            },
            "Login successfully");
    }

    public async Task<ServiceResult<MeResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.PlayerProfile)
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<MeResponse>.Failure(
                "User not found",
                ErrorCodes.NotFound);
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
                Username = user.Username,
                Role = user.Role.ToString(),
                DisplayName = user.PlayerProfile.DisplayName,
                WalletBalance = user.Wallet.Balance
            },
            "Current user loaded");
    }

    private static bool IsUsernameUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == "IX_Users_Username";
    }
}
