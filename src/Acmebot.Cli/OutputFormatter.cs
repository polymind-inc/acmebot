using System.Text.Json;

namespace Acmebot.Cli;

internal static class OutputFormatter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteJsonAsync(TextWriter writer, object value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, s_jsonOptions);

        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    public static async Task WriteCertificatesAsync(TextWriter writer, IReadOnlyList<CertificateItem> certificates, OutputFormat format, CancellationToken cancellationToken)
    {
        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(writer, certificates, cancellationToken);
            return;
        }

        if (certificates.Count == 0)
        {
            await writer.WriteLineAsync("No certificates found.");
            return;
        }

        var rows = certificates
            .OrderBy(certificate => certificate.ExpiresOn)
            .Select(certificate => new[]
            {
                certificate.Name,
                certificate.ExpiresOn.ToString("u"),
                certificate.Enabled ? "enabled" : "disabled",
                certificate.DnsProviderName,
                string.Join(", ", certificate.DnsNames)
            })
            .ToArray();

        await WriteTableAsync(writer, ["Name", "Expires", "State", "DNS Provider", "DNS Names"], rows);
    }

    public static async Task WriteDnsZonesAsync(TextWriter writer, IReadOnlyList<DnsZoneGroup> groups, OutputFormat format, CancellationToken cancellationToken)
    {
        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(writer, groups, cancellationToken);
            return;
        }

        var rows = groups
            .SelectMany(group => (group.DnsZones ?? []).Select(zone => new[] { group.DnsProviderName, zone.Name }))
            .OrderBy(row => row[0], StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row[1], StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rows.Length == 0)
        {
            await writer.WriteLineAsync("No DNS zones found.");
            return;
        }

        await WriteTableAsync(writer, ["DNS Provider", "Zone"], rows);
    }

    public static async Task WriteOperationResultAsync(TextWriter writer, string operationInstanceId, bool completed, OutputFormat format, CancellationToken cancellationToken)
    {
        var result = new OperationResult(completed ? "completed" : "accepted", operationInstanceId);

        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(writer, result, cancellationToken);
            return;
        }

        await writer.WriteLineAsync(completed ? "Operation completed." : "Operation accepted.");
        await writer.WriteLineAsync($"Instance ID: {operationInstanceId}");
    }

    public static async Task WriteCertificateCommandResultAsync(TextWriter writer, string certificateName, string status, OutputFormat format, CancellationToken cancellationToken)
    {
        var result = new CertificateCommandResult(status, certificateName);

        if (format == OutputFormat.Json)
        {
            await WriteJsonAsync(writer, result, cancellationToken);
            return;
        }

        await writer.WriteLineAsync($"Certificate '{certificateName}' {status}.");
    }

    private static async Task WriteTableAsync(TextWriter writer, string[] headers, IReadOnlyList<string[]> rows)
    {
        var widths = headers.Select(header => header.Length).ToArray();

        foreach (var row in rows)
        {
            for (var index = 0; index < row.Length; index++)
            {
                widths[index] = Math.Max(widths[index], row[index].Length);
            }
        }

        await WriteRowAsync(writer, headers, widths);
        await WriteRowAsync(writer, widths.Select(width => new string('-', width)).ToArray(), widths);

        foreach (var row in rows)
        {
            await WriteRowAsync(writer, row, widths);
        }
    }

    private static async Task WriteRowAsync(TextWriter writer, string[] columns, int[] widths)
    {
        for (var index = 0; index < columns.Length; index++)
        {
            if (index > 0)
            {
                await writer.WriteAsync("  ");
            }

            await writer.WriteAsync(columns[index].PadRight(widths[index]));
        }

        await writer.WriteLineAsync();
    }
}
