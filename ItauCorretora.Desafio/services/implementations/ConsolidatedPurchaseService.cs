using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Kafka.Producers;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ItauCorretora.Desafio.Services.Implementations;

public class ConsolidatedPurchaseService : IConsolidatedPurchaseService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConsolidatedPurchaseService> _logger;
    private readonly IKafkaProducer _kafkaProducer;

    public ConsolidatedPurchaseService(
        AppDbContext context,
        ILogger<ConsolidatedPurchaseService> logger,
        IKafkaProducer kafkaProducer)
    {
        _context = context;
        _logger = logger;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<ConsolidatedPurchaseResult> ExecutePurchaseAsync(DateTime? referenceDate = null)
{
    var result = new ConsolidatedPurchaseResult();
    var date = referenceDate ?? DateTime.Today;

    // Check if it is a business day and if it is one of the shopping days (5, 15, 25 or next business day).
    if (!IsPurchaseDay(date))
    {
        result.Success = false;
        result.Message = $"Today ({date:dd/MM/yyyy}) is not a scheduled purchase day.";
        return result;
    }

    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        // 1. Search for active clients
        var activeCustomers = await _context.Customers
            .Where(c => c.Active)
            .Include(c => c.Account)
            .ToListAsync();

        if (!activeCustomers.Any())
        {
            result.Success = false;
            result.Message = "No active clients found.";
            return result;
        }

        // 2. Calculate the total to be invested (1/3 of the monthly value of each)
        decimal totalAmount = 0;
        var customerInvestments = new Dictionary<int, decimal>();
        foreach (var customer in activeCustomers)
        {
            var amount = customer.MonthlyValue / 3m;
            customerInvestments[customer.Id] = amount;
            totalAmount += amount;
        }
        result.TotalAmount = totalAmount;
        result.TotalClients = activeCustomers.Count;

        // 3. Get active basket
        var activeWallet = await _context.RecommendedWallets
            .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
            .Where(w => w.Active)
            .FirstOrDefaultAsync();

        if (activeWallet == null)
        {
            result.Success = false;
            result.Message = "No active basket found.";
            return result;
        }

        // 4. Get the most recent closing quotes (last trading day)
        var quotes = await _context.Quotes
            .Where(q => q.Date <= date)
            .GroupBy(q => q.StockId)
            .Select(g => g.OrderByDescending(q => q.Date).FirstOrDefault())
            .ToDictionaryAsync(q => q.StockId, q => q.ClosePrice);

        if (!quotes.Any())
        {
            result.Success = false;
            result.Message = "No closing quotes available.";
            return result;
        }

        // 5. Calculate total quantities to purchase per stock
        var purchaseItems = new Dictionary<int, (decimal amount, int quantity, decimal price)>();
        foreach (var item in activeWallet.Itens)
        {
            var amount = totalAmount * (item.Weight / 100m);
            if (!quotes.TryGetValue(item.StockId, out var price))
            {
                _logger.LogWarning("No quote found for the asset {StockCode}. Skipping.", item.Stock.Code);
                continue;
            }
            if (price <= 0)
            {
                _logger.LogWarning("Invalid price (zero or negative) for asset {StockCode}. Skipping.", item.Stock.Code);
                continue;
            }
            var quantity = (int)(amount / price);
            if (quantity > 0)
            {
                purchaseItems[item.StockId] = (amount, quantity, price);
            }
        }

        // 6. Check master account and custody
        var masterAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Type == AccountType.Master);
        if (masterAccount == null)
        {
            result.Success = false;
            result.Message = "Master account not found.";
            return result;
        }

        var masterCustodies = await _context.MasterCustodies
            .Include(m => m.Stock)
            .ToDictionaryAsync(m => m.StockId);

        // 7. Prepare final purchase list (considering master balance)
        var finalPurchase = new List<OrderSummary>();
        foreach (var item in purchaseItems)
        {
            var stockId = item.Key;
            var (amount, quantity, price) = item.Value;

            // If there is master custody, we will add it later, not subtract now.
            finalPurchase.Add(new OrderSummary
            {
                StockCode = (await _context.Stocks.FindAsync(stockId))?.Code ?? "",
                Quantity = quantity,
                Price = price,
                TotalValue = quantity * price,
                Details = SplitIntoLotesFracionario(quantity, (await _context.Stocks.FindAsync(stockId))?.Code ?? "")
            });
        }
        result.Orders = finalPurchase;

        // 8. Register master order (consolidated purchase)
        foreach (var order in finalPurchase)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == order.StockCode);
            if (stock == null) continue;

            var orderEntity = new Order
            {
                AccountId = masterAccount.Id,
                CustomerId = null, // <-- adicione esta linha
                StockId = stock.Id,
                Date = DateTime.Now,
                Type = OrderType.Purchase,
                Quantity = order.Quantity,
                Price = order.Price,
                Status = StatusOrder.Executed,
            };
            _context.Orders.Add(orderEntity);
        }
        await _context.SaveChangesAsync();

        // 9. Update master custody (add purchases)
        foreach (var order in finalPurchase)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == order.StockCode);
            if (stock == null) continue;

            var masterCustody = await _context.MasterCustodies
                .FirstOrDefaultAsync(m => m.StockId == stock.Id);
            if (masterCustody == null)
            {
                masterCustody = new MasterCustody
                {
                    StockId = stock.Id,
                    Quantity = order.Quantity,
                    AveragePrice = order.Price
                };
                _context.MasterCustodies.Add(masterCustody);
            }
            else
            {
                var oldQuantity = masterCustody.Quantity;
                masterCustody.Quantity += order.Quantity;
                if (oldQuantity == 0)
                {
                    masterCustody.AveragePrice = order.Price;
                }
                else
                {
                    masterCustody.AveragePrice = (masterCustody.AveragePrice * oldQuantity + order.Price * order.Quantity) / masterCustody.Quantity;
                }
            }
        }
        await _context.SaveChangesAsync();

        // 10. Distribute to clients
        var distributions = new List<ClientDistribution>();
        foreach (var customer in activeCustomers)
        {
            var customerAmount = customerInvestments[customer.Id];
            var proportion = customerAmount / totalAmount;

            var clientDist = new ClientDistribution
            {
                ClientId = customer.Id,
                ClientName = customer.Name,
                Amount = customerAmount,
                Assets = new List<AssetDistribution>()
            };

            foreach (var order in finalPurchase)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == order.StockCode);
                if (stock == null) continue;

                // Get master custody quantity for this stock (if any)
                int masterQuantity = 0;
                if (masterCustodies.TryGetValue(stock.Id, out var masterStock))
                {
                    masterQuantity = masterStock.Quantity;
                }

                var totalAvailable = order.Quantity + masterQuantity;
                var qtyForClient = (int)(proportion * totalAvailable);
                if (qtyForClient > 0)
                {
                    // Update client position
                    var position = await _context.CustomerPositions
                        .FirstOrDefaultAsync(p => p.CustomerId == customer.Id && p.StockId == stock.Id);
                    if (position == null)
                    {
                        position = new CustomerPosition
                        {
                            CustomerId = customer.Id,
                            StockId = stock.Id,
                            Quantity = qtyForClient,
                            AveragePrice = order.Price
                        };
                        _context.CustomerPositions.Add(position);
                    }
                    else
                    {
                        var totalValue = position.Quantity * position.AveragePrice + qtyForClient * order.Price;
                        position.Quantity += qtyForClient;
                        if (position.Quantity > 0)
                            position.AveragePrice = totalValue / position.Quantity;
                        else
                            position.AveragePrice = 0; // fallback, though shouldn't happen
                    }
                    clientDist.Assets.Add(new AssetDistribution
                    {
                        Ticker = order.StockCode,
                        Quantity = qtyForClient
                    });

                    // Register movement in client account (debit)
                    var movement = new AccountMovement
                    {
                        AccountId = customer.Account.Id,
                        Date = DateTime.Now,
                        Type = TipoMovimento.Debito,
                        Value = qtyForClient * order.Price,
                        Description = $"Scheduled purchase - {order.StockCode}",
                        StockId = stock.Id,
                        OrderId = null
                    };
                    _context.AccountMovements.Add(movement);
                }
            }
            distributions.Add(clientDist);
        }
        result.Distributions = distributions;

        // 11. Calculate and publish IR dedo-duro
        int irEvents = 0;
        foreach (var dist in distributions)
        {
            foreach (var asset in dist.Assets)
            {
                var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == asset.Ticker);
                if (stock == null) continue;
                var order = finalPurchase.First(o => o.StockCode == asset.Ticker);
                var valorOperacao = asset.Quantity * order.Price;
                var irValue = valorOperacao * 0.00005m; // 0,005%

                var irEvent = new
                {
                    Tipo = "IR_DEDO_DURO",
                    ClienteId = dist.ClientId,
                    Cpf = activeCustomers.First(c => c.Id == dist.ClientId).CPF,
                    Ticker = asset.Ticker,
                    TipoOperacao = "COMPRA",
                    Quantidade = asset.Quantity,
                    PrecoUnitario = order.Price,
                    ValorOperacao = valorOperacao,
                    Aliquota = 0.00005m,
                    ValorIR = irValue,
                    DataOperacao = DateTime.Now
                };

                var message = JsonSerializer.Serialize(irEvent);
                await _kafkaProducer.ProduceAsync("ir-dedo-duro", message);
                irEvents++;
            }
        }
        result.IrEventsPublished = irEvents;

        // 12. Update master custody (subtract distributed amounts)
        foreach (var order in finalPurchase)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == order.StockCode);
            if (stock == null) continue;

            var masterCustody = await _context.MasterCustodies.FirstOrDefaultAsync(m => m.StockId == stock.Id);
            if (masterCustody != null)
            {
                var totalDistributed = distributions.Sum(d => d.Assets.Where(a => a.Ticker == order.StockCode).Sum(a => a.Quantity));
                masterCustody.Quantity -= totalDistributed;
            }
        }
        await _context.SaveChangesAsync();

        // 13. Calculate final residuals
        var residuals = await _context.MasterCustodies
            .Where(m => m.Quantity > 0)
            .Include(m => m.Stock)
            .ToListAsync();
        result.Residuals = residuals.Select(r => new Residual
        {
            Ticker = r.Stock.Code,
            Quantity = r.Quantity
        }).ToList();

        await transaction.CommitAsync();

        result.Success = true;
        result.Message = "Scheduled purchase successfully executed.";
        result.ExecutionDate = DateTime.Now;
        return result;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error occurred while executing consolidated scheduled purchase");
        result.Success = false;
        result.Message = $"Erro: {ex.Message}";
        return result;
    }
}
    private bool IsPurchaseDay(DateTime date)
    {
        // Check if it's a weekday (Mon-Fri).
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Check if it's day 5, 15, or 25 (considering adjustments for business days)
        int day = date.Day;
        if (day == 5 || day == 15 || day == 25)
            return true;

        // If it's a business day after a non-business day, check if the previous day was a purchase day
        // Example: day 6 (Monday) if 5 was Sunday, it should execute.
        if (day == 6 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 5 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;
        if (day == 16 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 15 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;
        if (day == 26 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 25 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;

        return false;
    }

    private List<OrderDetail> SplitIntoLotesFracionario(int quantity, string ticker)
    {
        var details = new List<OrderDetail>();
        int lotes = quantity / 100;
        int frac = quantity % 100;
        if (lotes > 0)
        {
            details.Add(new OrderDetail
            {
                Tipo = "LOTE",
                Ticker = ticker,
                Quantity = lotes * 100
            });
        }
        if (frac > 0)
        {
            details.Add(new OrderDetail
            {
                Tipo = "FRACIONARIO",
                Ticker = ticker + "F",
                Quantity = frac
            });
        }
        return details;
    }
}