using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_WithRemovedScopeOption_ReturnsUsage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["--scope", "api://acmebot/user_impersonation", "--endpoint", "https://acmebot.example", "certificate", "list"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Unknown option '--scope'.", error.ToString());
    }
}
