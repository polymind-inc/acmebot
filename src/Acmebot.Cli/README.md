# Acmebot CLI

Command-line client for Acmebot HTTP API automation.

Full command syntax and authentication details are available in the [CLI Reference](../../docs/reference/cli.md).

## Usage

```powershell
acmebot config set --endpoint https://<function-app>.azurewebsites.net
acmebot certificate list --json
acmebot dns-zone list
acmebot certificate issue --dns-name "*.example.com" --dns-provider "Azure DNS"
acmebot certificate renew wildcard-example-com
acmebot certificate revoke wildcard-example-com
```

Use `acmebot config set --audience <application-id-uri>` when App Service Authentication protects the API with a custom Microsoft Entra application ID URI. The value should be an application ID URI such as `api://<application-client-id>`, not a token scope.
