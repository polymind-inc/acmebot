using Acmebot.App.Providers;

using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsRecordNameTests
{
    [Theory]
    [InlineData("", "@")]
    [InlineData("_acme-challenge", "_acme-challenge")]
    [InlineData("_acme-challenge.www", "_acme-challenge.www")]
    public void AzureDnsTxtRecord_UsesAtSignForZoneApex(string relativeRecordName, string expected)
    {
        Assert.Equal(expected, AzureDnsProvider.ToAzureRelativeRecordName(relativeRecordName));
    }

    [Fact]
    public void AzureDnsApexAdd_PreservesExistingTxtAndAvoidsDuplicates()
    {
        var data = new DnsTxtRecordData { TtlInSeconds = 300 };
        data.Metadata["owner"] = "external";
        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "existing" } });

        Assert.True(AzureDnsProvider.AddApexTxtValues(data, ["token-1", "token-2", "token-1"]));
        Assert.False(AzureDnsProvider.AddApexTxtValues(data, ["token-1", "token-2"]));
        Assert.Equal(new[] { "existing", "token-1", "token-2" }, data.DnsTxtRecords.SelectMany(x => x.Values).ToArray());
        Assert.Equal(300L, data.TtlInSeconds);
        Assert.Equal("external", data.Metadata["owner"]);
    }

    [Fact]
    public void AzureDnsApexCleanup_RemovesOnlyExpectedWholeTxtRecords()
    {
        var data = new DnsTxtRecordData { TtlInSeconds = 300 };
        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "existing" } });
        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "token-1" } });
        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "token-2" } });
        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "token", "-1" } });

        Assert.True(AzureDnsProvider.RemoveApexTxtValues(data, ["token-1", "token-2"]));
        Assert.False(AzureDnsProvider.RemoveApexTxtValues(data, ["token-1", "token-2"]));
        Assert.Equal(2, data.DnsTxtRecords.Count);
        Assert.Equal(new[] { "existing" }, data.DnsTxtRecords[0].Values.ToArray());
        Assert.Equal(new[] { "token", "-1" }, data.DnsTxtRecords[1].Values.ToArray());
        Assert.Equal(300L, data.TtlInSeconds);
    }

    [Fact]
    public void AzureDnsApexCleanup_OnlyDeletesEmptyAcmebotCreatedSets()
    {
        var data = new DnsTxtRecordData();

        // A pre-existing empty record set must not be deleted.
        Assert.False(AzureDnsProvider.IsAcmebotOwnedEmptyApexRecord(data));

        data.Metadata["acmebot-ns-apex-created"] = "true";
        Assert.True(AzureDnsProvider.IsAcmebotOwnedEmptyApexRecord(data));

        data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { "another-system's-value" } });
        Assert.False(AzureDnsProvider.IsAcmebotOwnedEmptyApexRecord(data));
    }

    [Theory]
    [InlineData("example.com", "_acme-challenge", "_acme-challenge.example.com")]
    [InlineData("example.com.", "_acme-challenge", "_acme-challenge.example.com")]
    [InlineData("example.com", "_acme-challenge.", "_acme-challenge.example.com")]
    [InlineData("example.com", "_acme-challenge.www", "_acme-challenge.www.example.com")]
    public void ToFqdn_NormalizesTrailingDots(string zoneName, string relativeRecordName, string expected)
    {
        Assert.Equal(expected, DnsRecordName.ToFqdn(zoneName, relativeRecordName));
    }

    [Theory]
    [InlineData("example.com", "_acme-challenge", "_acme-challenge.example.com.")]
    [InlineData("example.com.", "_acme-challenge", "_acme-challenge.example.com.")]
    public void ToAbsoluteFqdn_AlwaysEndsWithTrailingDot(string zoneName, string relativeRecordName, string expected)
    {
        Assert.Equal(expected, DnsRecordName.ToAbsoluteFqdn(zoneName, relativeRecordName));
    }
}
