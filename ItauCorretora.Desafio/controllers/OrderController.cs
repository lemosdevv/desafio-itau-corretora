using ItauCorretora.Desafio.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrderController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] int? customerId, [FromQuery] int? page = 1, [FromQuery] int? pageSize = 10)
    {
        var query = _context.Orders
            .Include(o => o.Stock)
            .Include(o => o.Customer)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.Date)
            .Skip((page.Value - 1) * pageSize.Value)
            .Take(pageSize.Value)
            .Select(o => new
            {
                o.Id,
                CustomerName = o.Customer.Name,
                StockCode = o.Stock.Code,
                o.Date,
                Type = o.Type.ToString(),
                o.Quantity,
                o.Price,
                TotalValue = o.Quantity * o.Price,
                Status = o.Status.ToString()
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, orders });
    }
}