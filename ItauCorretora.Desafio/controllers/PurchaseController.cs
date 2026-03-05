using ItauCorretora.Desafio.Kafka.Producers;
using ItauCorretora.Desafio.Services.Implementations;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseController : ControllerBase
{
    private readonly IPurchaseEngineService _purchaseService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IConsolidatedPurchaseService _consolidatedPurchaseService;

    public PurchaseController(IPurchaseEngineService purchaseService, IKafkaProducer kafkaProducer, IConsolidatedPurchaseService consolidatedPurchaseService)
    {
        _purchaseService = purchaseService;
        _kafkaProducer = kafkaProducer;
        _consolidatedPurchaseService = consolidatedPurchaseService;
    }

    [HttpPost("{customerId}/process")]
    public async Task<IActionResult> ProcessPurchase(int customerId, [FromBody] PurchaseRequest request)
    {
        var result = await _purchaseService.ProcessPurchaseAsync(customerId, request.Amount);

        if (!result.Success)
            return BadRequest(result);

        await _kafkaProducer.ProduceAsync("orders-creates", new
        {
            CustomerId = customerId,
            Date = DateTime.Now,
            Orders = result.Orders
        });

        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecutarCompra([FromQuery] DateTime? referenceDate = null)
    {
        var result = await _consolidatedPurchaseService.ExecutePurchaseAsync(referenceDate);
        if (result.Success)
            return Ok(result);
        else
            return BadRequest(result);
    }
    
}

public class PurchaseRequest
{
    public decimal Amount { get; set; }
}

public class ExecutePurchaseRequest
{
    public DateTime? DataReferencia { get; set; }
}