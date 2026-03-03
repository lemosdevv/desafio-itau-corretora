using System.Text.Json;
using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static ItauCorretora.Desafio.Kafka.Consumers.OrderExecutedConsumer;

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
                _logger.LogWarning("Invalid message received: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing execution of order {OrderId} with status {Status}, executed quantity: {ExecutedQuantity}",
                orderExecuted.OrderId, orderExecuted.Status, orderExecuted.ExecutedQuantity);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seeking order in all necessary relationships
            var order = await context.Orders
                .Include(o => o.Customer)
                .ThenInclude(c => c.Account)
                .Include(o => o.Stock)
                .FirstOrDefaultAsync(o => o.Id == orderExecuted.OrderId);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found in the database", orderExecuted.OrderId);
                return;
            }

            // Use transactions to ensure consistency.
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // Update order status
                order.Status = MapStatus(orderExecuted.Status);
                
                // If it was partially executed, we keep track of the amount executed.
                int executedQuantity = orderExecuted.ExecutedQuantity ?? order.Quantity;
                decimal executedValue = executedQuantity * order.Price;

                // Update customer status
                var position = await context.CustomerPositions
                    .FirstOrDefaultAsync(p => p.CustomerId == order.CustomerId && p.StockId == order.StockId);

                if (order.Type == OrderType.Purchase)
                {
                    if (position == null)
                    {
                        position = new CustomerPosition
                        {
                            CustomerId = order.CustomerId,
                            StockId = order.StockId,
                            Quantity = executedQuantity,
                            AveragePrice = order.Price
                        };
                        context.CustomerPositions.Add(position);
                    }
                    else
                    {
                        // Update quantity and average price.
                        var totalValue = position.Quantity * position.AveragePrice + executedQuantity * order.Price;
                        position.Quantity += executedQuantity;
                        position.AveragePrice = totalValue / position.Quantity;
                    }

                    // If the order was partially executed, we need to adjust the balance (refund the difference)
                    if (orderExecuted.Status == "PARTIALLY_EXECUTED" && orderExecuted.ExecutedQuantity.HasValue)
                    {
                        var originalTotal = order.Quantity * order.Price;
                        var executedTotal = executedQuantity * order.Price;
                        var refund = originalTotal - executedTotal;

                        if (refund > 0)
                        {
                            order.Customer.Account.Credit(refund);
                            
                            // Record reversal transaction
                            var refundMovement = new AccountMovement
                            {
                                AccountId = order.Customer.Account.Id,
                                Date = DateTime.Now,
                                Type = TipoMovimento.Credito,
                                Value = refund,
                                Description = $"Partial reversal of the order {order.Id}",
                                OrderId = order.Id
                            };
                            context.AccountMovements.Add(refundMovement);
                        }
                    }
                }
                else // SALE
                {
                    if (position == null)
                    {
                        _logger.LogWarning("Client {CustomerId} does not have a position for the asset {StockCode} to sell",
                            order.CustomerId, order.Stock.Code);
                        order.Status = StatusOrder.Error;
                    }
                    else
                    {
                        if (position.Quantity < executedQuantity)
                        {
                            _logger.LogWarning("Client {CustomerId} tried to sell {ExecutedQuantity} but only has {CurrentQuantity} of {StockCode}",
                                order.CustomerId, executedQuantity, position.Quantity, order.Stock.Code);
                            order.Status = StatusOrder.Error;
                        }
                        else
                        {
                            // Reduce the position (average price does not change on sale)
                            position.Quantity -= executedQuantity;

                            // Credit the value to the account
                            order.Customer.Account.Credit(executedValue);

                            // Record credit transaction
                            var creditMovement = new AccountMovement
                            {
                                AccountId = order.Customer.Account.Id,
                                Date = DateTime.Now,
                                Type = TipoMovimento.Credito,
                                Value = executedValue,
                                Description = $"Sale of {executedQuantity} {order.Stock.Code}",
                                OrderId = order.Id
                            };
                            context.AccountMovements.Add(creditMovement);

                            // If the position is depleted, we can remove it or keep it with quantity 0
                            if (position.Quantity == 0)
                            {
                                context.CustomerPositions.Remove(position);
                            }
                        }
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Order {OrderId} processed successfully. Balance updated.", orderExecuted.OrderId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing order {OrderId}", orderExecuted.OrderId);
                throw; // Re-throw to prevent the message from being committed (depending on the commit strategy).
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing message: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while processing executed order message");
        }
    }

    private StatusOrder MapStatus(string status) => status switch
    {
        "EXECUTED" => StatusOrder.Executed,
        "PARTIALLY_EXECUTED" => StatusOrder.PartiallyExecuted,
        "REJECTED" => StatusOrder.Rejected,
        _ => StatusOrder.Error
    };

    public class OrderExecutedMessage{
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty; // EXECUTED, PARTIALLY_EXECUTED, REJECTED
        public int? ExecutedQuantity { get; set; }
        public decimal? ExecutedPrice { get; set; }
        }
}