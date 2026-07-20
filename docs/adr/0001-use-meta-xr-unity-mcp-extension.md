# ADR 0001 — Use the Meta XR Unity MCP Extension for Meta-XR Editor Work

- **Status:** Accepted
- **Date:** 2026-07-20
- **Deciders:** Project owner
- **Context of decision:** Made during project planning follow-up (not part of the original Grill Me session).

## Context
This project (QR Reader — Quest 3 AR) requires Meta-XR-specific Unity Editor setup: a Camera Rig, passthrough, Android manifest/permission configuration (Spatial Data permission), and MRUK trackable configuration. These are Meta-platform tasks where general Unity tooling has limited, non-authoritative knowledge.

The `com.meta.xr.unity-mcp.extension` package (from [meta-quest/Unity-MCP-Extensions](https://github.com/meta-quest/Unity-MCP-Extensions)) is already present in the project manifest. It augments the base Unity MCP with Meta-XR-specific tooling.

## Decision
Use the **Meta XR Unity MCP Extension** as the preferred, authoritative tool for Meta-XR-specific Editor operations in this project (rig setup, manifest/permission configuration, Interaction SDK setup), in preference to hand-configuring these or relying on non-Meta-specific tooling.

## Rationale
- It is Meta's own extension, purpose-built for Meta Quest XR development, so it reflects current Meta-platform conventions more reliably than generic tooling.
- Its documented capabilities map directly onto this project's required device setup.

## Verified Capabilities (per the official repository)
- **Core SDK tools:** Camera Rig setup for VR/MR; Android manifest configuration management; configuration file access.
- **Interaction SDK tools:** poke/ray interaction, grabbables, interaction rig assembly, teleport hotspots, interactor state control.
- **Skill Importer:** imports reusable AI workflows ("skills") from Meta's agentic-tools repository.

## Requirements / Caveats (per the official repository — verify against this install)
- Meta Core SDK v78+ and Interaction SDK v78+ (this project is on SDK v203, which clears this bar).
- Some features require the **Unity Assistant package** and **Unity AI Gateway (beta early access)** — availability not confirmed for this project.
- Installed via Git URL (already in `Packages/manifest.json`).

## Consequences
- Positive: Meta-XR setup tasks (rig, manifest, permissions) are handled by a Meta-authored tool aligned with the platform.
- Negative / risks: some capabilities are beta-gated (Unity Assistant + AI Gateway) and may not be usable here; a Git-URL dependency can shift with upstream changes.

## Alternatives Considered
- **Manual Editor configuration** of rig, manifest, and permissions — more error-prone for Meta-specific conventions.
- **Base Unity MCP only** (no Meta extension) — lacks Meta-XR-specific knowledge and tools.

## Notes
This ADR records a tooling/workflow decision made after the planning session, not a decision from the Grill Me session itself. It does not assert capabilities beyond those documented in the official repository.
