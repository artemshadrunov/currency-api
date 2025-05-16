using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CurrencyConverter.Core.Controllers;

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
        _logger.LogInformation("Starting currency conversion for {FromCurrency} to {ToCurrency}",
            request.FromCurrency, request.ToCurrency);
        try
        {
            var result = await _converterService.Convert(request);
            _logger.LogInformation("Successfully converted {Amount} {FromCurrency} to {ToCurrency}",
                request.Amount, request.FromCurrency, request.ToCurrency);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid conversion request: {FromCurrency} to {ToCurrency}",
                request.FromCurrency, request.ToCurrency);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            _logger.LogWarning(ex, "Forbidden conversion attempt: {FromCurrency} to {ToCurrency}",
                request.FromCurrency, request.ToCurrency);
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            _logger.LogError("External API is unavailable for conversion {FromCurrency} to {ToCurrency}",
                request.FromCurrency, request.ToCurrency);
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting currency from {FromCurrency} to {ToCurrency}",
                request.FromCurrency, request.ToCurrency);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("latest")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetLatestRates([FromBody] LatestRatesRequest request)
    {
        _logger.LogInformation("Getting latest rates for base currency {BaseCurrency}", request.BaseCurrency);
        try
        {
            var result = await _converterService.GetLatestRates(request);
            _logger.LogInformation("Successfully retrieved latest rates for {BaseCurrency}", request.BaseCurrency);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid latest rates request for {BaseCurrency}", request.BaseCurrency);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            _logger.LogWarning(ex, "Forbidden latest rates request for {BaseCurrency}", request.BaseCurrency);
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            _logger.LogError("External API is unavailable for latest rates request for {BaseCurrency}",
                request.BaseCurrency);
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest rates for {BaseCurrency}", request.BaseCurrency);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("history")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<PagedRatesResult>> GetHistoricalRates([FromBody] HistoricalRatesRequest request)
    {
        _logger.LogInformation("Getting historical rates for {BaseCurrency} from {StartDate} to {EndDate}",
            request.BaseCurrency, request.Start, request.End);
        try
        {
            var result = await _converterService.GetHistoricalRates(request);
            _logger.LogInformation("Successfully retrieved historical rates for {BaseCurrency}", request.BaseCurrency);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid historical rates request for {BaseCurrency}", request.BaseCurrency);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("is excluded from conversion"))
        {
            _logger.LogWarning(ex, "Forbidden historical rates request for {BaseCurrency}", request.BaseCurrency);
            return Forbid(ex.Message);
        }
        catch (HttpRequestException)
        {
            _logger.LogError("External API is unavailable for historical rates request for {BaseCurrency}",
                request.BaseCurrency);
            return StatusCode(502, new { error = "External API is unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting historical rates for {BaseCurrency}", request.BaseCurrency);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}