using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Services.Implementations;

public class IncomeTaxService : IIncomeTaxService
{
    private readonly AppDbContext _context;
    private readonly ILogger<IncomeTaxService> _logger;

    public IncomeTaxService(AppDbContext context, ILogger<IncomeTaxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CalculateMonthlyTaxAsync(int year, int month)
    {
        var customers = await _context.Customers
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var customerId in customers)
        {
            try
            {
                await CalculateCustomerTaxAsync(customerId, year, month);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating income tax for client. {CustomerId} in {Year}-{Month}", customerId, year, month);
            }
        }
    }

    public async Task<IncomeTaxResult> CalculateCustomerTaxAsync(int customerId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Find all sales for the customer within the period with a status of Executed.
        var sales = await _context.Orders
            .Include(o => o.Stock)
            .Where(o => o.CustomerId == customerId
                && o.Type == OrderType.Sale
                && o.Status == StatusOrder.Executed
                && o.Date >= startDate
                && o.Date <= endDate)
            .ToListAsync();

        if (!sales.Any())
        {
            return new IncomeTaxResult
            {
                CustomerId = customerId,
                Year = year,
                Month = month,
                TotalProfit = 0,
                TotalLoss = 0,
                NetProfit = 0,
                TaxDue = 0
            };
        }

        decimal totalProfit = 0;
        decimal totalLoss = 0;

        foreach (var sale in sales)
        {
            // We need the average purchase price to calculate profit.
            // Assume the customer's position reflects the current average price.
            // For past sales, we would need history. As a simplification:
            // We use the position's average price on the sale date.
            // Since we don't have history, we assume the current average price reflects the cost.
            // In a real system, more elaborate tracking would be necessary.
            var position = await _context.CustomerPositions
                .FirstOrDefaultAsync(p => p.CustomerId == customerId && p.StockId == sale.StockId);

            if (position == null)
            {
                _logger.LogWarning("Consumer {CustomerId} does not have a position for the asset {StockCode} in the sell market. {OrderId}", 
                    customerId, sale.Stock.Code, sale.Id);
                continue;
            }

            // Total cost of sale: quantity sold * average price
            var cost = sale.Quantity * position.AveragePrice;
            var revenue = sale.Quantity * sale.Price;
            var profit = revenue - cost;

            if (profit > 0)
                totalProfit += profit;
            else
                totalLoss += Math.Abs(profit); // loss (positive)
        }

        var netProfit = totalProfit - totalLoss;

        // Determine the tax rate (e.g., 15% for swing trading). We could differentiate between day trading.
        // Let's assume 15% for all sales.
        var taxRate = 0.15m;
        var taxDue = netProfit > 0 ? netProfit * taxRate : 0;

        // Register or update the tax in the income tax table.
        var incomeTax = await _context.IncomeTaxes
            .FirstOrDefaultAsync(t => t.CustomerId == customerId && t.ReferenceDate == startDate);

        if (incomeTax == null)
        {
            incomeTax = new IncomeTax
            {
                CustomerId = customerId,
                ReferenceDate = startDate,
                AmountDue = taxDue,
                PaidValue = null
            };
            _context.IncomeTaxes.Add(incomeTax);
        }
        else
        {
            incomeTax.AmountDue = taxDue;
        }

        await _context.SaveChangesAsync();

        return new IncomeTaxResult
        {
            CustomerId = customerId,
            Year = year,
            Month = month,
            TotalProfit = totalProfit,
            TotalLoss = totalLoss,
            NetProfit = netProfit,
            TaxRate = taxRate,
            TaxDue = taxDue
        };
    }
}