# ADR 0006 — Prefer the Meta XR SDK Toolkit Over Custom VR/MR Code

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Context of decision:** Working session validating Meta Building Blocks creation via the Unity MCP.

## Context
Quest 3 AR/MR work in this project (camera rig, passthrough, hand/controller tracking,
interaction, spatial anchors, MRUK trackables, etc.) is repeatedly the kind of thing that
*could* be written by hand but is also already provided, first-party, by the Meta XR SDK —
as **Building Blocks**, the **Interaction SDK**, **MRUK**, and the **Core SDK** components.

During this session we confirmed the project's Meta XR SDK exposes ~69 installable
Building Blocks (Camera Rig, Passthrough, Hand Tracking, Controller Tracking, Ray/Poke/Grab
interactions, Interactions Rig, spatial anchors, MRUK, and more), each backed by
Meta-authored prefabs and components (`OVRCameraRig`, `OVRManager`, `OVRHand`, `OVRSkeleton`,
`OVRPassthroughLayer`, `BuildingBlock`, …).

Hand-rolling equivalents of these (custom rig wiring, custom hand-tracking meshes, custom
interaction/grab logic, custom anchor management) means reimplementing — and then maintaining —
behaviour Meta already ships, tests against the platform, and updates with each SDK release.
It also drifts from current Meta-platform conventions and is more error-prone.

This complements [ADR 0001](0001-use-meta-xr-unity-mcp-extension.md) (which chose the Meta XR
Unity MCP Extension as the tool for Meta-XR *Editor operations*) and
[ADR 0002](0002-detect-qr-with-mruk-trackables.md) (which chose first-party MRUK Trackables
over a hand-built detection pipeline). This ADR generalises that same principle into a standing
rule for all VR/MR work in the project.

## Decision
**Default to the Meta XR SDK toolkit. Do not write VR/MR functionality from scratch when the
Meta XR SDK already provides a Building Block, Interaction SDK feature, MRUK capability, or
Core SDK component for it.**

Concretely, for any VR/MR capability:
1. **Check the SDK first.** Look for a matching Meta XR **Building Block**, Interaction SDK
   feature, MRUK API, or Core SDK component before writing new code.
2. **If one exists, use it** — install/compose the SDK block/component rather than
   reimplementing its behaviour.
3. **Only write custom code** when no SDK equivalent exists, the SDK one is a documented
   poor fit (e.g. the low-frequency pose limitation noted in ADR 0002), or the custom code is
   *glue on top of* SDK primitives — not a replacement for them.
4. **Record the exception.** When custom VR/MR code is written despite an overlapping SDK
   feature, note why in the PR (and, if it is an architectural choice, in a new/updated ADR).

## Rationale
- **First-party correctness:** Meta's blocks/components reflect current platform conventions
  and are validated against the device; custom equivalents are not.
- **Less code to own:** the SDK carries the maintenance, lifecycle, and per-release updates.
- **Consistency:** aligns with the tooling choice in ADR 0001 and the detection choice in
  ADR 0002 — this project has already chosen "first-party over hand-built" twice.

## Consequences
- Positive: less bespoke VR code, fewer platform bugs, easier upgrades, faster delivery.
- Negative / constraints:
  - Adds a "search the SDK first" step to VR/MR tasks.
  - Ties features to SDK behaviour and its caveats (e.g. beta-gated capabilities per ADR 0001,
    pose-frequency limits per ADR 0002).
  - "An SDK feature exists for this" is a judgement call; borderline cases should be raised in
    review rather than assumed.

## Scope
Applies to VR/MR runtime and setup functionality (rig, passthrough, tracking, interaction,
anchors, MRUK, manifest/permissions). It does **not** force SDK use for ordinary,
non-XR application logic (QR payload handling, networking to the download URL, UI/business
logic), where normal engineering judgement applies.

## Alternatives Considered
- **Case-by-case, no standing rule** — rejected: leaves the "reinvent vs. reuse" decision
  implicit and lets custom code accrete where SDK blocks already exist.
- **Mandate SDK use with no exceptions** — rejected: some needs have no SDK equivalent or hit
  documented SDK limits (see ADR 0002); a hard mandate would force worse solutions.
