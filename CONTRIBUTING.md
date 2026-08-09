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
dotnet restore ./Acmebot.slnx
dotnet build -c Release ./Acmebot.slnx
dotnet format --verify-no-changes --verbosity detailed --no-restore ./Acmebot.slnx
az bicep build -f ./deploy/azuredeploy.bicep
```

These commands cover the contributor-facing validation checks.

## Pull Request Guidelines

- Keep pull requests focused on a single change.
- Include documentation updates when behavior, configuration, or deployment changes.
- Add or update tests when the change affects behavior that can be validated automatically.
- Avoid unrelated refactoring in the same pull request.
- Do not commit secrets, certificates, or populated `local.settings.json` values.

## Issue and Pull Request Classification

Issues use GitHub Issue Types as their primary classification:

- `Bug` for unexpected behavior
- `Feature` for requests, ideas, or new functionality
- `Task` for a specific piece of implementation or documentation work

Maintainers assign the organization-level `Priority` issue field during triage. An unset priority means the issue has not been prioritized yet. Labels beginning with `area:` identify affected components, and labels beginning with `status:` describe the current triage state.

Pull requests use exactly one release category label:

- `bug`
- `enhancement`
- `documentation`
- `dependencies`
- `maintenance`

These release category labels are reserved for pull requests. For pull requests whose branch is in this repository, the official GitHub pull request labeler adds `documentation` and `area:` labels from changed paths. It only adds labels and does not remove manually assigned labels. Maintainers label pull requests from forks and assign `bug`, `enhancement`, `maintenance`, and `breaking-change` when applicable, while Dependabot applies dependency labels through its repository configuration. Pull request priority is inherited from its linked issue rather than represented by a label.

## Release Publishing

The `Publish` workflow runs for version tags such as `v5.0.0`. Before pushing the tag, create a matching draft GitHub Release. The workflow uploads the Function App package, publishes the CLI package to NuGet, and then publishes the draft release.

NuGet publishing uses Trusted Publishing. Configure a nuget.org trusted publishing policy for repository `polymind-inc/acmebot` and workflow file `publish.yml`, then set the repository secret `NUGET_USER` to the nuget.org profile name used by that policy.

## Submission Checklist

- Build succeeds locally.
- Formatting check passes locally.
- Deployment template changes are validated with Bicep.
- The pull request description explains the problem and the proposed fix.

## Code of Conduct

This project follows the guidelines in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
