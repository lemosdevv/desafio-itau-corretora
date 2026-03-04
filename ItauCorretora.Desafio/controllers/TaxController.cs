using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaxController : ControllerBase
{
    private readonly IIncomeTaxService _taxService;

    public TaxController(IIncomeTaxService taxService)
    {
        _taxService = taxService;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateMonthlyTax([FromQuery] int year, [FromQuery] int month)
    {
        await _taxService.CalculateMonthlyTaxAsync(year, month);
        return Ok($"Cálculo de IR para {year}-{month} concluído.");
    }

    [HttpPost("calculate/{customerId}")]
    public async Task<IActionResult> CalculateCustomerTax(int customerId, [FromQuery] int year, [FromQuery] int month)
    {
        var result = await _taxService.CalculateCustomerTaxAsync(customerId, year, month);
        return Ok(result);
    }
}