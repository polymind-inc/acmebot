using Acmebot.App.Functions.Http;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class PingTests
{
    [Fact]
    public void Get_WithoutAuthentication_ReturnsOk()
    {
        var httpContext = new DefaultHttpContext();
        var function = new Ping(new HttpContextAccessor { HttpContext = httpContext });

        var result = function.Get(httpContext.Request);

        Assert.IsType<OkResult>(result);
    }
}
