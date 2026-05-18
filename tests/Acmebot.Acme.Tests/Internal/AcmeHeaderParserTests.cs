using System.Net;

using Acmebot.Acme.Internal;

using Xunit;

namespace Acmebot.Acme.Tests.Internal;

public sealed class AcmeHeaderParserTests
{
    [Fact]
    public void ParseLinkHeaders_ParsesMultipleLinksWithQuotedValues()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation(
            "Link",
            "<https://example.com/next>;rel=\"next\";title=\"next, page\";type=\"application/json\", <https://example.com/up>;rel=\"up\"");

        var links = AcmeHeaderParser.ParseLinkHeaders(response.Headers);

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal(new Uri("https://example.com/next"), link.Uri);
                Assert.Equal("next", link.Relation);
                Assert.Equal("next, page", link.Title);
                Assert.Equal("application/json", link.MediaType);
            },
            link =>
            {
                Assert.Equal(new Uri("https://example.com/up"), link.Uri);
                Assert.Equal("up", link.Relation);
                Assert.Null(link.Title);
                Assert.Null(link.MediaType);
            });
    }

    [Fact]
    public void ParseLinkHeaders_IgnoresInvalidEntries()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation(
            "Link",
            "invalid-entry, <relative>;rel=\"next\", <https://example.com/valid>;rel=\"alternate\"");

        var links = AcmeHeaderParser.ParseLinkHeaders(response.Headers);

        var link = Assert.Single(links);
        Assert.Equal(new Uri("https://example.com/valid"), link.Uri);
        Assert.Equal("alternate", link.Relation);
    }
}
