using System.ComponentModel.DataAnnotations;

using Acmebot.App.Options;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AcmebotOptionsTests
{
    [Fact]
    public void Validate_WithDefaultValues_Succeeds()
    {
        Assert.True(TryValidate(CreateValidOptions(), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Validate_WithOutOfRangeDnsChallengeCheckMaxAttempts_Fails(int value)
    {
        var options = CreateValidOptions();
        options.DnsChallengeCheckMaxAttempts = value;

        Assert.False(TryValidate(options, out var results));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(AcmebotOptions.DnsChallengeCheckMaxAttempts)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void Validate_WithOutOfRangeDnsChallengeCheckIntervalSeconds_Fails(int value)
    {
        var options = CreateValidOptions();
        options.DnsChallengeCheckIntervalSeconds = value;

        Assert.False(TryValidate(options, out var results));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(AcmebotOptions.DnsChallengeCheckIntervalSeconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Validate_WithOutOfRangeOrderPollingMaxAttempts_Fails(int value)
    {
        var options = CreateValidOptions();
        options.OrderPollingMaxAttempts = value;

        Assert.False(TryValidate(options, out var results));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(AcmebotOptions.OrderPollingMaxAttempts)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void Validate_WithOutOfRangeOrderPollingIntervalSeconds_Fails(int value)
    {
        var options = CreateValidOptions();
        options.OrderPollingIntervalSeconds = value;

        Assert.False(TryValidate(options, out var results));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(AcmebotOptions.OrderPollingIntervalSeconds)));
    }

    private static AcmebotOptions CreateValidOptions()
    {
        return new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://acme.example/directory"),
            VaultBaseUrl = "https://vault.example/"
        };
    }

    private static bool TryValidate(AcmebotOptions options, out List<ValidationResult> results)
    {
        results = [];

        return Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
    }
}
