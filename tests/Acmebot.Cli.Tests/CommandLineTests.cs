using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Parse_WithRepeatedOptions_PreservesAllValues()
    {
        var commandLine = CommandLine.Parse(
        [
            "--endpoint",
            "https://acmebot.example",
            "certificate",
            "issue",
            "--dns-name",
            "example.com",
            "--dns-name=*.example.com",
            "--no-wait"
        ]);

        Assert.Equal(["certificate", "issue"], commandLine.Arguments);
        Assert.Equal("https://acmebot.example", commandLine.GetOption("endpoint"));
        Assert.Equal(["example.com", "*.example.com"], commandLine.GetOptions("dns-name"));
        Assert.True(commandLine.GetFlag("no-wait"));
    }

    [Fact]
    public void Parse_WithMissingOptionValue_Throws()
    {
        var ex = Assert.Throws<CliException>(() => CommandLine.Parse(["--endpoint"]));

        Assert.Equal("Option '--endpoint' requires a value.", ex.Message);
    }
}
