using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Telemetry;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        ITelemetryService telemetryService,
        ILogger<TelemetryController> logger)
    {
        _telemetryService = telemetryService;
        _logger = logger;
    }

    [HttpPost("batch")]
    public async Task<ActionResult<ApiResponse<TelemetryBatchResponse>>> IngestBatch(
        [FromBody] TelemetryBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(claim, out var authenticatedUserId))
            {
                return Unauthorized(
                    ApiResponse<object>.Fail("Invalid token", ErrorCodes.TokenInvalid));
            }

            var result = await _telemetryService.IngestBatchAsync(
                request,
                authenticatedUserId,
                cancellationToken);
            if (!result.IsSuccess)
            {
                var response = ApiResponse<object>.Fail(result.Message, result.ErrorCode!);
                return result.ErrorCode == ErrorCodes.TelemetryUserMismatch
                    ? StatusCode(StatusCodes.Status403Forbidden, response)
                    : BadRequest(response);
            }

            return Ok(ApiResponse<TelemetryBatchResponse>.Ok(result.Data!, result.Message));
        }
        catch (MongoException ex)
        {
            _logger.LogWarning(ex, "MongoDB unavailable in {Action}", nameof(IngestBatch));
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    "Telemetry storage is temporarily unavailable",
                    ErrorCodes.TelemetryUnavailable));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "MongoDB timed out in {Action}", nameof(IngestBatch));
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    "Telemetry storage is temporarily unavailable",
                    ErrorCodes.TelemetryUnavailable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Action}", nameof(IngestBatch));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
