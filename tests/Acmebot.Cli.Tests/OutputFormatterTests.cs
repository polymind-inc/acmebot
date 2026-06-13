using System.Text.Json;

using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class OutputFormatterTests
{
    [Fact]
    public async Task WriteOperationResultAsync_WithTableOutput_WritesInstanceId()
    {
        using var output = new StringWriter();

        await OutputFormatter.WriteOperationResultAsync(
            output,
            "abc",
            completed: false,
            OutputFormat.Table,
            TestContext.Current.CancellationToken);

        var text = output.ToString();

        Assert.Contains("Operation accepted.", text);
        Assert.Contains("Instance ID: abc", text);
        Assert.DoesNotContain("Location:", text);
    }

    [Fact]
    public async Task WriteOperationResultAsync_WithJsonOutput_WritesOperationInstanceId()
    {
        using var output = new StringWriter();

        await OutputFormatter.WriteOperationResultAsync(
            output,
            "abc",
            completed: true,
            OutputFormat.Json,
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(output.ToString());

        Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("abc", document.RootElement.GetProperty("operationInstanceId").GetString());
        Assert.False(document.RootElement.TryGetProperty("operationLocation", out _));
    }
}
