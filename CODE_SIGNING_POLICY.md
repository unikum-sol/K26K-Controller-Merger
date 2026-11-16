# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

## What is signed

Only `K26KControllerMerger.exe` attached to the [releases](https://github.com/unikum-sol/K26K-Controller-Merger/releases) of this repository is signed. It is built by the [release workflow](.github/workflows/release.yml) on GitHub-hosted runners directly from the source code in this repository, and every signing request is approved manually before the certificate is used.

The ViGEmBus and HidHide driver installers are not part of this project and are not signed by it. The app downloads them only when the user starts the installation, and checks their SHA-256 hash and their original publisher signature (Nefarius Software Solutions e.U.) before running them.

## Team roles

| Role | Members |
|---|---|
| Committers and reviewers | [unikum-sol](https://github.com/unikum-sol) |
| Approvers | [unikum-sol](https://github.com/unikum-sol) |

All team members use multi-factor authentication for GitHub and SignPath.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

The only network access happens when the user installs a required driver from within the app (ViGEmBus in the setup wizard, HidHide via the "Install HidHide" button). The installer is then downloaded from the driver's official GitHub release page. No usage data, telemetry or personal data is collected or sent.
