using Azure.Identity;

namespace Acmebot.Cli;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var commandLine = CommandLine.Parse(args);

            if (commandLine.GetFlag("help") || commandLine.Arguments.Count == 0)
            {
                await HelpText.WriteAsync(output);
                return ExitCodes.Success;
            }

            ValidateKnownOptions(commandLine);

            var options = CliOptions.Create(commandLine);

            using var client = new AcmebotApiClient(new HttpClient(), options.Endpoint, options.CredentialOptions.CreateCredential(), options.TokenScopes);

            return await RunCommandAsync(commandLine, options, client, output, error, cancellationToken);
        }
        catch (CliException ex)
        {
            await error.WriteLineAsync(ex.Message);
            await error.WriteLineAsync();
            await HelpText.WriteUsageHintAsync(error);

            return ExitCodes.Usage;
        }
        catch (AuthenticationFailedException ex)
        {
            await error.WriteLineAsync($"Authentication failed: {ex.Message}");

            return ExitCodes.AuthenticationError;
        }
        catch (AcmebotApiException ex)
        {
            await error.WriteLineAsync($"API request failed: {(int)ex.StatusCode} {ex.StatusCode}");
            await error.WriteLineAsync(ex.Message);

            return ExitCodes.ApiError;
        }
        catch (HttpRequestException ex)
        {
            await error.WriteLineAsync($"HTTP request failed: {ex.Message}");

            return ExitCodes.NetworkError;
        }
        catch (TimeoutException ex)
        {
            await error.WriteLineAsync(ex.Message);

            return ExitCodes.ApiError;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Canceled.");

            return ExitCodes.Canceled;
        }
    }

    private static async Task<int> RunCommandAsync(
        CommandLine commandLine,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var command = commandLine.Arguments[0].ToLowerInvariant();

        return command switch
        {
            "certificate" => await RunCertificateCommandAsync(commandLine, options, client, output, error, cancellationToken),
            "dns-zone" => await RunDnsZoneCommandAsync(commandLine, options, client, output, cancellationToken),
            "operation" => await RunOperationCommandAsync(commandLine, options, client, output, error, cancellationToken),
            _ => throw new CliException($"Unknown command '{commandLine.Arguments[0]}'.")
        };
    }

    private static async Task<int> RunCertificateCommandAsync(
        CommandLine commandLine,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (commandLine.Arguments.Count < 2)
        {
            throw new CliException("Missing certificate subcommand.");
        }

        return commandLine.Arguments[1].ToLowerInvariant() switch
        {
            "list" => await ListCertificatesAsync(commandLine, 2, options, client, output, cancellationToken),
            "issue" => await IssueCertificateAsync(commandLine, 2, options, client, output, error, cancellationToken),
            "renew" => await RenewCertificateAsync(commandLine, 2, options, client, output, error, cancellationToken),
            "revoke" => await RevokeCertificateAsync(commandLine, 2, options, client, output, cancellationToken),
            _ => throw new CliException($"Unknown certificate subcommand '{commandLine.Arguments[1]}'.")
        };
    }

    private static async Task<int> RunDnsZoneCommandAsync(
        CommandLine commandLine,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (commandLine.Arguments.Count < 2)
        {
            throw new CliException("Missing DNS zone subcommand.");
        }

        return commandLine.Arguments[1].ToLowerInvariant() switch
        {
            "list" => await ListDnsZonesAsync(commandLine, 2, options, client, output, cancellationToken),
            _ => throw new CliException($"Unknown DNS zone subcommand '{commandLine.Arguments[1]}'.")
        };
    }

    private static async Task<int> RunOperationCommandAsync(
        CommandLine commandLine,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (commandLine.Arguments.Count < 2)
        {
            throw new CliException("Missing operation subcommand.");
        }

        if (!string.Equals(commandLine.Arguments[1], "wait", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException($"Unknown operation subcommand '{commandLine.Arguments[1]}'.");
        }

        var operationLocation = GetSingleArgument(commandLine, 2, "operation location");

        await client.WaitForOperationAsync(BuildOperationLocation(options.Endpoint, operationLocation), options.PollInterval, options.Timeout, error, cancellationToken);
        await OutputFormatter.WriteOperationResultAsync(output, BuildOperationLocation(options.Endpoint, operationLocation), completed: true, options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static async Task<int> ListCertificatesAsync(
        CommandLine commandLine,
        int commandOffset,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        EnsureNoExtraArguments(commandLine, commandOffset);

        var certificates = await client.GetCertificatesAsync(cancellationToken);

        await OutputFormatter.WriteCertificatesAsync(output, certificates, options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static async Task<int> ListDnsZonesAsync(
        CommandLine commandLine,
        int commandOffset,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        EnsureNoExtraArguments(commandLine, commandOffset);

        var dnsZones = await client.GetDnsZonesAsync(cancellationToken);

        await OutputFormatter.WriteDnsZonesAsync(output, dnsZones, options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static async Task<int> IssueCertificateAsync(
        CommandLine commandLine,
        int commandOffset,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        EnsureNoExtraArguments(commandLine, commandOffset);

        var policy = CertificatePolicyFactory.Create(commandLine);
        var operationLocation = await client.IssueCertificateAsync(policy, cancellationToken);
        var wait = ShouldWait(commandLine);

        if (wait)
        {
            await client.WaitForOperationAsync(operationLocation, options.PollInterval, options.Timeout, error, cancellationToken);
        }

        await OutputFormatter.WriteOperationResultAsync(output, operationLocation, wait, options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static async Task<int> RenewCertificateAsync(
        CommandLine commandLine,
        int commandOffset,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var certificateName = GetSingleArgument(commandLine, commandOffset, "certificate name");
        var operationLocation = await client.RenewCertificateAsync(certificateName, cancellationToken);
        var wait = ShouldWait(commandLine);

        if (wait)
        {
            await client.WaitForOperationAsync(operationLocation, options.PollInterval, options.Timeout, error, cancellationToken);
        }

        await OutputFormatter.WriteOperationResultAsync(output, operationLocation, wait, options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static async Task<int> RevokeCertificateAsync(
        CommandLine commandLine,
        int commandOffset,
        CliOptions options,
        AcmebotApiClient client,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var certificateName = GetSingleArgument(commandLine, commandOffset, "certificate name");

        await client.RevokeCertificateAsync(certificateName, cancellationToken);
        await OutputFormatter.WriteCertificateCommandResultAsync(output, certificateName, "revoked", options.OutputFormat, cancellationToken);

        return ExitCodes.Success;
    }

    private static bool ShouldWait(CommandLine commandLine)
    {
        return !commandLine.GetFlag("no-wait");
    }

    private static string GetSingleArgument(CommandLine commandLine, int commandOffset, string name)
    {
        var remaining = commandLine.Arguments.Skip(commandOffset).ToArray();

        return remaining.Length switch
        {
            0 => throw new CliException($"Missing {name}."),
            1 => remaining[0],
            _ => throw new CliException($"Unexpected argument '{remaining[1]}'.")
        };
    }

    private static void EnsureNoExtraArguments(CommandLine commandLine, int commandOffset)
    {
        if (commandLine.Arguments.Count > commandOffset)
        {
            throw new CliException($"Unexpected argument '{commandLine.Arguments[commandOffset]}'.");
        }
    }

    private static void ValidateKnownOptions(CommandLine commandLine)
    {
        var unknownOptions = commandLine.OptionNames
            .Where(option => !CliOptionNames.KnownOptions.Contains(option))
            .ToArray();

        if (unknownOptions.Length > 0)
        {
            throw new CliException($"Unknown option '--{unknownOptions[0]}'.");
        }
    }

    private static Uri BuildOperationLocation(Uri endpoint, string operationLocation)
    {
        if (Uri.TryCreate(operationLocation, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        if (operationLocation.StartsWith("/", StringComparison.Ordinal))
        {
            return new Uri(new Uri(endpoint.GetLeftPart(UriPartial.Authority)), operationLocation.TrimStart('/'));
        }

        if (!operationLocation.Contains('/', StringComparison.Ordinal))
        {
            return new Uri(endpoint, $"api/operations/{Uri.EscapeDataString(operationLocation)}");
        }

        return new Uri(endpoint, operationLocation);
    }
}
