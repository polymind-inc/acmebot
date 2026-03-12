using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Acmebot.App.Internal;

internal class ApplicationVersionInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry) => telemetry.Context.Component.Version = Constants.ApplicationVersion;
}
