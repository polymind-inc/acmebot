using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions;

public partial class PurgeInstanceHistory(ILogger<PurgeInstanceHistory> logger)
{
    [Function($"{nameof(PurgeInstanceHistory)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 1 * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        var deleteBefore = DateTimeOffset.UtcNow.AddMonths(-1);

        LogInstanceHistoryPurgeStarted(logger, deleteBefore);

        await starter.PurgeInstancesAsync(
            DateTimeOffset.MinValue,
            deleteBefore,
            [
                OrchestrationRuntimeStatus.Completed,
                OrchestrationRuntimeStatus.Failed
            ]);

        LogInstanceHistoryPurgeCompleted(logger, deleteBefore);
    }

    [LoggerMessage(LogLevel.Information, "Durable instance history purge started. DeleteBefore: {DeleteBefore}")]
    private static partial void LogInstanceHistoryPurgeStarted(ILogger logger, DateTimeOffset deleteBefore);

    [LoggerMessage(LogLevel.Information, "Durable instance history purge completed. DeleteBefore: {DeleteBefore}")]
    private static partial void LogInstanceHistoryPurgeCompleted(ILogger logger, DateTimeOffset deleteBefore);
}
