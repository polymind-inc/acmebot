# Copilot code review

Use [CONTRIBUTING.md](../CONTRIBUTING.md) for contribution and validation requirements. Use the deployment templates under `deploy/` and the [deployment guide](../docs/guide/deployment.md), [migration guide](../docs/guide/migration-v5.md), and [architecture reference](../docs/reference/architecture.md) to establish which environments and configurations Acmebot supports.

## Evaluate the change in context

- Trace the changed code through its callers and the supported configurations that can reach it. Check where inputs come from, who controls them, and which trust boundary the change affects. If a finding depends on a particular plan, OS, permission, or custom configuration, name that prerequisite and distinguish confirmed behavior from an assumption.
- Evaluate new deployments and in-place upgrades against their documented infrastructure and operational assumptions. Reassess those assumptions when a change expands a supported configuration or changes a trust boundary.
- Check compatibility with existing state, configuration, public contracts, and in-flight work. For retries, partial failures, and concurrent operations, trace which work can repeat and who owns cleanup. Follow resource ownership through the calling code before reporting a leak.

## Write actionable findings

- Use the PR description, linked issue, and maintainer comments to establish scope. If a limitation is pre-existing or explicitly deferred, treat it as follow-up work unless the PR introduces it, materially worsens it, or needs to address it to deliver the stated behavior. Explain that connection before asking the author to expand the PR.
- Prioritize correctness, security, compatibility, and operability regressions introduced or exposed by the change. Anchor each finding to the relevant changed line and explain the condition, the observable impact, and the smallest effective fix.
- Recommend changes proportionate to the demonstrated impact. Treat a style preference or a configuration Acmebot does not support as optional context when it materially helps the author. When the support status or a prerequisite is unclear, state what needs verification before assigning severity.
- Use [CONTRIBUTING.md](../CONTRIBUTING.md) to identify the tests, documentation, and generated artifacts the change requires.
