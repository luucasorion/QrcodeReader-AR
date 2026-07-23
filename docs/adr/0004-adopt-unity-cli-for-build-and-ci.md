# ADR 0004 — Adopt the Unity CLI for Build/CI Automation (keep UnityMCP for authoring)

- **Status:** Accepted
- **Date:** 2026-07-21 (proposed); 2026-07-23 (accepted, extended to the CI test runner)
- **Deciders:** Project owner
- **Context of decision:** Follow-up after Unity's official Unity CLI launch (Unite 2026, ~July 2026).

## Context
Unity released an official, standalone **Unity CLI** (a self-contained `unity` binary) plus a
**Pipeline** package. The CLI manages editors/modules/projects/auth from the terminal and supports
headless builds, test runs, structured output (`json`/`tsv`/`ndjson`), non-interactive/CI mode, and
an `mcp` mode. It is positioned by Unity as the successor to the standalone Unity MCP, but ships an
MCP compatibility mode so existing MCP-based agents keep working.

This project currently uses the **community UnityMCP** as the interactive agent bridge, plus the
Meta XR Unity MCP Extension for Meta-specific work (see [ADR 0001](0001-use-meta-xr-unity-mcp-extension.md)).
We want the CLI's automation/CI strengths **without** disrupting the working authoring setup.

The [implementation plan](../implementation-plan.md) commits to automated tests for the
security-sensitive, off-device logic: the content-type classifier (M2-T6) and the content-resolver
safety guards — HTTPS-only, timeout, and the ~25 MB download cap (M2-T7). These guards protect an
**untrusted-input** path (architecture.md §7, §8), so a regression should fail loudly on the PR that
introduces it, not on-device weeks later. That requires a CI test runner.

### Supersedes a reverted GameCI attempt
An earlier ADR 0004 ("CI runs EditMode tests via GameCI") plus a GameCI workflow and an EditMode test
assembly were merged and then **reverted off `dev`** (commit `1639a20 "Revert Dev"`). GameCI activates
Unity in a hosted runner via a license secret, whose only Personal-license source was Unity's manual
`.alf`→`.ulf` activation — which **Unity has removed for Personal licenses**, and Unity 6's licensing
client leaves no extractable `.ulf`. The hosted-GameCI path is therefore not viable for this project's
Personal license. This ADR reuses the `0004` number (the GameCI file no longer exists on `dev`) and
records the replacement decision.

## Decision
Adopt the **Unity CLI for build and CI/automation** tasks (headless builds, test runs, editor/module
management), while **keeping the community UnityMCP as the live authoring bridge** (scene/GameObject/
script edits, console reads) for now. Do **not** migrate the interactive agent path to the CLI's
`mcp` mode at this time.

**CI test runner.** CI runs **EditMode tests only**, via `unity test --mode EditMode` on a
**self-hosted GitHub Actions runner**, gating pull requests into `dev` and `main`:
- The runner host is signed into Unity once (`unity auth login`) and carries the editor pinned in
  `ProjectSettings/ProjectVersion.txt`. Activation lives on the host, so **no Unity license
  credentials are stored as CI secrets** — this sidesteps the removed-Personal-activation problem
  above.
- Scope is `--mode EditMode` (pure-C# logic: classifier, resolver guards, decode-failure paths).
  PlayMode / on-device behaviour is explicitly **out of CI scope** (no headset/MRUK in CI).
- Triggers: `pull_request` into `dev`/`main`, plus manual `workflow_dispatch`.
- Workflow: [`.github/workflows/unity-editmode-tests.yml`](../../.github/workflows/unity-editmode-tests.yml);
  tests live in the `QRReader.Tests.EditMode` assembly (`Assets/Tests/EditMode/`).

## Scope of this ADR
- In scope: installing the Unity CLI as a project/dev tool; using it for build/test; running EditMode
  tests in CI on a self-hosted runner.
- Out of scope (deferred): replacing community UnityMCP with the CLI's `mcp` mode (revisit once the
  CLI — currently `1.0.0-beta.2` — has more community mileage); hosted-runner CI (revisit only if a
  Unity Cloud service account + seat-based entitlement is provisioned).

## Verified on this machine (2026-07-21 / 2026-07-23)
- Installed via the official CDN script (beta channel); binary at
  `%LOCALAPPDATA%\Unity\bin\unity.exe`, version **1.0.0-beta.2**.
- `unity env` resolves the Unity Hub environment; `unity editors list` sees the installed
  **6000.3.20f1** editor with **Android** modules (this project's Quest 3 target).
- Subcommands present: `build`, `test`, `mcp`, `pipeline`, `command`, `install`, `auth`.
- `unity test` supports `--mode EditMode`, `--non-interactive`, `--allow-install`, and NUnit-XML
  `--output`; `unity auth login` supports browser sign-in or `--client-id/--client-secret` (service
  account) for headless use.

## Rationale
- Official, Unity-maintained tooling for automation → better longevity than scripting the Editor by hand.
- Self-contained binary suits headless workers (no Hub desktop app required).
- A **self-hosted** runner is the only path that keeps CI green for a Personal license on Unity 6
  without a paid seat or an unproven cloud entitlement.
- Directly protects the untrusted-input guards (§8) at the point of change; EditMode tests are fast
  and headset-free, matching plan R1's "keep decoder/classifier/renderer unit-testable off-device."
- Coexistence avoids risk: the proven authoring path (UnityMCP) is untouched.

## Consequences
- Positive: a clean, first-party path for build/test automation; security guards and classifier
  routing are regression-gated on every PR; no license secrets in the repo/CI.
- Negative / risks:
  - The CLI is early (beta); behavior may change across releases.
  - A **self-hosted runner must be operated** (registered, kept online, kept signed in). If it is
    offline the check does not run — an availability, not a correctness, risk.
  - Two bridges into the same Editor must not run concurrently — the CLI's MCP mode and community
    UnityMCP should not both drive one running Editor at once. Mitigation: use the CLI **headless**
    for build/CI, not as a second interactive bridge.
  - CI gives **no coverage** of detection/rendering/memory — those remain on-device checks
    (plan M5-T6, M6). CI is a floor, not the whole test story.

## Alternatives Considered
- **GameCI on hosted runners** — rejected: hosted activation of a Personal license on Unity 6 is
  unsupported (manual activation removed); this was the reverted approach.
- **Hosted runner + Unity Cloud service account** — deferred: needs a Unity org, a service account,
  and an entitlement that permits headless CI; unverified for our Personal license.
- **Switch fully to the Unity CLI (MCP mode) now**, retiring community UnityMCP — rejected as
  premature while the CLI is beta and the community bridge works.
- **No CI (run tests locally only)** — rejected: guards on untrusted input should not depend on a
  human remembering to run the Test Runner.

## Notes
This ADR records a tooling/workflow decision. It does not assert CLI capabilities beyond those
verified above or documented by Unity. Adoption for interactive authoring (CLI MCP mode) would
require updating this ADR or a new one.
