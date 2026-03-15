using System.Reflection;

namespace Acmebot.App.Infrastructure;

internal static class Constants
{
    public static string FunctionAppName { get; } = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "Unknown";

    public static string ApplicationVersion { get; } = typeof(Program).Assembly
                                                                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                                                      ?.InformationalVersion ?? "1.0.0";
}
