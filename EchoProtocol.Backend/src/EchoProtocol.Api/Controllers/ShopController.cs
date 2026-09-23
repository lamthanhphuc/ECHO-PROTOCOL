using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Route("api/shop")]
public sealed class ShopController : ControllerBase
{
    private readonly IShopCatalogService _service;
    private readonly ILogger<ShopController> _logger;

    public ShopController(
        IShopCatalogService service,
        ILogger<ShopController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("items")]
    public async Task<ActionResult<ApiResponse<ShopCatalogResponse>>> GetItems(
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ShopCatalogService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetItemsAsync(
                category,
                page,
                pageSize,
                cancellationToken);
            if (!result.IsSuccess)
            {
                return BadRequest(ApiResponse<object>.Fail(result.Message, result.ErrorCode!));
            }

            return Ok(ApiResponse<ShopCatalogResponse>.Ok(result.Data!, result.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error in {Action}", nameof(GetItems));
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Internal server error", ErrorCodes.InternalServerError));
        }
    }
}
