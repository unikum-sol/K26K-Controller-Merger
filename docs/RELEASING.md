# Releasing and code signing

Release builds are produced by GitHub Actions on GitHub-hosted runners and signed through [SignPath](https://about.signpath.io) with a certificate of the [SignPath Foundation](https://signpath.org) (free for open source projects). See the [code signing policy](../CODE_SIGNING_POLICY.md).

## Workflows

| Workflow | Trigger | What it does |
|---|---|---|
| [`ci.yml`](../.github/workflows/ci.yml) | push to `main`, pull requests | build and run the unit tests |
| [`release.yml`](../.github/workflows/release.yml) | tag `v*`, or "Run workflow" | test, publish the single-file EXE, sign it via SignPath, check the signature, attach EXE and `.sha256` to the release |

A new tag creates a **draft** release with the signed EXE. Write the release notes, then publish the draft. If the release already exists, its assets are replaced.

Signing only runs when the repository variable `SIGNPATH_ORGANIZATION_ID` is set. Without it, the workflow attaches the unsigned EXE.

## One-time setup

### 1. Apply at the SignPath Foundation
Apply for the open source program on [signpath.org](https://signpath.org). Requirements include an OSI-approved license (AGPL-3.0), a public repository, the [code signing policy](../CODE_SIGNING_POLICY.md), builds on GitHub-hosted runners and multi-factor authentication for all team members on GitHub and SignPath.

### 2. Configure the SignPath project (after approval)
1. Link the predefined **GitHub.com** trusted build system to the project.
2. Install the **SignPath GitHub App** for this repository.
3. Create an artifact configuration with the content of [`.signpath/artifact-configuration.xml`](../.signpath/artifact-configuration.xml).
4. Use the signing policy for releases (manual approval).
5. Create an API token for a user with submitter permission.

### 3. Add the values to GitHub
In the repository under *Settings → Secrets and variables → Actions*:

| Type | Name | Value |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | the API token from SignPath |
| Variable | `SIGNPATH_ORGANIZATION_ID` | organization ID from SignPath |
| Variable | `SIGNPATH_PROJECT_SLUG` | project slug |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | signing policy slug, e.g. `release-signing` |
| Variable | `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` | artifact configuration slug |

## Creating a release

1. Bump `<Version>` in `Directory.Build.props`, commit and push.
2. Create and push the tag, e.g. `git tag v1.4` and `git push origin v1.4`.
3. Approve the signing request in SignPath (the workflow waits up to one hour).
4. Write the release notes in the draft release and publish it.

Notes:
- Windows shows "SignPath Foundation" as the publisher.
- SmartScreen builds reputation over time, so a warning may still appear for the first downloads of a new signed release.
