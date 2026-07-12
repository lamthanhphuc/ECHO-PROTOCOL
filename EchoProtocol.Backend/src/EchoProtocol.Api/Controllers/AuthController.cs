using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Auth;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<UserSummaryResponse>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                var status = AuthHttpStatusMapper.ToStatusCode(result.ErrorCode!);
                return StatusCode(status, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
            }

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<UserSummaryResponse>.Ok(result.Data!, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Action}", nameof(Register));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                var status = AuthHttpStatusMapper.ToStatusCode(result.ErrorCode!);
                return StatusCode(status, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
            }

            return Ok(ApiResponse<AuthResponse>.Ok(result.Data!, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Action}", nameof(Login));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<MeResponse>>> Me(CancellationToken cancellationToken)
    {
        try
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(claim, out var userId))
            {
                return Unauthorized(
                    ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
            }

            var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
            if (!result.IsSuccess)
            {
                var status = AuthHttpStatusMapper.ToStatusCode(result.ErrorCode!);
                return StatusCode(status, ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
            }

            return Ok(ApiResponse<MeResponse>.Ok(result.Data!, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Action}", nameof(Me));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
