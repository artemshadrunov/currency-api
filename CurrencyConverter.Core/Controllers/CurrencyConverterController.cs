using ApiCurrency.Models;
using ApiCurrency.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ApiCurrency.Controllers;

[ApiController]
[Route("api/v1/currencies")]
[Authorize]
public class CurrencyConverterController : ControllerBase
{
    private readonly ICurrencyConverterService _converterService;
    private readonly ILogger<CurrencyConverterController> _logger;

    public CurrencyConverterController(
        ICurrencyConverterService converterService,
        ILogger<CurrencyConverterController> logger)
    {
        _converterService = converterService;
        _logger = logger;
    }

    [HttpPost("convert")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<CurrencyConversionResult>> Convert([FromBody] CurrencyConversionRequest request)
    {
        try
        {
            var result = await _converterService.Convert(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting currency");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("latest")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetLatestRates([FromBody] LatestRatesRequest request)
    {
        try
        {
            var result = await _converterService.GetLatestRates(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest rates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("history")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<PagedRatesResult>> GetHistoricalRates([FromBody] HistoricalRatesRequest request)
    {
        try
        {
            var result = await _converterService.GetHistoricalRates(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting historical rates");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}