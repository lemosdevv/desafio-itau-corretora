using ItauCorretora.Desafio.Services.Interfaces;

namespace ItauCorretora.Desafio.Workers;

public class PurchaseSchedulerWorker : BackgroundService
{
    private readonly ILogger<PurchaseSchedulerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public PurchaseSchedulerWorker(ILogger<PurchaseSchedulerWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PurchaseSchedulerWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if today is a shopping day.
                if (IsPurchaseDay(DateTime.Today))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var purchaseService = scope.ServiceProvider.GetRequiredService<IConsolidatedPurchaseService>();
                    _logger.LogInformation("Executing a scheduled purchase for today...");
                    var result = await purchaseService.ExecutePurchaseAsync();
                    _logger.LogInformation("Scheduled purchase completed. Success: {Success}, Message: {Message}", result.Success, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PurchaseSchedulerWorker");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private bool IsPurchaseDay(DateTime date)
    {
        // Check if it's a weekday (Monday to Friday).
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        int day = date.Day;
        if (day == 5 || day == 15 || day == 25)
            return true;

        // If it's a weekday after a weekend, check if the previous day was one of the purchase days
        if (day == 6 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 5 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;
        if (day == 16 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 15 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;
        if (day == 26 && date.DayOfWeek == DayOfWeek.Monday && date.AddDays(-1).Day == 25 && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
            return true;

        return false;
    }
}