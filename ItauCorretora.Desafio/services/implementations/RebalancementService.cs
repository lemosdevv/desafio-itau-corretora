using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Services.Implementations;

public class RebalancementService : IRebalancementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RebalancementService> _logger;

    public RebalancementService(AppDbContext context, ILogger<RebalancementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RebalanceAllAsync()
    {
        var customers = await _context.Customers
            .Include(c => c.Account)
            .Include(c => c.Positions)
            .ThenInclude(p => p.Stock)
            .ToListAsync();

        foreach (var customer in customers)
        {
            try
            {
                await RebalanceCustomerAsync(customer.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in customer rebalancing. {CustomerId}", customer.Id);
            }
        }
    }

    public async Task<RebalancementResult> RebalanceCustomerAsync(int customerId)
    {
        var result = new RebalancementResult();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var customer = await _context.Customers
                .Include(c => c.Account)
                .Include(c => c.Positions)
                .ThenInclude(p => p.Stock)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null || customer.Account == null)
            {
                result.Message = "Customer or account not found.";
                return result;
            }

            // Buscar carteira recomendada ativa
            var activeWallet = await _context.RecommendedWallets
                .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
                .Where(w => w.StartDate <= DateTime.Now && (!w.EndDate.HasValue || w.EndDate > DateTime.Now))
                .FirstOrDefaultAsync();

            if (activeWallet == null)
            {
                result.Message = "No active recommended wallet found.";
                return result;
            }

            // View current quotes (last closing price)
            var today = DateTime.Today;
            var quotes = await _context.Quotes
                .Where(q => q.Date == today)
                .ToDictionaryAsync(q => q.StockId);

            // Calculate total assets
            decimal totalEquity = customer.Positions.Sum(p => p.Quantity * (quotes.ContainsKey(p.StockId) ? quotes[p.StockId].ClosePrice : 0));

            if (totalEquity <= 0)
            {
                result.Message = "Total assets zero, nothing to rebalance.";
                result.Success = true;
                return result;
            }

            // Dictionary of current positions
            var currentPositions = customer.Positions.ToDictionary(p => p.StockId);

            foreach (var item in activeWallet.Itens)
            {
                if (!quotes.ContainsKey(item.StockId)) continue;

                var currentPrice = quotes[item.StockId].ClosePrice;
                var targetValue = totalEquity * (item.Weight / 100m); // item.Weight é percentual (ex: 30), então dividir por 100
                var targetQuantity = (int)(targetValue / currentPrice);

                var currentQuantity = currentPositions.ContainsKey(item.StockId) ? currentPositions[item.StockId].Quantity : 0;

                if (targetQuantity > currentQuantity)
                {
                    // need to buy
                    var buyQuantity = targetQuantity - currentQuantity;
                    var orderValue = buyQuantity * currentPrice;

                    // Check balance
                    if (customer.Account.Balance < orderValue)
                    {
                        _logger.LogWarning("Customer {CustomerId} has insufficient balance to buy {Quantity} shares of {StockCode}", 
                            customerId, buyQuantity, item.Stock.Code);
                        continue;
                    }

                    var order = new Order
                    {
                        CustomerId = customerId,
                        StockId = item.StockId,
                        Date = DateTime.Now,
                        Type = OrderType.Purchase,
                        Quantity = buyQuantity,
                        Price = currentPrice,
                        Status = StatusOrder.WaitingForExecution
                    };
                    _context.Orders.Add(order);

                    // Debit balance (provisionally)
                    customer.Account.Balance -= orderValue;

                    result.BuyOrders.Add(new GeneratedOrder
                    {
                        StockId = item.StockId,
                        StockCode = item.Stock.Code,
                        Quantity = buyQuantity,
                        Price = currentPrice,
                        TotalValue = orderValue
                    });
                    result.TotalCost += orderValue;
                }
                else if (targetQuantity < currentQuantity)
                {
                    // need to sell
                    var sellQuantity = currentQuantity - targetQuantity;
                    var orderValue = sellQuantity * currentPrice;

                    var order = new Order
                    {
                        CustomerId = customerId,
                        StockId = item.StockId,
                        Date = DateTime.Now,
                        Type = OrderType.Sale,
                        Quantity = sellQuantity,
                        Price = currentPrice,
                        Status = StatusOrder.WaitingForExecution
                    };
                    _context.Orders.Add(order);

                    // The balance will be credited when the order is executed (consumer).
                    result.SellOrders.Add(new GeneratedOrder
                    {
                        StockId = item.StockId,
                        StockCode = item.Stock.Code,
                        Quantity = sellQuantity,
                        Price = currentPrice,
                        TotalValue = orderValue
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            result.Success = true;
            result.Message = "Rebalancing completed successfully.";
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error occurred while rebalancing customer {CustomerId}", customerId);
            result.Message = $"Error: {ex.Message}";
            return result;
        }
    }

    public async Task RebalanceByWalletChangeAsync(int newWalletId)
    {
        var newWallet = await _context.RecommendedWallets
            .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
            .FirstOrDefaultAsync(w => w.Id == newWalletId);

        if (newWallet == null)
            throw new ArgumentException("Wallet not found");

        // Get the previous wallet (last deactivated)
        var previousWallet = await _context.RecommendedWallets
            .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
            .Where(w => !w.Active && w.EndDate.HasValue)
            .OrderByDescending(w => w.EndDate)
            .FirstOrDefaultAsync();

        if (previousWallet == null)
        {
            _logger.LogInformation("No previous wallet found. Nothing to rebalance.");
            return;
        }

        var oldTickers = previousWallet.Itens.Select(i => i.Stock.Code).ToHashSet();
        var newTickers = newWallet.Itens.Select(i => i.Stock.Code).ToHashSet();

        var tickersToSell = oldTickers.Except(newTickers).ToList();
        var tickersToBuy = newTickers.Except(oldTickers).ToList();

        // Get current quotes
        var quotes = await _context.Quotes
            .GroupBy(q => q.StockId)
            .Select(g => g.OrderByDescending(q => q.Date).FirstOrDefault())
            .ToDictionaryAsync(q => q.StockId, q => q);

        var activeCustomers = await _context.Customers
            .Include(c => c.Positions)
                .ThenInclude(p => p.Stock)
            .Where(c => c.Active)
            .ToListAsync();

        foreach (var customer in activeCustomers)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalSellValue = 0;

                // Sell assets that left the wallet
                foreach (var ticker in tickersToSell)
                {
                    var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == ticker);
                    if (stock == null) continue;

                    var position = customer.Positions.FirstOrDefault(p => p.StockId == stock.Id);
                    if (position == null || position.Quantity == 0) continue;

                    var quote = quotes.GetValueOrDefault(stock.Id);
                    if (quote == null) continue;

                    var sellValue = position.Quantity * quote.ClosePrice;
                    totalSellValue += sellValue;

                    var order = new Order
                    {
                        CustomerId = customer.Id,
                        StockId = stock.Id,
                        Date = DateTime.Now,
                        Type = OrderType.Sale,
                        Quantity = position.Quantity,
                        Price = quote.ClosePrice,
                        Status = StatusOrder.WaitingForExecution
                    };
                    _context.Orders.Add(order);
                }

                // Buy new assets (if any sell value)
                if (totalSellValue > 0 && tickersToBuy.Any())
                {
                    // Calculate total percentage of new assets (those entering)
                    var totalBuyPercent = tickersToBuy.Sum(t => newWallet.Itens.First(i => i.Stock.Code == t).Weight);

                    foreach (var ticker in tickersToBuy)
                    {
                        var item = newWallet.Itens.First(i => i.Stock.Code == ticker);
                        var stock = item.Stock;
                        var quote = quotes.GetValueOrDefault(stock.Id);
                        if (quote == null) continue;

                        var amountToInvest = totalSellValue * (item.Weight / totalBuyPercent);
                        var quantity = (int)(amountToInvest / quote.ClosePrice);
                        if (quantity > 0)
                        {
                            var order = new Order
                            {
                                CustomerId = customer.Id,
                                StockId = stock.Id,
                                Date = DateTime.Now,
                                Type = OrderType.Purchase,
                                Quantity = quantity,
                                Price = quote.ClosePrice,
                                Status = StatusOrder.WaitingForExecution
                            };
                            _context.Orders.Add(order);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during wallet change rebalance for customer {CustomerId}", customer.Id);
            }
        }
    }
}