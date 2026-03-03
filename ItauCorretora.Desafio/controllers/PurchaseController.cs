using ItauCorretora.Desafio.Kafka.Producers;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseController : ControllerBase
{
    private readonly IPurchaseEngineService _purchaseService;
    private readonly IKafkaProducer _kafkaProducer;

    public PurchaseController(IPurchaseEngineService purchaseService, IKafkaProducer kafkaProducer)
    {
        _purchaseService = purchaseService;
        _kafkaProducer = kafkaProducer;
    }

    [HttpPost("{customerId}/process")]
    public async Task<IActionResult> ProcessPurchase(int customerId, [FromBody] PurchaseRequest request)
    {
        var result = await _purchaseService.ProcessPurchaseAsync(customerId, request.Amount);

        if (!result.Success)
            return BadRequest(result);

        await _kafkaProducer.ProduceAsync("ordens-criadas", new
        {
            CustomerId = customerId,
            Date = DateTime.Now,
            Orders = result.Orders
        });

        return Ok(result);
    }
}

public class PurchaseRequest
{
    public decimal Amount { get; set; }
}