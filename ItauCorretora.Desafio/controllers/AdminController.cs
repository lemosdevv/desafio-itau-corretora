using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.DTOs;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRebalancementService _rebalancementService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext context, IRebalancementService rebalancementService, ILogger<AdminController> logger)
    {
        _context = context;
        _rebalancementService = rebalancementService;
        _logger = logger;
    }

    // POST: api/admin/wallets
    [HttpPost("wallets")]
    public async Task<IActionResult> CreateWallet([FromBody] CreateWalletRequest request)
    {
        // Validations
        if (request.Items.Count != 5)
            return BadRequest(new { error = "The basket must contain exactly 5 assets.", code = "INVALID_WALLET_SIZE" });

        var totalPercent = request.Items.Sum(i => i.Percentual);
        if (Math.Abs(totalPercent - 100m) > 0.01m) // tolerância para erros de arredondamento
            return BadRequest(new { error = "The sum of percentages must be 100%.", code = "INVALID_PERCENT_SUM" });

        // Verify if all tickers exist
        var tickers = request.Items.Select(i => i.Ticker).ToList();
        var existingStocks = await _context.Stocks.Where(s => tickers.Contains(s.Code)).Select(s => s.Code).ToListAsync();
        var missingTickers = tickers.Except(existingStocks).ToList();
        if (missingTickers.Any())
            return BadRequest(new { error = $"Tickers not found: {string.Join(", ", missingTickers)}", code = "TICKER_NOT_FOUND" });

        // Deactivate the current active basket.
        var activeWallet = await _context.RecommendedWallets.FirstOrDefaultAsync(w => w.Active);
        if (activeWallet != null)
        {
            activeWallet.Active = false;
            activeWallet.EndDate = DateTime.Now;
        }

        // Create new basket
        var wallet = new RecommendedWallet
        {
            Name = request.Name,
            StartDate = DateTime.Now,
            Active = true
        };
        _context.RecommendedWallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Add items
        foreach (var item in request.Items)
        {
            var stock = await _context.Stocks.FirstAsync(s => s.Code == item.Ticker);
            var walletItem = new WalletRecommendedItem
            {
                RecommendedWalletId = wallet.Id,
                StockId = stock.Id,
                Weight = item.Percentual
            };
            _context.WalletRecommendedItems.Add(walletItem);
        }
        await _context.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await _rebalancementService.RebalanceByWalletChangeAsync(wallet.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing rebalancement by basket change");
            }
        });

        var response = new WalletResponse
        {
            Id = wallet.Id,
            Name = wallet.Name,
            IsActive = wallet.Active,
            CreatedAt = wallet.StartDate,
            DeactivatedAt = wallet.EndDate,
            Items = request.Items.Select(i => new WalletItemResponse
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList()
        };

        return CreatedAtAction(nameof(GetCurrentWallet), new { }, response);
    }

    // GET: api/admin/wallets/current
    [HttpGet("wallets/current")]
    public async Task<IActionResult> GetCurrentWallet()
    {
        var wallet = await _context.RecommendedWallets
            .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
            .FirstOrDefaultAsync(w => w.Active);

        if (wallet == null)
            return NotFound(new { error = "No active basket found.", code = "NO_ACTIVE_WALLET" });

        var response = new WalletResponse
        {
            Id = wallet.Id,
            Name = wallet.Name,
            IsActive = wallet.Active,
            CreatedAt = wallet.StartDate,
            DeactivatedAt = wallet.EndDate,
            Items = wallet.Itens.Select(i => new WalletItemResponse
            {
                Ticker = i.Stock.Code,
                Percentual = i.Weight
            }).ToList()
        };

        return Ok(response);
    }

    // GET: api/admin/wallets/history
    [HttpGet("wallets/history")]
    public async Task<IActionResult> GetWalletHistory()
    {
        var wallets = await _context.RecommendedWallets
            .Include(w => w.Itens)
                .ThenInclude(i => i.Stock)
            .OrderByDescending(w => w.StartDate)
            .ToListAsync();

        var response = wallets.Select(w => new WalletResponse
        {
            Id = w.Id,
            Name = w.Name,
            IsActive = w.Active,
            CreatedAt = w.StartDate,
            DeactivatedAt = w.EndDate,
            Items = w.Itens.Select(i => new WalletItemResponse
            {
                Ticker = i.Stock.Code,
                Percentual = i.Weight
            }).ToList()
        }).ToList();

        return Ok(response);
    }
}