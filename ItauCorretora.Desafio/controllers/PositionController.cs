using ItauCorretora.Desafio.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionController : ControllerBase
{
    private readonly AppDbContext _context;

    public PositionController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetCustomerPositions(int customerId)
    {
        var positions = await _context.CustomerPositions
            .Include(p => p.Stock)
            .Where(p => p.CustomerId == customerId)
            .Select(p => new
            {
                p.Stock.Code,
                p.Quantity,
                p.AveragePrice,
                TotalValue = p.Quantity * p.AveragePrice
            })
            .ToListAsync();

        return Ok(positions);
    }
}