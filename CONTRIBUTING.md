# Contributing

Thanks for your interest in contributing to Acmebot.

## Before You Start

- Use GitHub Discussions for usage questions and design discussions.
- Use GitHub Issues for confirmed bugs and feature requests.
- Do not report security vulnerabilities in public issues or discussions. Follow [SECURITY.md](SECURITY.md).

## Development Setup

### Prerequisites

- .NET SDK 10
- Azure CLI
- Bicep CLI support through Azure CLI
- Git

### Clone the repository

```bash
git clone https://github.com/polymind-inc/acmebot.git
cd acmebot
```

## Build and Validation

Run these commands from the repository root.

```bash
dotnet restore ./src
dotnet build -c Release ./src
dotnet format --verify-no-changes --verbosity detailed --no-restore ./src
az bicep build -f ./deploy/azuredeploy.bicep
```

These commands mirror the checks currently enforced in CI.

## Pull Request Guidelines

- Keep pull requests focused on a single change.
- Include documentation updates when behavior, configuration, or deployment changes.
- Add or update tests when the change affects behavior that can be validated automatically.
- Avoid unrelated refactoring in the same pull request.
- Do not commit secrets, certificates, or populated `local.settings.json` values.

## Submission Checklist

- Build succeeds locally.
- Formatting check passes locally.
- Deployment template changes are validated with Bicep.
- The pull request description explains the problem and the proposed fix.

## Code of Conduct

This project follows the guidelines in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
