## Summary

<!-- Explain the concrete problem and resulting behavior in one or two English sentences. -->

## Related Issue

<!-- Use Closes #123, Related to #123, or None. -->

## What Changed

<!-- List meaningful changes and relevant design choices as concise English bullets. -->

## Validation

<!-- Check only passed checks. For unchecked items, explain failures, why they were not run, or why they are not applicable. Add other relevant checks and results. -->

- [ ] `dotnet build -c Release ./Acmebot.slnx`
- [ ] `dotnet format --verify-no-changes --verbosity detailed --no-restore ./Acmebot.slnx`
- [ ] `az bicep build -f ./deploy/azuredeploy.bicep`
- [ ] Documentation updated if needed

## Notes

<!-- Describe limitations, compatibility considerations, breaking changes, or deployment steps, or write None. Remove template comments before submitting. -->
