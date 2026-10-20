using Xunit;

namespace Acmebot.App.Tests;

[CollectionDefinition(AppServiceAuthenticationCollection.Name, DisableParallelization = true)]
public sealed class AppServiceAuthenticationCollection
{
    public const string Name = "App Service authentication";
}

internal sealed class AppServiceAuthenticationScope : IDisposable
{
    private const string VariableName = "WEBSITE_AUTH_ENABLED";
    private readonly string? _previousValue = Environment.GetEnvironmentVariable(VariableName);

    public AppServiceAuthenticationScope(string? value) => Environment.SetEnvironmentVariable(VariableName, value);

    public void Dispose() => Environment.SetEnvironmentVariable(VariableName, _previousValue);
}
