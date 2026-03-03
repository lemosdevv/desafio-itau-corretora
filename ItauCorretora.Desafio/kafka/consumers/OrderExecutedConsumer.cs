using System.Text.Json;
using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Desafio.Kafka.Consumers;

public class OrderExecutedConsumer : KafkaConsumerBase<OrderExecutedMessage>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderExecutedConsumer(
        IConfiguration configuration,
        ILogger<OrderExecutedConsumer> logger,
        IServiceScopeFactory scopeFactory)
        : base(configuration, logger, "orders-executed")
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ProcessMessageAsync(string message, CancellationToken stoppingToken)
    {
        try
        {
            var orderExecuted = JsonSerializer.Deserialize<OrderExecutedMessage>(message);
            if (orderExecuted == null)
            {
                _logger.LogWarning("Invalid message received.: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing order execution {OrderId} with status {Status}", 
                orderExecuted.OrderId, orderExecuted.Status);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var order = await context.Orders.FindAsync(orderExecuted.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found in the database", orderExecuted.OrderId);
                return;
            }

            // Updates order status
            order.Status = orderExecuted.Status switch
            {
                "EXECUTED" => StatusOrder.Executed,
                "PARTIALLY_EXECUTED" => StatusOrder.PartiallyExecuted,
                "REJECTED" => StatusOrder.Rejected,
                _ => order.Status
            };

            // If the order has been executed, we can update the client's position.
            if (orderExecuted.Status == "EXECUTED" || orderExecuted.Status == "PARTIALLY_EXECUTED")
            {
                // Updates the customer's position. (CustomerPosition)
                var position = await context.CustomerPositions
                    .FirstOrDefaultAsync(p => p.CustomerId == order.CustomerId && p.StockId == order.StockId);

                if (position == null)
                {
                    position = new CustomerPosition
                    {
                        CustomerId = order.CustomerId,
                        StockId = order.StockId,
                        Quantity = order.Quantity,
                        AveragePrice = order.Price
                    };
                    context.CustomerPositions.Add(position);
                }
                else
                {
                    // Update quantity and average price.
                    var totalValue = position.Quantity * position.AveragePrice + order.Quantity * order.Price;
                    position.Quantity += order.Quantity;
                    position.AveragePrice = totalValue / position.Quantity;
                }
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} updated successfully", orderExecuted.OrderId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing message: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing executed order message");
        }
    }
}

public class OrderExecutedMessage
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty; // EXECUTED, PARTIALLY_EXECUTED, REJECTED
    public int? ExecutedQuantity { get; set; }
    public decimal? ExecutedPrice { get; set; }
}