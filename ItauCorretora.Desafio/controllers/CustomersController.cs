using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.DTOs;
using ItauCorretora.Desafio.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(AppDbContext context, ILogger<CustomersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // POST: api/customers/subscribe
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] ClientSubscriptionRequest request)
    {
        // Validations
        if (request.MonthlyValue < 100)
            return BadRequest(new { error = "The minimum monthly amount is $ 100.00.", code = "INVALID_MONTHLY_VALUE" });

        var existingCPF = await _context.Customers.AnyAsync(c => c.CPF == request.CPF);
        if (existingCPF)
            return BadRequest(new { error = "CPF already registered in the system.", code = "DUPLICATE_CLIENT_CPF" });

        // Create customer
        var customer = new Customer
        {
            Name = request.Name,
            CPF = request.CPF,
            Email = request.Email,
            MonthlyValue = request.MonthlyValue,
            Active = true,
            SubscriptionDate = DateTime.Now
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // Create a graphical account (filhote)
        var accountNumber = $"FLH-{customer.Id:D6}";
        var account = new Account
        {
            CustomerId = customer.Id,
            Type = AccountType.Filhote,
            Balance = 0,
            AccountNumber = accountNumber,
            CreatedAt = DateTime.Now
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var response = new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            CPF = customer.CPF,
            Email = customer.Email,
            MonthlyValue = customer.MonthlyValue,
            Active = customer.Active,
            SubscriptionDate = customer.SubscriptionDate,
            AccountGraphics = new AccountGraphicsResponse
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Type = account.Type.ToString(),
                CreationDate = account.CreatedAt
            }
        };

        return CreatedAtAction(nameof(Subscribe), new { id = customer.Id }, response);
    }

    // POST: api/customers/{customerId}/exit
    [HttpPost("{customerId}/exit")]
    public async Task<IActionResult> Exit(int customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
            return NotFound(new { error = "Customer not found.", code = "CUSTOMER_NOT_FOUND" });

        if (!customer.Active)
            return BadRequest(new { error = "Customer is already inactive.", code = "CUSTOMER_ALREADY_INACTIVE" });

        customer.Active = false;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            customerId = customer.Id,
            name = customer.Name,
            active = customer.Active,
            exitDate = DateTime.Now,
            message = "Subscription ended. Your custody position has been kept."
        });
    }

    // PUT: api/customers/{customerId}/monthly-value
    [HttpPut("{customerId}/monthly-value")]
    public async Task<IActionResult> ChangeMonthlyValue(int customerId, [FromBody] ChangeMonthlyValueRequest request)
    {
        if (request.NewMonthlyValue < 100)
            return BadRequest(new { error = "The minimum monthly amount is $ 100.00.", code = "INVALID_MONTHLY_VALUE" });

        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
            return NotFound(new { error = "Customer not found.", code = "CUSTOMER_NOT_FOUND" });

        var previousValue = customer.MonthlyValue;
        customer.MonthlyValue = request.NewMonthlyValue;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            customerId = customer.Id,
            previousMonthlyValue = previousValue,
            newMonthlyValue = customer.MonthlyValue,
            changeDate = DateTime.Now,
            message = "Monthly value updated. The new value will be considered from the next purchase date."
        });
    }

    // GET: api/customers/{customerId}/portfolio
    [HttpGet("{customerId}/portfolio")]
    public async Task<IActionResult> GetPortfolio(int customerId)
    {
        var customer = await _context.Customers
            .Include(c => c.Positions)
                .ThenInclude(p => p.Stock)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            return NotFound(new { error = "Customer not found.", code = "CUSTOMER_NOT_FOUND" });

        // Get latest quotes for each stock
        var quotes = await _context.Quotes
            .GroupBy(q => q.StockId)
            .Select(g => g.OrderByDescending(q => q.Date).FirstOrDefault())
            .ToDictionaryAsync(q => q.StockId, q => q);

        decimal totalInvested = 0;
        decimal totalCurrentValue = 0;
        var assets = new List<PortfolioAssetDto>();

        foreach (var position in customer.Positions)
        {
            var quote = quotes.GetValueOrDefault(position.StockId);
            var currentPrice = quote?.ClosePrice ?? 0;
            var currentValue = position.Quantity * currentPrice;
            var invested = position.Quantity * position.AveragePrice;
            totalInvested += invested;
            totalCurrentValue += currentValue;
            var profitLoss = currentValue - invested;
            var profitLossPercent = invested > 0 ? (profitLoss / invested) * 100 : 0;

            assets.Add(new PortfolioAssetDto
            {
                Ticker = position.Stock.Code,
                Quantity = position.Quantity,
                AveragePrice = position.AveragePrice,
                CurrentPrice = currentPrice,
                CurrentValue = currentValue,
                ProfitLoss = profitLoss,
                ProfitLossPercent = profitLossPercent,
                Composition = 0 // will be calculated later
            });
        }

        // Calculate composition percentage
        foreach (var asset in assets)
        {
            asset.Composition = totalCurrentValue > 0 ? (asset.CurrentValue / totalCurrentValue) * 100 : 0;
        }

        var profitability = totalInvested > 0 ? ((totalCurrentValue - totalInvested) / totalInvested) * 100 : 0;

        return Ok(new
        {
            customerId = customer.Id,
            name = customer.Name,
            consultationDate = DateTime.Now,
            summary = new
            {
                totalInvested = totalInvested,
                totalCurrentValue = totalCurrentValue,
                totalProfitLoss = totalCurrentValue - totalInvested,
                profitabilityPercent = profitability
            },
            assets = assets
        });
    }

    // GET: api/customers/{customerId}/performance (optional)
    [HttpGet("{customerId}/performance")]
    public async Task<IActionResult> GetPerformance(int customerId)
    {
        var customer = await _context.Customers
            .Include(c => c.Positions)
                .ThenInclude(p => p.Stock)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
            return NotFound();

        // Get customer's account first
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId);
        if (account == null)
            return NotFound("Account not found");

        // Get contribution history from AccountMovements (debits related to scheduled purchases)
        var movements = await _context.AccountMovements
            .Where(m => m.AccountId == account.Id
                        && m.Type == TipoMovimento.Debito
                        && m.Description.Contains("Scheduled purchase"))
            .OrderBy(m => m.Date)
            .ToListAsync();

        var contributionHistory = movements.Select(m => new
        {
            date = m.Date.ToString("yyyy-MM-dd"),
            amount = m.Value,
            installment = "1/3"
        });

        // Get latest quotes for each stock to compute current values
        var quotes = await _context.Quotes
            .GroupBy(q => q.StockId)
            .Select(g => g.OrderByDescending(q => q.Date).FirstOrDefault())
            .ToDictionaryAsync(q => q.StockId, q => q);

        decimal totalInvested = 0;
        decimal totalCurrentValue = 0;

        foreach (var position in customer.Positions)
        {
            totalInvested += position.Quantity * position.AveragePrice;
            var quote = quotes.GetValueOrDefault(position.StockId);
            var currentPrice = quote?.ClosePrice ?? 0;
            totalCurrentValue += position.Quantity * currentPrice;
        }

        var totalProfitLoss = totalCurrentValue - totalInvested;
        var profitabilityPercent = totalInvested > 0 ? (totalProfitLoss / totalInvested) * 100 : 0;

        return Ok(new
        {
            customerId = customer.Id,
            name = customer.Name,
            consultationDate = DateTime.Now,
            performance = new
            {
                totalInvested = totalInvested,
                totalCurrentValue = totalCurrentValue,
                totalProfitLoss = totalProfitLoss,
                profitabilityPercent = profitabilityPercent
            },
            contributionHistory = contributionHistory
        });
    }
}