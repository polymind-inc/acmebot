# Contributing

Thanks for your interest in contributing to Acmebot.

## Before You Start

- Use GitHub Discussions for usage questions and design discussions.
- Use GitHub Issues for confirmed bugs and feature requests.
- Do not report security vulnerabilities in public issues or discussions. Follow [SECURITY.md](SECURITY.md).

## Development Setup

### Prerequisites

- .NET SDK 10
- Node.js 24 with npm
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

### Backend

```bash
dotnet restore ./Acmebot.slnx
dotnet format --verify-no-changes --verbosity detailed --no-restore ./Acmebot.slnx
dotnet build -c Release --no-restore ./Acmebot.slnx
dotnet test -c Release --no-build --no-restore ./Acmebot.slnx
```

### Dashboard

```bash
npm --prefix ./src/Acmebot.App/ClientApp ci
npm --prefix ./src/Acmebot.App/ClientApp run lint
npm --prefix ./src/Acmebot.App/ClientApp run build
```

### Documentation

```bash
npm --prefix ./docs ci
npm --prefix ./docs run build
```

### Deployment templates

Use the same Bicep version as CI when regenerating the ARM JSON templates:

```bash
az config set bicep.use_binary_from_path=false
az bicep install --version v0.48.1
az bicep build -f ./deploy/azuredeploy.bicep
az bicep build -f ./deploy/update/azuredeploy.bicep
az bicep build -f ./deploy/migrate/azuredeploy.bicep
```

Commit regenerated ARM JSON files alongside Bicep changes. CI checks that rebuilding the templates leaves the working tree unchanged.

These commands match the checks in the [CI](.github/workflows/ci.yml) and [Docs](.github/workflows/docs.yml) workflows.

## Pull Request Guidelines

- Keep pull requests focused on a single change.
- Include documentation updates when behavior, configuration, or deployment changes.
- Add or update tests when the change affects behavior that can be validated automatically.
- Avoid unrelated refactoring in the same pull request.
- Do not commit secrets, certificates, or populated `local.settings.json` values.

## Commit and Pull Request Writing Style

Use this style for contributor-written and agent-generated commit messages, pull request titles, and pull request descriptions in this repository. Automated dependency update messages may retain the bot's own format.

### Commit subjects and pull request titles

- Write a short English imperative sentence starting with a capitalized verb, such as `Add`, `Fix`, `Update`, `Remove`, or `Document`.
- Describe the specific change or resulting behavior. Avoid vague subjects such as `Fix issues` or `Update files`.
- Aim for 72 characters or fewer, with no trailing period.
- Do not add Conventional Commits prefixes such as `feat:` or `docs:`, emoji, or manually appended pull request numbers.
- Describe the individual change in a commit subject and the overall delivered change in a pull request title. They may be identical for a single-change pull request.

Examples:

```text
Fix certificate renewal error handling
Add DNS-PERSIST-01 capability validation
Document Key Vault CSR compatibility limitations
Update Azure Functions SDK and xUnit packages
```

### Commit bodies

Omit the body when the subject is sufficient. Otherwise, separate it from the subject with a blank line and explain why the change is needed, along with any material behavior changes or limitations. Use English prose or concise bullets. Include issue references in the body when relevant.

### Pull request descriptions

Use [.github/pull_request_template.md](.github/pull_request_template.md), retain its headings, and write the content in English:

- **Summary:** Explain the concrete problem and resulting behavior in one or two sentences.
- **Related Issue:** Use `Closes #123` only when the pull request resolves that issue, or `Related to #123` for context. Write `None` when there is no linked issue.
- **What Changed:** List the meaningful changes that help a reviewer understand the implementation. Describe the final scope and relevant design choices rather than the sequence of work.
- **Validation:** Record the checks actually performed and their results. Mark a checkbox only when the check passed. State why a check failed, was not run, or is not applicable, and add relevant checks beyond the template when needed.
- **Notes:** Describe material limitations, compatibility considerations, breaking changes, or deployment steps. Write `None` when there are none.

Keep descriptions proportional to the change. Remove placeholder text and template comments when filling in the description, and avoid repeating the same explanation across sections.

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

The [Release](.github/workflows/release.yml) workflow runs for version tags such as `v5.0.0`. Before pushing the tag, create a matching draft GitHub Release. The workflow uploads the Function App package, publishes the CLI package to NuGet, and then publishes the draft release.

NuGet publishing uses Trusted Publishing. Configure a nuget.org trusted publishing policy under the `shibayan` profile for repository `polymind-inc/acmebot` and workflow file `release.yml`. The workflow sets the `NuGet/login` action's `user` input to `shibayan`; update that input and the matching policy together if the publishing profile changes.

## Submission Checklist

- Build succeeds locally.
- Formatting check passes locally.
- Backend tests pass locally.
- Dashboard lint and build pass when dashboard files change.
- Documentation build passes when documentation files change.
- Deployment template changes are validated with Bicep.
- The pull request description explains the problem and the proposed fix.

## Code of Conduct

This project follows the guidelines in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
