using System.Threading.Channels;

namespace UTXO_E_Mail_Agent.Services;

/// <summary>
/// Interface for background task queue
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queues a work item to be processed in the background
    /// </summary>
    void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem);
    
    /// <summary>
    /// Dequeues a work item from the queue
    /// </summary>
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of background task queue using Channel
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    public void QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        
        if (!_queue.Writer.TryWrite(workItem))
        {
            throw new InvalidOperationException("Unable to queue work item. Queue may be full.");
        }
    }

    public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}
