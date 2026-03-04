using ItauCorretora.Desafio.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuoteController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuoteController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{stockCode}")]
    public async Task<IActionResult> GetQuotes(string stockCode, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var query = _context.Quotes
            .Include(q => q.Stock)
            .Where(q => q.Stock.Code == stockCode);

        if (startDate.HasValue)
            query = query.Where(q => q.Date >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(q => q.Date <= endDate.Value);

        var quotes = await query
            .OrderBy(q => q.Date)
            .Select(q => new
            {
                q.Date,
                q.OpenPrice,
                q.ClosePrice,
                q.HighPrice,
                q.LowPrice,
                q.Volume
            })
            .ToListAsync();

        return Ok(quotes);
    }
}