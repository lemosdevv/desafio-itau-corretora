using System.Text.Json;
using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using Microsoft.EntityFrameworkCore;
using static ItauCorretora.Desafio.Kafka.Consumers.OrderExecutedConsumer;

namespace ItauCorretora.Desafio.Kafka.Consumers;

public class OrderExecutedConsumer : KafkaConsumerBase<OrderExecutedMessage>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OrderExecutedConsumer(
        IConfiguration configuration,
        ILogger<OrderExecutedConsumer> logger,
        IServiceScopeFactory scopeFactory)
        : base(configuration, logger, "orders-executed")
    {
        _logger.LogInformation("Initializing OrderExecutedConsumer...");
        _scopeFactory = scopeFactory;
    }

    protected override async Task ProcessMessageAsync(string message, CancellationToken stoppingToken)
    {
        try
        {
            var orderExecuted = JsonSerializer.Deserialize<OrderExecutedMessage>(message, _jsonOptions);
            if (orderExecuted == null)
            {
                _logger.LogWarning("Invalid message received: {Message}", message);
                return;
            }

            _logger.LogInformation("Processing execution of order {OrderId} with status {Status}, executed quantity: {ExecutedQuantity}",
                orderExecuted.OrderId, orderExecuted.Status, orderExecuted.ExecutedQuantity);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seek order with all necessary relationships
            var order = await context.Orders
                .Include(o => o.Customer)
                .ThenInclude(c => c.Account)
                .Include(o => o.Stock)
                .FirstOrDefaultAsync(o => o.Id == orderExecuted.OrderId, stoppingToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found in the database", orderExecuted.OrderId);
                return;
            }

            // If order is a master order (no customer), we cannot process execution
            if (order.Customer == null)
            {
                _logger.LogWarning("Order {OrderId} is a master order and cannot be processed by this consumer", orderExecuted.OrderId);
                return;
            }

            // Use transactions to ensure consistency
            await using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                // Update order status
                order.Status = MapStatus(orderExecuted.Status);

                // If partially executed, use the actual executed quantity
                int executedQuantity = orderExecuted.ExecutedQuantity ?? order.Quantity;
                decimal executedValue = executedQuantity * order.Price;

                // Update customer position
                var position = await context.CustomerPositions
                    .FirstOrDefaultAsync(p => p.CustomerId == order.CustomerId && p.StockId == order.StockId, stoppingToken);

                if (order.Type == OrderType.Purchase)
                {
                    if (position == null)
                    {
                        position = new CustomerPosition
                        {
                            CustomerId = order.CustomerId.Value,
                            StockId = order.StockId,
                            Quantity = executedQuantity,
                            AveragePrice = order.Price
                        };
                        context.CustomerPositions.Add(position);
                    }
                    else
                    {
                        // Update quantity and average price
                        var totalValue = position.Quantity * position.AveragePrice + executedQuantity * order.Price;
                        position.Quantity += executedQuantity;
                        position.AveragePrice = totalValue / position.Quantity;
                    }

                    // If partially executed, refund the difference
                    if (orderExecuted.Status == "PARTIALLY_EXECUTED" && orderExecuted.ExecutedQuantity.HasValue)
                    {
                        var originalTotal = order.Quantity * order.Price;
                        var executedTotal = executedQuantity * order.Price;
                        var refund = originalTotal - executedTotal;

                        if (refund > 0 && order.Customer?.Account != null)
                        {
                            order.Customer.Account.Credit(refund);

                            // Record reversal movement
                            var refundMovement = new AccountMovement
                            {
                                AccountId = order.Customer.Account.Id,
                                Date = DateTime.Now,
                                Type = TipoMovimento.Credito,
                                Value = refund,
                                Description = $"Partial reversal of order {order.Id}",
                                OrderId = order.Id
                            };
                            context.AccountMovements.Add(refundMovement);
                        }
                    }
                }
                else // Sale
                {
                    if (position == null)
                    {
                        _logger.LogWarning("Client {CustomerId} does not have a position for asset {StockCode} to sell",
                            order.CustomerId, order.Stock!.Code);
                        order.Status = StatusOrder.Error;
                    }
                    else
                    {
                        if (position.Quantity < executedQuantity)
                        {
                            _logger.LogWarning("Client {CustomerId} tried to sell {ExecutedQuantity} but only has {CurrentQuantity} of {StockCode}",
                                order.CustomerId, executedQuantity, position.Quantity, order.Stock!.Code);
                            order.Status = StatusOrder.Error;
                        }
                        else
                        {
                            // Reduce position (average price unchanged)
                            position.Quantity -= executedQuantity;

                            // Credit the account
                            if (order.Customer?.Account != null)
                            {
                                order.Customer.Account.Credit(executedValue);

                                // Record credit movement
                                var creditMovement = new AccountMovement
                                {
                                    AccountId = order.Customer.Account.Id,
                                    Date = DateTime.Now,
                                    Type = TipoMovimento.Credito,
                                    Value = executedValue,
                                    Description = $"Sale of {executedQuantity} {order.Stock!.Code}",
                                    OrderId = order.Id
                                };
                                context.AccountMovements.Add(creditMovement);
                            }

                            // If position becomes zero, remove it
                            if (position.Quantity == 0)
                            {
                                context.CustomerPositions.Remove(position);
                            }
                        }
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);

                _logger.LogInformation("Order {OrderId} processed successfully. Balance updated.", orderExecuted.OrderId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(stoppingToken);
                _logger.LogError(ex, "Error processing order {OrderId}", orderExecuted.OrderId);
                throw; // Re-throw to prevent commit
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing message: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing executed order message");
        }
    }

    private StatusOrder MapStatus(string status) => status switch
    {
        "EXECUTED" => StatusOrder.Executed,
        "PARTIALLY_EXECUTED" => StatusOrder.PartiallyExecuted,
        "REJECTED" => StatusOrder.Rejected,
        _ => StatusOrder.Error
    };

    public class OrderExecutedMessage
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ExecutedQuantity { get; set; }
        public decimal? ExecutedPrice { get; set; }
    }
}