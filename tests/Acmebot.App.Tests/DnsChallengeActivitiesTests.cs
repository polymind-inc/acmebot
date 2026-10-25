using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;
using Acmebot.App.Options;
using Acmebot.App.Providers;
using Acmebot.App.Services;

using DnsClient;
using DnsClient.Protocol;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsChallengeActivitiesTests
{
    [Fact]
    public async Task AuthorizationAndCleanup_AcrossWorkers_EnumerateZonesOnlyDuringPrecondition()
    {
        using var handler = new AuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var provider = new RecordingProvider();
        var activities = CreateActivities(factory, provider);
        var token = TestContext.Current.CancellationToken;

        var prepared = await activities.Dns01Precondition(CreatePolicy(["example.com", "*.example.com"]));
        var zones = RoundTrip(prepared);
        var (challenges, propagationSeconds) = await activities.Dns01Authorization((provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2)], zones), token);
        // A fresh worker may now see a new child zone, but cleanup must use the original snapshot.
        var cleanupProvider = new RecordingProvider { ChallengeZone = "_acme-challenge.example.com" };
        await CreateActivities(factory, cleanupProvider).CleanupDnsChallenge((provider.Name, RoundTrip(challenges), RoundTrip(zones)), token);

        Assert.Equal(1, provider.ListCount);
        Assert.Equal(0, cleanupProvider.ListCount);
        var zone = Assert.Single(zones);
        Assert.Equal("zone-1", zone.Id);
        Assert.Equal("example.com", zone.Name);
        Assert.Equal(2, challenges.Count);
        Assert.Equal(60, propagationSeconds);
        var created = Assert.Single(provider.Created);
        Assert.Equal("zone-1", created.ZoneId);
        Assert.Equal("_acme-challenge", created.RecordName);
        Assert.Equal(challenges.Select(challenge => challenge.DnsRecordValue), created.Values);
        Assert.Equal(("zone-1", "example.com", "_acme-challenge"), Assert.Single(provider.Deleted));
        Assert.Equal(("zone-1", "example.com", "_acme-challenge"), Assert.Single(cleanupProvider.Deleted));
    }

    [Fact]
    public async Task AuthorizationAndCleanup_WithMultipleSanZones_RetainOnlyRequiredZones()
    {
        using var handler = new AuthorizationHandler
        {
            IdentifierValues = new() { [3] = "www.example.net", [4] = "staging.example.net" }
        };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var provider = new RecordingProvider
        {
            ChallengeZone = "_acme-challenge.staging.example.net",
            AdditionalZones =
            [
                new() { Id = "zone-2", Name = "example.net" },
                new() { Id = "zone-3", Name = "staging.example.net" },
                new() { Id = "unused-zone", Name = "example.org" }
            ]
        };
        var activities = CreateActivities(factory, provider);
        var token = TestContext.Current.CancellationToken;

        var prepared = await activities.Dns01Precondition(CreatePolicy(["example.com", "*.example.com", "www.example.net", "*.staging.example.net"]));
        var zones = RoundTrip(prepared);
        var (challenges, _) = await activities.Dns01Authorization((provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2), AuthorizationUrl(3), AuthorizationUrl(4)], zones), token);
        var cleanupProvider = new RecordingProvider();
        await CreateActivities(factory, cleanupProvider).CleanupDnsChallenge((provider.Name, RoundTrip(challenges), RoundTrip(zones)), token);

        Assert.Equal(new[] { "zone-1", "zone-2", "challenge-zone" }, zones.Select(zone => zone.Id));
        Assert.Equal(4, challenges.Count);
        Assert.Collection(provider.Created,
            record =>
            {
                Assert.Equal("zone-1", record.ZoneId);
                Assert.Equal("_acme-challenge", record.RecordName);
                Assert.Equal(2, record.Values.Length);
            },
            record =>
            {
                Assert.Equal("zone-2", record.ZoneId);
                Assert.Equal("_acme-challenge.www", record.RecordName);
                Assert.Single(record.Values);
            },
            record =>
            {
                Assert.Equal("challenge-zone", record.ZoneId);
                Assert.Equal("", record.RecordName);
                Assert.Single(record.Values);
            });
        Assert.Equal(new[]
        {
            ("zone-1", "example.com", "_acme-challenge"),
            ("zone-2", "example.net", "_acme-challenge.www"),
            ("challenge-zone", "_acme-challenge.staging.example.net", "")
        }, cleanupProvider.Deleted);
        Assert.Equal(provider.Deleted, cleanupProvider.Deleted);
        Assert.Equal(1, provider.ListCount);
        Assert.Equal(0, cleanupProvider.ListCount);
    }

    [Theory]
    [InlineData("example.com", null, "_acme-challenge.example.com")]
    [InlineData("*.example.com", null, "_acme-challenge.example.com")]
    [InlineData("example.com", "validation.example.com", "_acme-challenge.validation.example.com")]
    public async Task Precondition_RetainsMostSpecificChallengeZone(string dnsName, string? dnsAlias, string challengeZone)
    {
        using var handler = new AuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var provider = new RecordingProvider { ChallengeZone = challengeZone };
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy([dnsName], dnsAlias));

        var (challenges, _) = await activities.Dns01Authorization((provider.Name, dnsAlias, [AuthorizationUrl(1)], RoundTrip(zones)), TestContext.Current.CancellationToken);
        await activities.CleanupDnsChallenge((provider.Name, challenges, zones), TestContext.Current.CancellationToken);

        var zone = Assert.Single(zones);
        Assert.Equal("challenge-zone", zone.Id);
        Assert.Equal(challengeZone, zone.Name);
        var created = Assert.Single(provider.Created);
        Assert.Equal("challenge-zone", created.ZoneId);
        Assert.Equal("", created.RecordName);
        Assert.All(provider.Deleted, record => Assert.Equal(("challenge-zone", challengeZone, ""), record));
        Assert.Equal(1, provider.ListCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authorization_WithoutPendingChallenges_DoesNotResolveDnsProvider(bool hasAuthorization)
    {
        using var handler = new AuthorizationHandler { ValidIds = [1] };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var activities = CreateActivities(factory);
        Uri[] urls = hasAuthorization ? [AuthorizationUrl(1)] : [];

        var (challenges, propagationSeconds) = await activities.Dns01Authorization(("Missing DNS", null, urls, []), TestContext.Current.CancellationToken);

        Assert.Empty(challenges);
        Assert.Equal(0, propagationSeconds);
        Assert.Equal(hasAuthorization ? 1 : 0, handler.AuthorizationCount);
        if (!hasAuthorization)
        {
            Assert.Equal(0, handler.RequestCount);
        }
    }

    [Fact]
    public async Task Authorization_SelectsProviderByName_WhenZonesHaveNoProviderReference()
    {
        using var handler = new AuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var otherProvider = new RecordingProvider("Other DNS");
        var selectedProvider = new RecordingProvider();
        var activities = CreateActivities(factory, otherProvider, selectedProvider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com"]));

        var (challenges, _) = await activities.Dns01Authorization((selectedProvider.Name, null, [AuthorizationUrl(1)], RoundTrip(zones)), TestContext.Current.CancellationToken);
        await activities.CleanupDnsChallenge((selectedProvider.Name, challenges, RoundTrip(zones)), TestContext.Current.CancellationToken);

        Assert.Equal(1, selectedProvider.ListCount);
        Assert.Single(selectedProvider.Created);
        Assert.Equal(2, selectedProvider.Deleted.Count);
        Assert.Equal(0, otherProvider.ListCount);
        Assert.Empty(otherProvider.Created);
        Assert.Empty(otherProvider.Deleted);
    }

    [Fact]
    public async Task Authorization_WithMixedStatuses_WritesOnlyPendingChallenges()
    {
        using var handler = new AuthorizationHandler { ValidIds = [1] };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var provider = new RecordingProvider();
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com", "*.example.com"]));

        var (challenges, _) = await activities.Dns01Authorization((provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2)], zones), TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://acme.example/challenge/2"), Assert.Single(challenges).Url);
        Assert.Single(Assert.Single(provider.Created).Values);
        Assert.Equal(1, provider.ListCount);
    }

    [Fact]
    public async Task Authorization_WhenInitialDeleteFails_CleansUpOnlyAttemptedCreations()
    {
        using var handler = new AuthorizationHandler { IdentifierValues = new() { [2] = "www.example.com" } };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var failure = new InvalidOperationException("Initial deletion failed.");
        var provider = new RecordingProvider
        {
            OnDelete = (name, _) =>
            {
                if (name == "_acme-challenge.www")
                {
                    throw failure;
                }
            }
        };
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com", "www.example.com"]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => activities.Dns01Authorization(
            (provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2)], zones), TestContext.Current.CancellationToken));

        Assert.Same(failure, exception);
        Assert.Single(provider.Created);
        Assert.Equal(new[] { "_acme-challenge", "_acme-challenge.www", "_acme-challenge" }, provider.Deleted.Select(record => record.RecordName));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Authorization_WhenCreationFails_CleansUpThroughFailedRecordOnly(int failedCreation)
    {
        using var handler = new AuthorizationHandler
        {
            IdentifierValues = new() { [2] = "www.example.com", [3] = "api.example.com" }
        };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var failure = new InvalidOperationException("Creation response was lost.");
        var creationCount = 0;
        var provider = new RecordingProvider
        {
            OnCreate = (_, _) =>
            {
                if (++creationCount == failedCreation)
                {
                    throw failure;
                }
            }
        };
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com", "www.example.com", "api.example.com"]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => activities.Dns01Authorization(
            (provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2), AuthorizationUrl(3)], RoundTrip(zones)), TestContext.Current.CancellationToken));

        Assert.Same(failure, exception);
        string[] recordNames = ["_acme-challenge", "_acme-challenge.www", "_acme-challenge.api"];
        var attemptedNames = recordNames.Take(failedCreation).ToArray();
        Assert.Equal(attemptedNames, provider.Created.Select(record => record.RecordName));
        Assert.Equal(attemptedNames.Concat(attemptedNames), provider.Deleted.Select(record => record.RecordName));
        Assert.All(provider.Deleted, record => Assert.Equal(("zone-1", "example.com"), (record.ZoneId, record.ZoneName)));
        Assert.Equal(1, provider.ListCount);
    }

    [Fact]
    public async Task Authorization_WhenLaterZoneIsMissing_CleansUpEarlierCreation()
    {
        using var handler = new AuthorizationHandler { IdentifierValues = new() { [2] = "example.net" } };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var provider = new RecordingProvider();
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com"]));

        await Assert.ThrowsAsync<PreconditionException>(() => activities.Dns01Authorization(
            (provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2)], zones), TestContext.Current.CancellationToken));

        Assert.Single(provider.Created);
        Assert.Equal(2, provider.Deleted.Count);
        Assert.All(provider.Deleted, record => Assert.Equal(("zone-1", "example.com", "_acme-challenge"), record));
    }

    [Fact]
    public async Task Authorization_WhenCleanupFails_PreservesCreationFailureAndAttemptsRemainingRecords()
    {
        using var handler = new AuthorizationHandler
        {
            IdentifierValues = new() { [2] = "www.example.com", [3] = "api.example.com" }
        };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        var failure = new InvalidOperationException("Creation failed.");
        var creationFailed = false;
        var provider = new RecordingProvider
        {
            OnCreate = (name, _) =>
            {
                if (name == "_acme-challenge.api")
                {
                    creationFailed = true;
                    throw failure;
                }
            },
            OnDelete = (name, _) =>
            {
                if (creationFailed && name == "_acme-challenge")
                {
                    throw new IOException("Cleanup failed.");
                }
            }
        };
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com", "www.example.com", "api.example.com"]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => activities.Dns01Authorization(
            (provider.Name, null, [AuthorizationUrl(1), AuthorizationUrl(2), AuthorizationUrl(3)], zones), TestContext.Current.CancellationToken));

        Assert.Same(failure, exception);
        string[] recordNames = ["_acme-challenge", "_acme-challenge.www", "_acme-challenge.api"];
        Assert.Equal(recordNames.Concat(recordNames), provider.Deleted.Select(record => record.RecordName));
    }

    [Fact]
    public async Task Authorization_WhenCreationIsCancelled_CleansUpWithIndependentToken()
    {
        using var handler = new AuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var failure = new OperationCanceledException(cancellation.Token);
        var provider = new RecordingProvider
        {
            OnCreate = (_, _) =>
            {
                cancellation.Cancel();
                throw failure;
            },
            OnDelete = (_, token) => token.ThrowIfCancellationRequested()
        };
        var activities = CreateActivities(factory, provider);
        var zones = await activities.Dns01Precondition(CreatePolicy(["example.com"]));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => activities.Dns01Authorization(
            (provider.Name, null, [AuthorizationUrl(1)], zones), cancellation.Token));

        Assert.Same(failure, exception);
        Assert.Single(provider.Created);
        Assert.Equal(2, provider.Deleted.Count);
        Assert.True(provider.DeletedTokens[^1].CanBeCanceled);
        Assert.False(provider.DeletedTokens[^1].IsCancellationRequested);
        Assert.NotEqual(cancellation.Token, provider.DeletedTokens[^1]);
    }

    [Fact]
    public async Task Cleanup_WithoutChallenges_DoesNotResolveProvider()
    {
        using var handler = new AuthorizationHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(httpClient);

        await CreateActivities(factory).CleanupDnsChallenge(("Missing DNS", [], []), TestContext.Current.CancellationToken);

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("_acme-challenge.example.com", "_acme-challenge.example.com")]
    [InlineData("_acme-challenge.example.com", "_ACME-CHALLENGE.EXAMPLE.COM")]
    [InlineData("_acme-challenge.validation.example.com", "_acme-challenge.validation.example.com")]
    public async Task CheckDnsChallenge_WithSharedRecordName_QueriesOnce(string firstName, string secondName)
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        var lookupClient = new RecordingLookupClient(name => new TxtQueryResponse(name, "value-1", "value-2", "unrelated"));
        var activities = CreateActivities(factory, lookupClient);

        await activities.CheckDnsChallenge([CreateChallenge(firstName, "value-1"), CreateChallenge(secondName, "value-2")]);

        Assert.Equal(new[] { firstName }, lookupClient.Queries);
    }

    [Fact]
    public async Task CheckDnsChallenge_WithDistinctRecordNames_QueriesEachNameOnce()
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        const string firstName = "_acme-challenge.example.com";
        const string secondName = "_acme-challenge.example.net";
        var lookupClient = new RecordingLookupClient(name => name == firstName
            ? new TxtQueryResponse(name, "value-1", "value-3")
            : new TxtQueryResponse(name, "value-2"));
        var activities = CreateActivities(factory, lookupClient);

        await activities.CheckDnsChallenge([
            CreateChallenge(firstName, "value-1"),
            CreateChallenge(secondName, "value-2"),
            CreateChallenge(firstName, "value-3")
        ]);

        Assert.Equal(new[] { firstName, secondName }, lookupClient.Queries);
    }

    [Theory]
    [InlineData("value-1", "value-2")]
    [InlineData("value-2", "value-1")]
    [InlineData("VALUE-1", "value-1")]
    public async Task CheckDnsChallenge_WhenAnyValueIsMissing_Retries(string actualValue, string missingValue)
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        const string recordName = "_acme-challenge.example.com";
        var lookupClient = new RecordingLookupClient(name => new TxtQueryResponse(name, actualValue));
        var activities = CreateActivities(factory, lookupClient);

        var exception = await Assert.ThrowsAsync<RetriableActivityException>(() => activities.CheckDnsChallenge([
            CreateChallenge(recordName, "value-1"),
            CreateChallenge(recordName, "value-2")
        ]));

        Assert.Contains($"Expected: '{missingValue}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Actual: '{actualValue}'", exception.Message, StringComparison.Ordinal);
        Assert.Single(lookupClient.Queries);
    }

    [Fact]
    public async Task CheckDnsChallenge_WithoutTxtRecords_Retries()
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        var lookupClient = new RecordingLookupClient(name => new TxtQueryResponse(name));
        var activities = CreateActivities(factory, lookupClient);

        var exception = await Assert.ThrowsAsync<RetriableActivityException>(() => activities.CheckDnsChallenge([
            CreateChallenge("_acme-challenge.example.com", "value-1"),
            CreateChallenge("_acme-challenge.example.com", "value-2")
        ]));

        Assert.Contains("did not return any TXT records yet", exception.Message, StringComparison.Ordinal);
        Assert.Single(lookupClient.Queries);
    }

    [Fact]
    public async Task CheckDnsChallenge_WithDnsError_RetriesWithOriginalException()
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        var dnsException = new DnsResponseException(DnsResponseCode.ServerFailure);
        var lookupClient = new RecordingLookupClient(_ => throw dnsException);
        var activities = CreateActivities(factory, lookupClient);

        var exception = await Assert.ThrowsAsync<RetriableActivityException>(() => activities.CheckDnsChallenge([
            CreateChallenge("_acme-challenge.example.com", "value-1"),
            CreateChallenge("_acme-challenge.example.com", "value-2")
        ]));

        Assert.Same(dnsException, exception.InnerException);
        Assert.Contains("_acme-challenge.example.com", exception.Message, StringComparison.Ordinal);
        Assert.Single(lookupClient.Queries);
    }

    [Fact]
    public async Task CheckDnsChallenge_OnRetry_QueriesAgainAndAcceptsNewValues()
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        string[] values = ["value-1"];
        var lookupClient = new RecordingLookupClient(name => new TxtQueryResponse(name, values));
        var activities = CreateActivities(factory, lookupClient);
        AcmeChallengeResult[] challenges = [
            CreateChallenge("_acme-challenge.example.com", "value-1"),
            CreateChallenge("_acme-challenge.example.com", "value-2")
        ];

        await Assert.ThrowsAsync<RetriableActivityException>(() => activities.CheckDnsChallenge(challenges));
        values = ["value-1", "value-2"];
        await activities.CheckDnsChallenge(challenges);

        Assert.Equal(new[] { "_acme-challenge.example.com", "_acme-challenge.example.com" }, lookupClient.Queries);
    }

    [Fact]
    public async Task CheckDnsChallenge_WithoutChallenges_DoesNotQueryDns()
    {
        using var httpClient = new HttpClient(new AuthorizationHandler());
        using var factory = CreateFactory(httpClient);
        var lookupClient = new RecordingLookupClient(_ => throw new InvalidOperationException("DNS must not be queried."));

        await CreateActivities(factory, lookupClient).CheckDnsChallenge([]);

        Assert.Empty(lookupClient.Queries);
    }

    private static AcmeChallengeResult CreateChallenge(string recordName, string value) => new()
    {
        Url = new Uri($"https://acme.example/challenge/{value}"),
        DnsRecordName = recordName,
        DnsRecordValue = value
    };

    private static T RoundTrip<T>(T value)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, options), options)!;
    }

    private static Uri AuthorizationUrl(int id) => new($"https://acme.example/authorization/{id}");

    private static CertificatePolicyItem CreatePolicy(string[] dnsNames, string? dnsAlias = null) => new()
    {
        CertificateName = "certificate",
        DnsNames = dnsNames,
        DnsAlias = dnsAlias,
        DnsProviderName = "Test DNS",
        KeyType = "RSA"
    };

    private static DnsChallengeActivities CreateActivities(AcmeClientFactory factory, params IDnsProvider[] providers)
        => new(new LookupClient(), factory, new DnsZoneQueryService(providers, NullLogger<DnsZoneQueryService>.Instance), NullLogger<DnsChallengeActivities>.Instance);

    private static DnsChallengeActivities CreateActivities(AcmeClientFactory factory, ILookupClient lookupClient)
        => new(lookupClient, factory, new DnsZoneQueryService([], NullLogger<DnsZoneQueryService>.Instance), NullLogger<DnsChallengeActivities>.Instance);

    private static AcmeClientFactory CreateFactory(HttpClient httpClient)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Endpoint = new Uri("https://acme.example/directory"),
            Contacts = "admin@example.com",
            VaultBaseUrl = "https://vault.example/"
        });
        return new AcmeClientFactory(options, new ExistingAccountStore(), () => new AcmeClient(httpClient, options.Value.Endpoint));
    }

    private sealed class RecordingProvider(string name = "Test DNS") : IDnsProvider
    {
        public string Name => name;
        public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);
        public string? ChallengeZone { get; init; }
        public IReadOnlyList<DnsZone> AdditionalZones { get; init; } = [];
        public Action<string, CancellationToken>? OnCreate { get; init; }
        public Action<string, CancellationToken>? OnDelete { get; init; }
        public int ListCount { get; private set; }
        public List<(string ZoneId, string RecordName, string[] Values)> Created { get; } = [];
        public List<(string ZoneId, string ZoneName, string RecordName)> Deleted { get; } = [];
        public List<CancellationToken> DeletedTokens { get; } = [];

        public Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            ListCount++;
            Assert.Equal(1, ListCount);
            var zones = new List<DnsZone> { new() { Id = "zone-1", Name = "example.com" } };
            zones.AddRange(AdditionalZones);
            if (ChallengeZone is not null)
            {
                zones.Add(new DnsZone { Id = "challenge-zone", Name = ChallengeZone });
            }
            return Task.FromResult<IReadOnlyList<DnsZone>>(zones);
        }

        public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
        {
            Created.Add((zone.Id, relativeRecordName, values));
            OnCreate?.Invoke(relativeRecordName, cancellationToken);
            return Task.CompletedTask;
        }

        public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
        {
            Deleted.Add((zone.Id, zone.Name, relativeRecordName));
            DeletedTokens.Add(cancellationToken);
            OnDelete?.Invoke(relativeRecordName, cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLookupClient(Func<string, IDnsQueryResponse> query) : LookupClient, ILookupClient
    {
        public List<string> Queries { get; } = [];

        Task<IDnsQueryResponse> IDnsQuery.QueryAsync(string name, QueryType queryType, QueryClass queryClass, CancellationToken cancellationToken)
        {
            Assert.Equal(QueryType.TXT, queryType);
            Assert.Equal(QueryClass.IN, queryClass);
            Queries.Add(name);
            return Task.FromResult(query(name));
        }
    }

    private sealed class TxtQueryResponse(string name, params string[] values) : IDnsQueryResponse
    {
        public IReadOnlyList<DnsResourceRecord> Answers { get; } = values.Select(value => (DnsResourceRecord)new TxtRecord(
            new ResourceRecordInfo(name, ResourceRecordType.TXT, QueryClass.IN, 60, value.Length), [value], [value])).ToArray();
        public IReadOnlyList<DnsQuestion> Questions => throw new NotSupportedException();
        public IReadOnlyList<DnsResourceRecord> Additionals => throw new NotSupportedException();
        public IEnumerable<DnsResourceRecord> AllRecords => throw new NotSupportedException();
        public IReadOnlyList<DnsResourceRecord> Authorities => throw new NotSupportedException();
        public string AuditTrail => throw new NotSupportedException();
        public string ErrorMessage => throw new NotSupportedException();
        public bool HasError => throw new NotSupportedException();
        public DnsResponseHeader Header => throw new NotSupportedException();
        public int MessageSize => throw new NotSupportedException();
        public NameServer NameServer => throw new NotSupportedException();
        public DnsQuerySettings Settings => throw new NotSupportedException();
    }

    private sealed class ExistingAccountStore : IAcmeStateStore
    {
        private readonly AccountKey _key = AccountKey.CreateDefault();
        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default)
        {
            object state = path == "account_key.json" ? _key : new AccountDetails
            {
                Kid = new Uri("https://acme.example/account/1"),
                Payload = new AcmeAccountResource { Status = AcmeAccountStatuses.Valid }
            };
            return Task.FromResult((TState?)state);
        }

        public Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class AuthorizationHandler : HttpMessageHandler
    {
        public int[] ValidIds { get; init; } = [];
        public Dictionary<int, string> IdentifierValues { get; init; } = [];
        public int RequestCount { get; private set; }
        public int AuthorizationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Replay-Nonce", Base64Url.EncodeToString(Guid.NewGuid().ToByteArray()));
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/directory":
                    response.Content = JsonContent.Create(new
                    {
                        newNonce = "https://acme.example/nonce",
                        newAccount = "https://acme.example/account",
                        newOrder = "https://acme.example/order"
                    });
                    break;
                case "/nonce":
                    break;
                default:
                    Assert.StartsWith("/authorization/", request.RequestUri.AbsolutePath);
                    AuthorizationCount++;
                    var id = int.Parse(request.RequestUri.Segments[^1]);
                    response.Content = JsonContent.Create(new
                    {
                        status = ValidIds.Contains(id) ? "valid" : "pending",
                        identifier = new { type = "dns", value = IdentifierValues.GetValueOrDefault(id, "example.com") },
                        challenges = new[] { new { type = "dns-01", url = $"https://acme.example/challenge/{id}", token = $"dG9rZW4{id}" } }
                    });
                    break;
            }
            return Task.FromResult(response);
        }
    }
}
