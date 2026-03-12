using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;

namespace Acmebot.App.Functions;

public class PurgeInstanceHistory
{
    [Function($"{nameof(PurgeInstanceHistory)}_{nameof(Timer)}")]
    public Task Timer([TimerTrigger("0 0 0 1 * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        return starter.PurgeInstancesAsync(
            DateTimeOffset.MinValue,
            DateTimeOffset.UtcNow.AddMonths(-1),
            [
                OrchestrationRuntimeStatus.Completed,
                OrchestrationRuntimeStatus.Failed
            ]);
    }
}
