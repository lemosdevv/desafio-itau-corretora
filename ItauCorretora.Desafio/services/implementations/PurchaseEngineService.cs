using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Services.Implementations;

public class PurchaseEngineService : IPurchaseEngineService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PurchaseEngineService> _logger;

    public PurchaseEngineService(AppDbContext context, ILogger<PurchaseEngineService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PurchaseResult> ProcessPurchaseAsync(int customerId, decimal amount)
    {
        // 1. Validate Customer and balance
        var customer = await _context.Customers
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            return new PurchaseResult { Success = false, Message = "Customer not found" };

        if (customer.Account == null || customer.Account.Balance < amount)
            return new PurchaseResult { Success = false, Message = "Insufficient balance" };

        // 2. Search for active recommended wallet
        var activeWallet = await _context.RecommendedWallets
            .Include(w => w.Itens)
            .ThenInclude(i => i.Stock)
            .Where(w => w.StartDate <= DateTime.Now && (!w.EndDate.HasValue || w.EndDate > DateTime.Now))
            .FirstOrDefaultAsync();

        if (activeWallet == null)
            return new PurchaseResult { Success = false, Message = "No active recommended wallet found" };

        // 3. Search current quotes
        var today = DateTime.Today;
        var quotes = await _context.Quotes
            .Where(q => q.Date == today)
            .ToListAsync();

        var orders = new List<GeneratedOrder>();
        decimal totalInvested = 0;

        // 4. Distribute the amount according to the wallet weights
        foreach (var item in activeWallet.Itens)
        {
            var quote = quotes.FirstOrDefault(q => q.StockId == item.StockId);
            if (quote == null) continue;

            var allocatedAmount = amount * item.Weight;
            var quantity = (int)(allocatedAmount / quote.ClosePrice);

            if (quantity > 0)
            {
                var orderValue = quantity * quote.ClosePrice;
                totalInvested += orderValue;

                orders.Add(new GeneratedOrder
                {
                    StockId = item.StockId,
                    StockCode = item.Stock.Code,
                    Quantity = quantity,
                    Price = quote.ClosePrice,
                    TotalValue = orderValue
                });
            }
        }

        // 5. Update balance (in a transaction)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            customer.Account.Balance -= totalInvested;

            // Record movement in the account
            var movement = new AccountMovement
            {
                AccountId = customer.Account.Id,
                Date = DateTime.Now,
                Type = TipoMovimento.Debito,
                Value = totalInvested,
                Description = "Scheduled stock purchase"
            };
            _context.AccountMovements.Add(movement);

            // Create orders
            foreach (var order in orders)
            {
                var newOrder = new Order
                {
                    CustomerId = customerId,
                    StockId = order.StockId,
                    Date = DateTime.Now,
                    Type = OrderType.Purchase,
                    Quantity = order.Quantity,
                    Price = order.Price,
                    Status = StatusOrder.WaitingForExecution,
                };
                _context.Orders.Add(newOrder);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Purchase successfully processed for customer {CustomerId}, Invested total: {TotalInvested}", customerId, totalInvested);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing purchase for customer {CustomerId}", customerId);
            return new PurchaseResult { Success = false, Message = "Error processing purchase" };
        }

        return new PurchaseResult
        {
            Success = true,
            Message = "Purchase processed successfully",
            Orders = orders,
            TotalInvested = totalInvested
        };
    }
}