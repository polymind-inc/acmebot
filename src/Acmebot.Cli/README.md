# Acmebot CLI

Command-line client for Acmebot HTTP API automation.

## Usage

```powershell
acmebot --endpoint https://<function-app>.azurewebsites.net certificate list --json
acmebot --endpoint https://<function-app>.azurewebsites.net dns-zone list
acmebot --endpoint https://<function-app>.azurewebsites.net certificate issue --dns-name "*.example.com" --dns-provider "Azure DNS"
acmebot --endpoint https://<function-app>.azurewebsites.net certificate renew wildcard-example-com
acmebot --endpoint https://<function-app>.azurewebsites.net certificate revoke wildcard-example-com
```

Use `--audience <application-id-uri>` when App Service Authentication protects the API with a custom Microsoft Entra application ID URI. The value should be an application ID URI such as `api://<application-client-id>`, not a token scope.
