# CLI Reference

The Acmebot CLI is a .NET tool named `acmebot` that wraps the authenticated HTTP API for automation. Use it from local shells, CI jobs, or runbooks when you want scriptable certificate operations without calling the API directly.

## Installation

Install or update the packaged .NET tool:

```bash
dotnet tool install --global Acmebot.Cli
dotnet tool update --global Acmebot.Cli
```

Confirm the command is available:

```bash
acmebot --help
```

## Syntax

```text
acmebot [global-options] <command> <subcommand> [arguments] [command-options]
```

Save the Acmebot endpoint once:

```bash
acmebot config set --endpoint https://my-acmebot.azurewebsites.net
```

When App Service Authentication uses a custom Microsoft Entra application ID URI, save it with the endpoint:

```bash
acmebot config set --endpoint https://my-acmebot.azurewebsites.net --audience api://<application-client-id>
```

Then use short commands:

```bash
acmebot certificate list
acmebot dns-zone list
acmebot certificate issue --dns-name "*.example.com" --dns-provider "Azure DNS"
acmebot certificate renew wildcard-example-com
acmebot certificate revoke wildcard-example-com
```

## Authentication

All commands call the Acmebot API with a Microsoft Entra ID bearer token. By default, the CLI uses `DefaultAzureCredential` from Azure Identity, so `az login`, managed identity, Visual Studio, Azure PowerShell, and Azure Identity environment credentials can be used depending on the host environment.

Set the Acmebot application URL with saved configuration, `ACMEBOT_ENDPOINT`, or `--endpoint`:

```bash
acmebot config set --endpoint https://my-acmebot.azurewebsites.net
```

When App Service Authentication protects the API with a custom Microsoft Entra application ID URI, set the application ID URI with saved configuration, `ACMEBOT_AUDIENCE`, or `--audience`:

```bash
acmebot config set --audience api://<application-client-id>
```

The audience value must be an application ID URI or endpoint origin. Do not pass a token scope such as `user_impersonation` or `.default`; the CLI appends `/.default` internally.

When using `az login` or Azure CLI-backed credentials against an application ID URI such as `api://<application-client-id>`, the application registration used by App Service Authentication must allow the Microsoft Azure CLI public client. In the application registration, open **Expose an API**, add an authorized client application with client ID `04b07795-8ddb-461a-bbee-02f9e1bf7b46`, and select the `user_impersonation` scope.

## Global Options

| Option | Environment | Description |
| --- | --- | --- |
| `--endpoint <url>` | `ACMEBOT_ENDPOINT` | Acmebot application URL. Overrides saved configuration. Required unless saved configuration or the environment variable is set. |
| `--audience <audience>` | `ACMEBOT_AUDIENCE` | Microsoft Entra application ID URI or endpoint origin. Overrides saved configuration. Defaults to the endpoint origin. |
| `--config <path>` | `ACMEBOT_CONFIG` | CLI configuration file path. |
| `--tenant-id <id>` | `AZURE_TENANT_ID` | Microsoft Entra tenant ID. Required with explicit service principal authentication. |
| `--client-id <id>` | `AZURE_CLIENT_ID` | Service principal client ID. Required with explicit service principal authentication. |
| `--client-secret <secret>` | `AZURE_CLIENT_SECRET` | Service principal client secret. Cannot be combined with `--client-certificate-path`. |
| `--client-certificate-path <path>` | `AZURE_CLIENT_CERTIFICATE_PATH` | Service principal certificate path. Cannot be combined with `--client-secret`. |
| `--client-certificate-password <value>` | `AZURE_CLIENT_CERTIFICATE_PASSWORD` | PFX certificate password. Requires `--client-certificate-path` or `AZURE_CLIENT_CERTIFICATE_PATH`. |
| `--managed-identity-client-id <id>` | `ACMEBOT_MANAGED_IDENTITY_CLIENT_ID` | User-assigned managed identity client ID for `DefaultAzureCredential`. |
| `--format <format>` | `ACMEBOT_FORMAT` | Output format. Valid values are `table` and `json`. Defaults to `table`. |
| `--json` | | Shortcut for `--format json`. |
| `--poll-interval <seconds>` | | Operation polling interval. Defaults to `5`. |
| `--timeout <seconds>` | | Operation wait timeout. Defaults to `1800`. |
| `--help` | | Show usage information. |

`--poll-interval` and `--timeout` must be positive whole seconds.

## Configuration

Use `config set` to save CLI defaults:

```bash
acmebot config set --endpoint https://my-acmebot.azurewebsites.net
acmebot config set --audience api://<application-client-id>
```

The default configuration file is stored under the current user's profile in `.acmebot/config.json`. To use a different file, pass `--config <path>` or set `ACMEBOT_CONFIG`.

Show the saved values:

```bash
acmebot config show
```

Clear the saved values:

```bash
acmebot config clear
```

Configuration precedence is:

1. Command-line options.
2. Environment variables.
3. Saved CLI configuration.
4. Built-in defaults.

## Commands

### `certificate list`

Lists Key Vault certificates visible to Acmebot.

```bash
acmebot certificate list
```

Table output includes certificate name, expiration time, enabled state, DNS provider, and DNS names. JSON output returns the API certificate objects.

### `certificate issue`

Starts certificate issuance.

```bash
acmebot certificate issue --dns-name "*.example.com" --dns-provider "Azure DNS"
```

| Option | Description |
| --- | --- |
| `--dns-name <value>` | DNS name to include in the certificate. Required and repeatable. Duplicate names are removed case-insensitively. |
| `--name <value>` | Key Vault certificate name. If omitted, Acmebot derives the name from the first DNS name. Only letters, numbers, and hyphens are allowed. |
| `--dns-provider <value>` | DNS provider display name, such as `Azure DNS` or `Cloudflare`. Required when Acmebot cannot infer a single provider. |
| `--key-type <type>` | Certificate key type. Valid values are `RSA` and `EC`. Defaults to `RSA`. |
| `--key-size <size>` | RSA key size. Valid values are `2048`, `3072`, and `4096`. Defaults to `2048`. Valid only with `--key-type RSA`. |
| `--key-curve <curve>` | EC key curve. Valid values are `P-256`, `P-384`, `P-521`, and `P-256K`. Defaults to `P-256`. Valid only with `--key-type EC`. |
| `--reuse-key` | Reuse the existing Key Vault certificate key. |
| `--dns-alias <value>` | DNS-01 validation alias. |
| `--tag <name=value>` | Key Vault certificate tag. Repeatable. The `Acmebot` tag name is reserved. |
| `--no-wait` | Return after the operation is accepted instead of polling until completion. |

By default, `certificate issue` waits for the Durable Functions operation to complete and prints the operation instance ID. Use `--no-wait` for asynchronous scripts.

### `certificate renew`

Starts manual renewal for an existing Key Vault certificate.

```bash
acmebot certificate renew wildcard-example-com
```

Use `--no-wait` to return after Acmebot accepts the renewal operation.

### `certificate revoke`

Revokes an existing certificate through the configured ACME certificate authority and disables the current Key Vault certificate version.

```bash
acmebot certificate revoke wildcard-example-com
```

### `dns-zone list`

Lists DNS zones discovered from the configured DNS providers.

```bash
acmebot dns-zone list
```

Table output includes DNS provider and zone name. JSON output returns grouped DNS provider results.

### `operation wait`

Waits for a previously accepted issuance or renewal operation to complete.

```bash
acmebot operation wait <instance-id>
```

Pass only the operation instance ID returned by `certificate issue --no-wait` or `certificate renew --no-wait`. Operation URLs and `/api/operations/...` paths are rejected.

## Output

Use table output for humans:

```bash
acmebot certificate list
```

Use JSON output for automation:

```bash
acmebot certificate list --json
acmebot dns-zone list --format json
```

Operation commands return an operation status and instance ID. With `--no-wait`, the status is `accepted`; after waiting, the status is `completed`.

## Exit Codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `2` | Usage error, such as an unknown option or missing required argument. |
| `3` | API error or operation timeout. |
| `4` | Authentication failed. |
| `5` | Network request failed. |
| `130` | Canceled. |
