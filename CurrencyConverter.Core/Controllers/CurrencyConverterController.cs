using ApiCurrency.Models;
using ApiCurrency.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace ApiCurrency.Controllers;

[ApiController]
[Route("api/v1/currencies")]
public class CurrencyConverterController : ControllerBase
{
    private readonly ICurrencyConverterService _converterService;

    public CurrencyConverterController(ICurrencyConverterService converterService)
    {
        _converterService = converterService;
    }

    [HttpPost("convert")]
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
        catch (Exception)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("latest")]
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
        catch (Exception)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("history")]
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
        catch (Exception)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}