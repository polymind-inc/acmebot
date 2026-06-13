using Acmebot.Cli;

using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

return await CliApplication.RunAsync(args, Console.Out, Console.Error, cancellationTokenSource.Token);
