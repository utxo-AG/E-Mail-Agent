using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UTXO_E_Mail_Agent.Services;

/// <summary>
/// Background worker that processes queued email tasks
/// </summary>
public class EmailProcessingWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmailProcessingWorker(
        IBackgroundTaskQueue taskQueue,
        IServiceScopeFactory scopeFactory)
    {
        _taskQueue = taskQueue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Log("[EmailProcessingWorker] Started background email processing worker");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a work item from the queue
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                // Create a new scope for each work item (fresh DbContext etc.)
                using var scope = _scopeFactory.CreateScope();
                
                try
                {
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[EmailProcessingWorker] Error processing queued task: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EmailProcessingWorker] Error in worker loop: {ex.Message}");
                // Small delay before retrying to prevent tight loop on persistent errors
                await Task.Delay(1000, stoppingToken);
            }
        }

        Logger.Log("[EmailProcessingWorker] Stopped background email processing worker");
    }
}
