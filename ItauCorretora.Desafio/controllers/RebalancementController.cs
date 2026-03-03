using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RebalancementController : ControllerBase
{
    private readonly IRebalancementService _rebalancementService;

    public RebalancementController(IRebalancementService rebalancementService)
    {
        _rebalancementService = rebalancementService;
    }

    [HttpPost("all")]
    public async Task<IActionResult> RebalanceAll()
    {
        await _rebalancementService.RebalanceAllAsync();
        return Ok("Rebalancing in batch started.");
    }

    [HttpPost("customer/{customerId}")]
    public async Task<IActionResult> RebalanceCustomer(int customerId)
    {
        var result = await _rebalancementService.RebalanceCustomerAsync(customerId);
        if (result.Success)
            return Ok(result);
        else
            return BadRequest(result);
    }
}