using Acmebot.Acme;
using Acmebot.Acme.Models;

namespace Acmebot.Internal;

public sealed class OrderDetails
{
    public required AcmeOrderResource Payload { get; init; }

    public required Uri OrderUrl { get; init; }

    public static OrderDetails FromResult(AcmeResult<AcmeOrderResource> result, Uri? existingOrderUrl = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OrderDetails
        {
            Payload = result.Resource,
            OrderUrl = result.Location ?? existingOrderUrl ?? throw new InvalidOperationException("The ACME server did not return an order URL.")
        };
    }
}
