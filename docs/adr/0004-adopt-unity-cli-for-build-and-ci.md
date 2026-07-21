# ADR 0004 — Adopt the Unity CLI for Build/CI Automation (keep UnityMCP for authoring)

- **Status:** Proposed
- **Date:** 2026-07-21
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

## Decision
Adopt the **Unity CLI for build and CI/automation** tasks (headless builds, test runs, editor/module
management on CI workers), while **keeping the community UnityMCP as the live authoring bridge**
(scene/GameObject/script edits, console reads) for now. Do **not** migrate the interactive agent path
to the CLI's MCP mode at this time.

## Scope of this ADR
- In scope: installing the Unity CLI as a project/dev tool; using it for build/test/CI.
- Out of scope (deferred): replacing community UnityMCP with the CLI's `mcp` mode. Revisit once the
  CLI (currently `1.0.0-beta.2`) has more community mileage — see the wait-and-see rationale.

## Verified on this machine (2026-07-21)
- Installed via the official CDN script (beta channel); binary at
  `%LOCALAPPDATA%\Unity\bin\unity.exe`, version **1.0.0-beta.2**.
- `unity env` resolves the Unity Hub environment; `unity editors list` sees the installed
  **6000.3.20f1** editor with **Android** modules (this project's Quest 3 target).
- Relevant subcommands present: `build`, `test`, `mcp`, `pipeline`, `command`, `install`.
- `unity status` shows no connected Editor yet (the Pipeline package is not installed in a running
  Editor; not required for install/verify).

## Rationale
- Official, Unity-maintained tooling for automation → better longevity than scripting the Editor by hand.
- Self-contained binary suits headless CI workers (no Hub desktop app required).
- Structured output + non-interactive mode + service-account auth are purpose-built for CI.
- Coexistence avoids risk: the proven authoring path (UnityMCP) is untouched.

## Consequences
- Positive: a clean, first-party path for build/test automation and future CI, aligned with Unity's
  direction.
- Negative / risks: the CLI is early (beta); behavior may change across releases. Two bridges into the
  same Editor must not run concurrently — the CLI's MCP mode and community UnityMCP should not both
  drive one running Editor at the same time. We mitigate by using the CLI **headless** for build/CI,
  not as a second interactive agent bridge.

## Alternatives Considered
- **Switch fully to the Unity CLI (MCP mode) now**, retiring community UnityMCP — rejected as
  premature while the CLI is beta and the community bridge works.
- **Ignore the CLI, keep hand-rolled/Editor-only build steps** — rejected; misses first-party CI
  automation with little upside.

## Notes
This ADR records a tooling/workflow decision. It does not assert CLI capabilities beyond those
verified above or documented by Unity. Adoption for interactive authoring (CLI MCP mode) would
require updating this ADR or a new one.
