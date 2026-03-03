using System.Globalization;
using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Desafio.Services.Implementations;

public class CotahistParserService : ICotahistParserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CotahistParserService> _logger;

    public CotahistParserService(AppDbContext context, ILogger<CotahistParserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> ParseAndUpdateQuotesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Archive not found: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath);
        var quotesToAdd = new List<Quote>();
        var stockCache = new Dictionary<string, Stock>();

        int lineNumber = 0;
        int addedCount = 0;

        foreach (var line in lines)
        {
            lineNumber++;

            if (line.Length < 150) continue; 
            if (line.Substring(0, 2) != "01") continue; 

            try
            {
                var stockCode = line.Substring(11, 12).Trim();
                var dateStr = line.Substring(2, 8);
                var openStr = line.Substring(56, 13);
                var highStr = line.Substring(69, 13);
                var lowStr = line.Substring(82, 13);
                var closeStr = line.Substring(108, 13);

                // Convert data (formato: AAAAMMDD)
                if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    _logger.LogWarning("Invalid date on line {LineNumber}: {DateStr}", lineNumber, dateStr);
                    continue;
                }

                // Convert prices (format: integer with 2 implicit decimal places)
                if (!TryParsePrice(openStr, out var open) ||
                    !TryParsePrice(highStr, out var high) ||
                    !TryParsePrice(lowStr, out var low) ||
                    !TryParsePrice(closeStr, out var close))
                {
                    _logger.LogWarning("Invalid price on line {LineNumber}", lineNumber);
                    continue;
                }

                // Search for or create Stock (active)
                if (!stockCache.TryGetValue(stockCode, out var stock))
                {
                    stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Code == stockCode);
                    if (stock == null)
                    {
                        // If it doesn't exist, we can create a new one (or ignore)
                        stock = new Stock
                        {
                            Code = stockCode,
                            Name = stockCode, // perhaps fetch name from another source
                            Type = "ON" // default
                        };
                        _context.Stocks.Add(stock);
                        await _context.SaveChangesAsync(); // save to generate ID
                    }
                    stockCache[stockCode] = stock;
                }

                // Verify if a quote already exists for this asset/date
                var existingQuote = await _context.Quotes
                    .FirstOrDefaultAsync(q => q.StockId == stock.Id && q.Date == date);

                if (existingQuote != null)
                {
                    // Update (optional)
                    existingQuote.OpenPrice = open;
                    existingQuote.HighPrice = high;
                    existingQuote.LowPrice = low;
                    existingQuote.ClosePrice = close;
                }
                else
                {
                    var quote = new Quote
                    {
                        StockId = stock.Id,
                        Date = date,
                        OpenPrice = open,
                        HighPrice = high,
                        LowPrice = low,
                        ClosePrice = close
                    };
                    quotesToAdd.Add(quote);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing line {LineNumber}", lineNumber);
            }
        }

        // Add new quotes in batch.
        if (quotesToAdd.Any())
        {
            await _context.Quotes.AddRangeAsync(quotesToAdd);
            addedCount = await _context.SaveChangesAsync();
        }
        else
        {
            addedCount = await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Processing completed. {AddedCount} new quotes added.", addedCount);
        return addedCount;
    }

    private bool TryParsePrice(string input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        if (input.Length == 0) return false;

        // Format of B3: integer with 2 implicit decimal places (e.g., "0000012345" = 123.45)
        if (int.TryParse(input, out var intValue))
        {
            value = intValue / 100m;
            return true;
        }
        return false;
    }
}