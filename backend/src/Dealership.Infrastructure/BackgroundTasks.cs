using System.Threading.Channels;
using Dealership.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> queue = Channel.CreateBounded<Func<CancellationToken, Task>>(100);
    public ValueTask QueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken) => queue.Writer.WriteAsync(workItem, cancellationToken);
    public IAsyncEnumerable<Func<CancellationToken, Task>> ReadAllAsync(CancellationToken cancellationToken) => queue.Reader.ReadAllAsync(cancellationToken);
}

public sealed class QueuedWorker(BackgroundTaskQueue queue, ILogger<QueuedWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in queue.ReadAllAsync(stoppingToken))
        {
            try { await workItem(stoppingToken); }
            catch (Exception exception) { logger.LogError(exception, "A background work item failed permanently."); }
        }
    }
}
