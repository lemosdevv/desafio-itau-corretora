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
    public async Task<IActionResult> GetOrders([FromQuery] int? customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        // Validação básica
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Limite máximo para evitar sobrecarga

        var query = _context.Orders
            .Include(o => o.Stock)
            .Include(o => o.Customer)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                CustomerName = o.Customer != null ? o.Customer.Name : null,
                StockCode = o.Stock != null ? o.Stock.Code : null,
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