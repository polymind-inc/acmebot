using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Acmebot.Acme;
using Acmebot.Acme.Challenges;
using Acmebot.Acme.Models;

if (args.Length == 0 || args.Any(static x => string.Equals(x, "--help", StringComparison.OrdinalIgnoreCase)))
{
    PrintUsage();
    return;
}

SampleOptions options;

try
{
    options = SampleOptions.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    await RunAsync(options, cancellationTokenSource.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canceled.");
    Environment.ExitCode = 1;
}
catch (AcmeProtocolException ex)
{
    Console.Error.WriteLine($"ACME request failed: {(int)ex.StatusCode} {ex.StatusCode}");
    Console.Error.WriteLine(ex.Message);

    if (ex.RequestUri is not null)
    {
        Console.Error.WriteLine($"Request URL: {ex.RequestUri}");
    }

    if (ex.Problem is { } problem)
    {
        Console.Error.WriteLine($"Problem type: {problem.Type}");

        if (problem.Subproblems.Count > 0)
        {
            foreach (var subproblem in problem.Subproblems)
            {
                Console.Error.WriteLine($"- {subproblem.Detail}");
            }
        }
    }

    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task RunAsync(SampleOptions options, CancellationToken cancellationToken)
{
    Console.WriteLine($"Directory: {options.DirectoryUrl}");
    Console.WriteLine($"Domains: {string.Join(", ", options.Domains)}");
    Console.WriteLine($"Challenge: {options.ChallengeType}");
    Console.WriteLine();

    using var accountSigner = AcmeSigner.CreateP256();
    using var certificateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    using var client = new AcmeClient(
        options.DirectoryUrl,
        new AcmeClientOptions
        {
            UserAgent = "Acmebot.Acme.Sample/1.0"
        });

    var account = await client.CreateAccountAsync(
        accountSigner,
        new AcmeNewAccountRequest
        {
            Contact = [options.Contact],
            TermsOfServiceAgreed = true
        },
        cancellationToken: cancellationToken);

    Console.WriteLine($"Account created: {account.AccountUrl}");

    var orderResult = await client.CreateOrderAsync(
        account,
        options.Domains.Select(static domain => new AcmeIdentifier
        {
            Type = AcmeIdentifierTypes.Dns,
            Value = domain
        }).ToArray(),
        cancellationToken: cancellationToken);

    var orderUrl = orderResult.Location
        ?? throw new InvalidOperationException("The ACME server did not return an order URL.");

    Console.WriteLine($"Order created: {orderUrl}");

    foreach (var authorizationUrl in orderResult.Resource.Authorizations)
    {
        await CompleteAuthorizationAsync(client, account, authorizationUrl, options, cancellationToken);
    }

    var readyOrder = await WaitForOrderStateAsync(
        client,
        account,
        orderUrl,
        options.PollInterval,
        cancellationToken,
        AcmeOrderStatuses.Ready,
        AcmeOrderStatuses.Valid);

    if (readyOrder.Status != AcmeOrderStatuses.Valid)
    {
        var finalizeUrl = readyOrder.Finalize
            ?? throw new InvalidOperationException("The ACME order does not include a finalize URL.");
        var csr = CreateCertificateSigningRequest(options.Domains, certificateKey);
        var finalizeResult = await client.FinalizeOrderAsync(account, finalizeUrl, csr, cancellationToken);

        Console.WriteLine($"Order finalized with status: {finalizeResult.Resource.Status}");

        readyOrder = await WaitForOrderStateAsync(
            client,
            account,
            orderUrl,
            options.PollInterval,
            cancellationToken,
            AcmeOrderStatuses.Valid);
    }

    var certificateUrl = readyOrder.Certificate
        ?? throw new InvalidOperationException("The ACME order does not include a certificate URL.");
    var certificateChain = await client.DownloadCertificateAsync(account, certificateUrl, cancellationToken: cancellationToken);

    Directory.CreateDirectory(options.OutputDirectory);

    var filePrefix = GetFilePrefix(options.Domains[0]);
    var privateKeyPath = Path.Combine(options.OutputDirectory, $"{filePrefix}.key.pem");
    var certificatePath = Path.Combine(options.OutputDirectory, $"{filePrefix}.crt.pem");
    var fullChainPath = Path.Combine(options.OutputDirectory, $"{filePrefix}.fullchain.pem");

    await File.WriteAllTextAsync(privateKeyPath, certificateKey.ExportPkcs8PrivateKeyPem(), cancellationToken);
    await File.WriteAllTextAsync(certificatePath, certificateChain.Certificates[0].ExportCertificatePem(), cancellationToken);
    await File.WriteAllTextAsync(fullChainPath, certificateChain.PemChain, cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Certificate issued.");
    Console.WriteLine($"Private key: {privateKeyPath}");
    Console.WriteLine($"Certificate: {certificatePath}");
    Console.WriteLine($"Full chain: {fullChainPath}");
}

static async Task CompleteAuthorizationAsync(
    AcmeClient client,
    AcmeAccountHandle account,
    Uri authorizationUrl,
    SampleOptions options,
    CancellationToken cancellationToken)
{
    var authorization = (await client.GetAuthorizationAsync(account, authorizationUrl, cancellationToken)).Resource;

    if (authorization.Status == AcmeAuthorizationStatuses.Valid)
    {
        Console.WriteLine($"Authorization already valid: {authorization.Identifier.Value}");
        return;
    }

    if (authorization.Status == AcmeAuthorizationStatuses.Invalid)
    {
        throw new InvalidOperationException($"Authorization is already invalid: {authorization.Identifier.Value}");
    }

    var challenge = authorization.Challenges.FirstOrDefault(x => x.Type == options.ChallengeType)
        ?? throw new InvalidOperationException(
            $"The authorization for '{authorization.Identifier.Value}' does not expose the '{options.ChallengeType}' challenge.");

    Console.WriteLine();
    Console.WriteLine($"Authorization: {authorization.Identifier.Value}");

    if (options.ChallengeType == AcmeChallengeTypes.Dns01)
    {
        var instruction = AcmeChallengeInstructions.CreateDns01(account, authorization, challenge);
        Console.WriteLine($"Create TXT record: {instruction.RecordName}");
        Console.WriteLine($"TXT value: {instruction.RecordValue}");
    }
    else
    {
        var instruction = AcmeChallengeInstructions.CreateHttp01(account, challenge);
        Console.WriteLine($"Serve path: {instruction.Path}");
        Console.WriteLine($"Response body: {instruction.Content}");
    }

    Console.WriteLine("Press Enter after the challenge response is reachable.");
    Console.ReadLine();

    var challengeResult = await client.AnswerChallengeAsync(account, challenge.Url, cancellationToken);
    Console.WriteLine($"Challenge acknowledged with status: {challengeResult.Resource.Status?.ToString() ?? AcmeChallengeStatuses.Pending.Value}");

    var validatedAuthorization = await WaitForAuthorizationAsync(
        client,
        account,
        authorizationUrl,
        options.PollInterval,
        cancellationToken);

    Console.WriteLine($"Authorization status: {validatedAuthorization.Status}");
}

static async Task<AcmeAuthorizationResource> WaitForAuthorizationAsync(
    AcmeClient client,
    AcmeAccountHandle account,
    Uri authorizationUrl,
    TimeSpan pollInterval,
    CancellationToken cancellationToken)
{
    while (true)
    {
        var authorization = (await client.GetAuthorizationAsync(account, authorizationUrl, cancellationToken)).Resource;

        if (authorization.Status == AcmeAuthorizationStatuses.Valid)
        {
            return authorization;
        }

        if (authorization.Status == AcmeAuthorizationStatuses.Invalid)
        {
            var challengeError = authorization.Challenges
                .Select(static challenge => challenge.Error?.Detail)
                .FirstOrDefault(static detail => !string.IsNullOrWhiteSpace(detail));

            throw new InvalidOperationException(
                $"Authorization failed for '{authorization.Identifier.Value}'. {challengeError ?? "No additional detail was returned."}");
        }

        Console.WriteLine($"Waiting for authorization '{authorization.Identifier.Value}' to become valid. Current status: {authorization.Status}");
        await Task.Delay(pollInterval, cancellationToken);
    }
}

static async Task<AcmeOrderResource> WaitForOrderStateAsync(
    AcmeClient client,
    AcmeAccountHandle account,
    Uri orderUrl,
    TimeSpan pollInterval,
    CancellationToken cancellationToken,
    params AcmeOrderStatus[] expectedStatuses)
{
    var acceptedStatuses = new HashSet<AcmeOrderStatus>(expectedStatuses);

    while (true)
    {
        var order = (await client.GetOrderAsync(account, orderUrl, cancellationToken)).Resource;

        if (acceptedStatuses.Contains(order.Status))
        {
            return order;
        }

        if (order.Status == AcmeOrderStatuses.Invalid)
        {
            throw new InvalidOperationException($"Order failed. {order.Error?.Detail ?? "No additional detail was returned."}");
        }

        Console.WriteLine($"Waiting for order '{orderUrl}' to reach [{string.Join(", ", expectedStatuses)}]. Current status: {order.Status}");
        await Task.Delay(pollInterval, cancellationToken);
    }
}

static byte[] CreateCertificateSigningRequest(IReadOnlyList<string> domains, ECDsa certificateKey)
{
    var request = new CertificateRequest($"CN={domains[0]}", certificateKey, HashAlgorithmName.SHA256);
    var subjectAlternativeNames = new SubjectAlternativeNameBuilder();

    foreach (var domain in domains)
    {
        subjectAlternativeNames.AddDnsName(domain);
    }

    request.CertificateExtensions.Add(subjectAlternativeNames.Build());
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    return request.CreateSigningRequest();
}

static string GetFilePrefix(string domain)
{
    var invalidCharacters = Path.GetInvalidFileNameChars();
    var buffer = new char[domain.Length];

    for (var index = 0; index < domain.Length; index++)
    {
        var character = domain[index];
        buffer[index] = character == '*'
            ? '_'
            : invalidCharacters.Contains(character) ? '-' : character;
    }

    return new string(buffer);
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project Acmebot.Acme.Sample -- --email admin@example.com --domain example.com [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --email <value>           Account contact email address");
    Console.WriteLine("  --domain <value>          Domain name to include in the certificate. Repeatable.");
    Console.WriteLine("  --challenge <value>       dns-01 or http-01. Default: dns-01");
    Console.WriteLine("  --directory <value>       ACME directory URL. Default: Let's Encrypt staging");
    Console.WriteLine("  --output <value>          Output directory. Default: output");
    Console.WriteLine("  --poll-interval <value>   Poll interval in seconds. Default: 5");
}

sealed record SampleOptions
{
    public required Uri DirectoryUrl { get; init; }

    public required string Contact { get; init; }

    public required IReadOnlyList<string> Domains { get; init; }

    public required AcmeChallengeType ChallengeType { get; init; }

    public required string OutputDirectory { get; init; }

    public required TimeSpan PollInterval { get; init; }

    public static SampleOptions Parse(string[] args)
    {
        string? email = null;
        var domains = new List<string>();
        var challengeType = AcmeChallengeTypes.Dns01;
        var directoryUrl = new Uri("https://acme-staging-v02.api.letsencrypt.org/directory", UriKind.Absolute);
        var outputDirectory = "output";
        var pollInterval = TimeSpan.FromSeconds(5);

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--email":
                    email = GetRequiredValue(args, ref index);
                    break;
                case "--domain":
                    domains.AddRange(
                        GetRequiredValue(args, ref index)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--challenge":
                    challengeType = ParseChallengeType(GetRequiredValue(args, ref index));
                    break;
                case "--directory":
                    directoryUrl = new Uri(GetRequiredValue(args, ref index), UriKind.Absolute);
                    break;
                case "--output":
                    outputDirectory = GetRequiredValue(args, ref index);
                    break;
                case "--poll-interval":
                    pollInterval = TimeSpan.FromSeconds(int.Parse(GetRequiredValue(args, ref index)));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("The --email option is required.");
        }

        if (domains.Count == 0)
        {
            throw new ArgumentException("At least one --domain option is required.");
        }

        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("The --poll-interval option must be greater than zero.");
        }

        return new SampleOptions
        {
            DirectoryUrl = directoryUrl,
            Contact = email.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? email : $"mailto:{email}",
            Domains = domains,
            ChallengeType = challengeType,
            OutputDirectory = outputDirectory,
            PollInterval = pollInterval
        };
    }

    private static string GetRequiredValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for option: {args[index]}");
        }

        index++;
        return args[index];
    }

    private static AcmeChallengeType ParseChallengeType(string value)
    {
        return value switch
        {
            "dns-01" => AcmeChallengeTypes.Dns01,
            "http-01" => AcmeChallengeTypes.Http01,
            _ => throw new ArgumentException("The --challenge option must be either 'dns-01' or 'http-01'.")
        };
    }
}
