using System.Text.Json;

using Acmebot.App.Functions.Http;
using Acmebot.App.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class GetOperationTests
{
    [Fact]
    public async Task HttpStart_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var httpContext = new DefaultHttpContext();
        var function = new GetOperation(new HttpContextAccessor { HttpContext = httpContext }, NullLogger<GetOperation>.Instance);

        var result = await function.HttpStart(httpContext.Request, "instance-1", starter: null!);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void BuildFailureDetail_WithStatus_IncludesStepMessage()
    {
        var status = new CertificateIssuanceStatus
        {
            CertificateName = "example-com",
            Step = "MergingCertificate",
            Message = "Saving certificate to Key Vault...",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var detail = GetOperation.BuildFailureDetail("Key Vault request failed.", status);

        Assert.Equal("Key Vault request failed. (failed during: Saving certificate to Key Vault...)", detail);
    }

    [Fact]
    public void BuildFailureDetail_WithoutStatus_ReturnsErrorMessageUnchanged()
    {
        var detail = GetOperation.BuildFailureDetail("Key Vault request failed.", status: null);

        Assert.Equal("Key Vault request failed.", detail);
    }

    [Fact]
    public void ReadIssuanceStatus_WithoutCustomStatus_ReturnsNull()
    {
        var metadata = new OrchestrationMetadata("IssueCertificate", "instance-1");

        var status = GetOperation.ReadIssuanceStatus(metadata);

        Assert.Null(status);
    }

    // OrchestrationMetadata.ReadCustomStatusAs<T>() (used by ReadIssuanceStatus for a non-null
    // SerializedCustomStatus) throws unless the instance was fetched by the real Durable Task
    // runtime with getInputsAndOutputs: true; there is no supported way to construct such an
    // instance from test code. This test instead locks the exact wire contract that
    // ReadCustomStatusAs relies on, so a property-name/attribute regression is still caught.
    [Fact]
    public void CertificateIssuanceStatus_JsonRoundTrip_PreservesAllFieldsWithExpectedPropertyNames()
    {
        var updatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var status = new CertificateIssuanceStatus
        {
            CertificateName = "example-com",
            Step = "MergingCertificate",
            Message = "Saving certificate to Key Vault...",
            UpdatedAt = updatedAt
        };

        var json = JsonSerializer.Serialize(status);

        using (var document = JsonDocument.Parse(json))
        {
            var root = document.RootElement;
            Assert.Equal("example-com", root.GetProperty("certificateName").GetString());
            Assert.Equal("MergingCertificate", root.GetProperty("step").GetString());
            Assert.Equal("Saving certificate to Key Vault...", root.GetProperty("message").GetString());
            Assert.Equal(updatedAt, root.GetProperty("updatedAt").GetDateTimeOffset());
        }

        var roundTripped = JsonSerializer.Deserialize<CertificateIssuanceStatus>(json);

        Assert.Equal(status, roundTripped);
    }
}
