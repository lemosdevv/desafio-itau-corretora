using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Desafio.Workers;

public class RebalancementWorker : BackgroundService
{
    private readonly ILogger<RebalancementWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);
    public RebalancementWorker(ILogger<RebalancementWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RebalancingWorker initiated.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Executing scheduled rebalancing...");

                using var scope = _scopeFactory.CreateScope();
                var rebalancementService = scope.ServiceProvider.GetRequiredService<IRebalancementService>();

                await rebalancementService.RebalanceAllAsync();

                _logger.LogInformation("Scheduled rebalancing completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled rebalancing.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}