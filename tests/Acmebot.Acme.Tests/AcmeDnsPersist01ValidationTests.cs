using System.Net;
using System.Text.Json;

using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeDnsPersist01ValidationTests
{
    [Fact]
    public void MissingMetadata_IsUnavailableWithActionableErrors()
    {
        var directory = CreateDirectory(null);
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        Assert.Contains("issuerDomainNames", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsurePreProvisioningIsAvailable(directory)).Message);
        Assert.Contains("accountHashPrefix", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge())).Message);
    }

    [Fact]
    public void MissingDirectoryNames_StillAllowsInteractiveChallengeWithHashPrefix()
    {
        var directory = CreateDirectory(new AcmeDirectoryMetadata { AccountHashPrefix = "https://ca.example/hash/" });
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge());
    }

    [Fact]
    public void PreProvisioning_RequiresIssuerNamesAndHashPrefix_NotCaaIdentities()
    {
        var directory = CreateDirectory(new AcmeDirectoryMetadata { CaaIdentities = ["ca.example"] });
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        Assert.Contains("issuerDomainNames", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsurePreProvisioningIsAvailable(directory)).Message);

        directory = CreateDirectory(ValidMetadata());
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsurePreProvisioningIsAvailable(directory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/path/")]
    [InlineData("https://ca.example/with space/")]
    [InlineData("https://ca.example/é/")]
    public void InvalidHashPrefix_DisablesPreProvisioningAndRejectsInteractiveChallenge(string? prefix)
    {
        var directory = CreateDirectory(ValidMetadata() with { AccountHashPrefix = prefix });
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        Assert.Contains("accountHashPrefix", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsurePreProvisioningIsAvailable(directory)).Message);
        Assert.Contains("accountHashPrefix", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge())).Message);
    }

    [Theory]
    [InlineData("ca.example")]
    [InlineData("xn--bcher-kva.example")]
    public void NormalizedIssuerNames_AreAccepted(string name)
    {
        var directory = CreateDirectory(ValidMetadata() with { IssuerDomainNames = [name] });
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge([name]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("CA.example")]
    [InlineData("ca.example.")]
    [InlineData("bücher.example")]
    [InlineData("*.example")]
    [InlineData("_ca.example")]
    [InlineData("-ca.example")]
    [InlineData("ca-.example")]
    [InlineData("ca..example")]
    [InlineData("xn--.example")]
    public void InvalidIssuerNames_DisablePreProvisioningButDoNotPreventValidInteractiveChallenges(string name)
    {
        var directory = CreateDirectory(ValidMetadata() with { IssuerDomainNames = [name] });
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge());
        Assert.Contains("issuerDomainNames", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge([name]))).Message);
    }

    [Fact]
    public void IssuerNames_EnforceCountLabelAndDomainLengthLimits()
    {
        var longestName = string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', 61));
        Assert.Equal(253, longestName.Length);
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(CreateDirectory(
            ValidMetadata() with { IssuerDomainNames = [longestName] })));

        IReadOnlyList<string>[] invalidArrays = [[], Enumerable.Repeat("ca.example", 11).ToArray(),
            [new string('a', 64) + ".example"], [longestName + "d"], [null!]];
        foreach (var names in invalidArrays)
        {
            Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(CreateDirectory(
                ValidMetadata() with { IssuerDomainNames = names })));
            Assert.Throws<InvalidOperationException>(() =>
                AcmeDnsPersist01Validation.EnsureChallengeIsUsable(CreateDirectory(ValidMetadata()), CreateChallenge(names)));
        }

        var tenNames = Enumerable.Range(0, 10).Select(i => $"ca{i}.example").ToArray();
        var directory = CreateDirectory(ValidMetadata() with { IssuerDomainNames = tenNames });
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge(tenNames));
    }

    [Fact]
    public void Challenge_MustIncludeAllValidDirectoryNames_AndMayAddNames()
    {
        var directory = CreateDirectory(ValidMetadata() with { IssuerDomainNames = ["ca.example", "other.example"] });
        Assert.Contains("every issuer", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge())).Message);
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
            CreateChallenge(["extra.example", "other.example", "ca.example"]));
    }

    [Fact]
    public void Challenge_MustUseAdvertisedCaaIdentities_AfterNormalization()
    {
        var directory = CreateDirectory(ValidMetadata() with { CaaIdentities = ["CA.EXAMPLE.", "BÜCHER.EXAMPLE."] });
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
            CreateChallenge(["ca.example", "xn--bcher-kva.example"]));
        Assert.Contains("caaIdentities", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
                CreateChallenge(["ca.example", "unadvertised.example"]))).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad..example")]
    public void NonconformingCaaIdentities_DoNotDisablePreProvisioningOrConstrainChallenge(string identity)
    {
        var directory = CreateDirectory(ValidMetadata() with { CaaIdentities = ["other.example", identity] });
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge());
    }

    [Fact]
    public void Challenge_RequiresCorrectTypeUrlAndStatus_ButNoToken()
    {
        var directory = CreateDirectory(ValidMetadata());
        var challenge = CreateChallenge();
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, challenge);
        Assert.Null(challenge.Token);
        Assert.Throws<ArgumentException>(() => AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
            challenge with { Type = AcmeChallengeTypes.Dns01 }));
        Assert.Throws<InvalidOperationException>(() => AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
            challenge with { Url = new Uri("relative", UriKind.Relative) }));
        Assert.Throws<InvalidOperationException>(() => AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory,
            challenge with { Status = null }));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("42")]
    [InlineData("\"ca.example\"")]
    [InlineData("[\"ca.example\",null]")]
    [InlineData("[\"ca.example\",42]")]
    public async Task NonconformingDirectoryArrays_AreUnavailable_AndDoNotBreakOtherChallenges(string json)
    {
        using var handler = new RecordingHandler();
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, $$$"""
            {"newNonce":"https://example.com/nonce","newAccount":"https://example.com/account","newOrder":"https://example.com/order",
             "meta":{"issuerDomainNames":{{{json}}},"caaIdentities":{{{json}}},"accountHashPrefix":"https://example.com/hash/","future":true}}
            """, "application/json"));
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, new Uri("https://example.com/directory"));
        var directory = await client.GetDirectoryAsync(TestContext.Current.CancellationToken);
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        Assert.True(directory.Metadata!.AdditionalData!["future"].GetBoolean());
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, CreateChallenge());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{}")]
    public async Task NonStringHashPrefix_DoesNotBreakDirectoryParsing(string json)
    {
        using var handler = new RecordingHandler();
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, $$$"""
            {"newNonce":"https://example.com/nonce","newAccount":"https://example.com/account","newOrder":"https://example.com/order",
             "meta":{"issuerDomainNames":["ca.example"],"accountHashPrefix":{{{json}}}}}
            """, "application/json"));
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, new Uri("https://example.com/directory"));
        var directory = await client.GetDirectoryAsync(TestContext.Current.CancellationToken);
        Assert.False(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        Assert.Null(directory.Metadata!.AccountHashPrefix);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[\"ca.example\",42]")]
    public void MalformedChallengeIssuerArrays_AreRejectedWhenSelected(string json)
    {
        var challenge = JsonSerializer.Deserialize<AcmeChallengeResource>($$$"""
            {"type":"dns-persist-01","url":"https://example.com/challenge/1","status":"pending","issuerDomainNames":{{{json}}}}
            """);
        Assert.Contains("issuerDomainNames", Assert.Throws<InvalidOperationException>(() =>
            AcmeDnsPersist01Validation.EnsureChallengeIsUsable(CreateDirectory(ValidMetadata()), challenge!)).Message);
    }

    [Fact]
    public async Task Client_ParsesTypedMetadataAndChallenges_AndPreservesExtensions()
    {
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            newNonce = AcmeTestSupport.DefaultNewNonceUrl,
            newAccount = AcmeTestSupport.DefaultNewAccountUrl,
            newOrder = AcmeTestSupport.DefaultNewOrderUrl,
            meta = new { issuerDomainNames = new[] { "ca.example" }, accountHashPrefix = "https://ca.example/hash/", future = true }
        }));
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            identifier = new { type = "dns", value = "example.com" },
            status = "pending",
            challenges = new object[]
            {
                new { type = "dns-persist-01", url = "https://example.com/challenge/1", status = "pending", issuerDomainNames = new[] { "ca.example" }, future = 42 },
                new { type = "dns-01", url = "https://example.com/challenge/2", status = "pending", token = "dG9rZW4" },
                new { type = "future-01", url = "https://example.com/challenge/3", extension = true }
            }
        }, replayNonce: "bm9uY2Uy"));
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, new Uri("https://example.com/directory"));
        var directory = await client.GetDirectoryAsync(TestContext.Current.CancellationToken);
        var result = await client.GetAuthorizationAsync(AcmeTestSupport.CreateAccountHandle(signer),
            new Uri("https://example.com/authz/1"), TestContext.Current.CancellationToken);

        Assert.Equal("https://ca.example/hash/", directory.Metadata!.AccountHashPrefix);
        Assert.Equal("ca.example", Assert.Single(directory.Metadata.IssuerDomainNames));
        Assert.True(AcmeDnsPersist01Validation.IsPreProvisioningAvailable(directory));
        var persist = result.Resource.Challenges[0];
        Assert.Equal(AcmeChallengeTypes.DnsPersist01, persist.Type);
        Assert.Equal("ca.example", Assert.Single(persist.IssuerDomainNames));
        Assert.Equal(42, persist.AdditionalData!["future"].GetInt32());
        AcmeDnsPersist01Validation.EnsureChallengeIsUsable(directory, persist);
        var dns01 = result.Resource.Challenges[1];
        Assert.NotEmpty(AcmeChallengeInstructions.CreateDns01(AcmeTestSupport.CreateAccountHandle(signer), result.Resource, dns01).RecordValue);
        Assert.Equal("future-01", result.Resource.Challenges[2].Type.Value);
        Assert.True(result.Resource.Challenges[2].AdditionalData!["extension"].GetBoolean());

        var serialized = JsonSerializer.Serialize(persist);
        Assert.Contains("issuerDomainNames", serialized);
        Assert.Equal(persist.IssuerDomainNames, JsonSerializer.Deserialize<AcmeChallengeResource>(serialized)!.IssuerDomainNames);
    }

    private static AcmeDirectoryMetadata ValidMetadata() => new()
    {
        IssuerDomainNames = ["ca.example"],
        AccountHashPrefix = "https://ca.example/hash/"
    };

    private static AcmeDirectoryResource CreateDirectory(AcmeDirectoryMetadata? metadata) => new()
    {
        NewNonce = AcmeTestSupport.DefaultNewNonceUrl,
        NewAccount = AcmeTestSupport.DefaultNewAccountUrl,
        NewOrder = AcmeTestSupport.DefaultNewOrderUrl,
        Metadata = metadata
    };

    private static AcmeChallengeResource CreateChallenge(IReadOnlyList<string>? names = null) => new()
    {
        Type = AcmeChallengeTypes.DnsPersist01,
        Url = new Uri("https://example.com/challenge/1"),
        Status = AcmeChallengeStatuses.Pending,
        IssuerDomainNames = names ?? ["ca.example"]
    };
}
