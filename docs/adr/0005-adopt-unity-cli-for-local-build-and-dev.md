# ADR 0005 — Adopt the Unity CLI for Local Build/Dev Automation (keep UnityMCP for authoring)

- **Status:** Proposed
- **Date:** 2026-07-21
- **Deciders:** Project owner
- **Context of decision:** Follow-up after Unity's official Unity CLI launch (Unite 2026, ~July 2026).

## Context
Unity released an official, standalone **Unity CLI** (a self-contained `unity` binary) plus a
**Pipeline** package. The CLI manages editors/modules/projects/auth from the terminal and supports
headless builds (`unity build`), test runs (`unity test`), structured output (`json`/`tsv`/`ndjson`),
non-interactive/CI mode, and an `mcp` mode. Unity positions it as the successor to the standalone
Unity MCP, but it ships an MCP compatibility mode so existing MCP-based agents keep working.

This project currently uses the **community UnityMCP** as the interactive agent bridge, plus the
Meta XR Unity MCP Extension for Meta-specific work ([ADR 0001](0001-use-meta-xr-unity-mcp-extension.md)).

**Relationship to [ADR 0004](0004-adopt-unity-cli-for-build-and-ci.md):** ADR 0004 (Accepted)
adopts the Unity CLI for build/CI automation, including running **EditMode tests in CI via
`unity test` on a self-hosted runner**. This ADR is the *local/developer-machine* companion to that
decision — the same CLI, used before pushing. (An earlier ADR 0004 chose GameCI for CI; it was
reverted off `dev` and replaced — see ADR 0004 for the history.)

## Decision
Adopt the **Unity CLI as a local/dev build and automation tool** — reproducible editor/module setup,
headless Quest 3 `build`s, and local `test` runs before pushing — while **keeping the community
UnityMCP as the live authoring bridge** (scene/GameObject/script edits, console reads). Do **not**
migrate the interactive agent path to the CLI's MCP mode under this ADR. (The CI test runner is
decided in [ADR 0004](0004-adopt-unity-cli-for-build-and-ci.md): the Unity CLI on a self-hosted runner.)

## Scope of this ADR
- In scope: installing the Unity CLI as a dev tool; using `unity build` / `unity test` /
  `unity install` locally for reproducible builds and pre-push checks.
- Out of scope (deferred, would need a new/updated ADR):
  - Replacing community UnityMCP with the CLI's `mcp` mode.
  - (The CI test runner is out of scope here; it is decided in ADR 0004.)

## Verified on this machine (2026-07-21)
- Installed via the official CDN script (beta channel); binary at
  `%LOCALAPPDATA%\Unity\bin\unity.exe`, version **1.0.0-beta.2**.
- `unity env` resolves the Unity Hub environment; `unity editors list` sees the installed
  **6000.3.20f1** editor with **Android** modules (this project's Quest 3 target).
- Relevant subcommands present: `build`, `test`, `mcp`, `pipeline`, `command`, `install`.
- `unity status` shows no connected Editor yet (the Pipeline package is not installed in a running
  Editor; not required for install/verify).

## Rationale
- Official, Unity-maintained tooling → better longevity than hand-scripting the Editor.
- Self-contained binary gives every developer an identical, reproducible toolchain (pinned editor +
  Android modules) with one command.
- Local headless `build`/`test` shortens the loop: verify the Quest APK builds and unit tests pass
  before opening a PR, complementing the self-hosted Unity-CLI CI gate from ADR 0004.

## Consequences
- Positive: reproducible local builds/tests; a first-party path aligned with Unity's direction.
- Negative / risks: the CLI is early (beta `1.0.0-beta.2`); behavior may change across releases.
  Two bridges into the same Editor must not run concurrently — the CLI's MCP mode and community
  UnityMCP should not both drive one running Editor at once. Mitigation: use the CLI **headless**,
  not as a second interactive bridge.

## Alternatives Considered
- **Switch fully to the Unity CLI (MCP mode) now**, retiring community UnityMCP — rejected as
  premature while the CLI is beta and the community bridge works.
- **Ignore the CLI** — rejected; misses reproducible local builds with little downside.

## Notes
This ADR records a tooling/workflow decision and does not assert CLI capabilities beyond those
verified above or documented by Unity.
