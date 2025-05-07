using ApiCurrency.Models;
using ApiCurrency.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiCurrency.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var result = await _converterService.ConvertAsync(request);
        return Ok(result);
    }
} 