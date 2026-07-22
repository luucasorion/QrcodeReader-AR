<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Logos/logo-dark.png">
  <source media="(prefers-color-scheme: light)" srcset="Logos/logo-light.png">
  <img alt="QRcodeReader AR" src="Logos/logo-light.png" width="560">
</picture>

<br>

[![Status: early development](https://img.shields.io/badge/status-early%20development-orange)](docs/implementation-plan.md)
[![Platform: Quest 3 / 3S](https://img.shields.io/badge/platform-Quest%203%20%2F%203S-1c1e21)](docs/project-context.md)
[![Engine: Unity 6 + URP](https://img.shields.io/badge/engine-Unity%206%20%2B%20URP-000000)](docs/architecture.md)
[![Meta XR SDK v203](https://img.shields.io/badge/Meta%20XR%20SDK-v203-0467df)](docs/adr/0001-use-meta-xr-unity-mcp-extension.md)
[![License: internal / TBD](https://img.shields.io/badge/license-internal%20%2F%20TBD-lightgrey)](#license)

</div>

A Meta Quest 3 / 3S **passthrough mixed-reality** app: you see the real world through
passthrough, and when the headset detects a printed **QR code**, the app downloads the media
referenced by the QR's URL and renders it — an **image or GIF** — in place of the physical code,
tracked to its real-world pose. Multiple QR codes are tracked and rendered at once.

> **Note:** this is an **AR / passthrough MR** experience — the real world stays visible — **not**
> VR (a fully virtual scene). The docs and this README use "AR / passthrough MR" throughout.

---

## Status

🚧 **Early development.** Planning is complete — the [docs](docs/) and [ADRs](docs/adr/) are
written — and implementation is just beginning. **Nothing runs on-device yet.** The features below
describe the *target* MVP, not shipped behaviour.

Progress is tracked as milestones M0–M6 in the [implementation plan](docs/implementation-plan.md):

| ID | Milestone | Status |
|----|-----------|--------|
| M0 | Foundation & device setup | 🚧 In progress |
| M1 | QR detection + lifecycle skeleton | ⬜ Not started |
| M2 | Content resolver + classifier | ⬜ Not started |
| M3 | Media decoders (image / GIF) | ⬜ Not started |
| M4 | Renderer + feedback states | ⬜ Not started |
| M5 | Multi-QR integration & teardown | ⬜ Not started |
| M6 | Hardening & on-device verification | ⬜ Not started |

---

## What it does

For each detected QR code, the app runs a short pipeline:

1. **Detect** — Meta MRUK reports a QR code with its pose, physical size, and payload string.
2. **Resolve** — the payload is an `https://` URL; the app downloads the bytes under safety guards
   and classifies the content type.
3. **Render** — a flush, coplanar quad is placed at the QR's pose (sized from the code and scaled by
   a configurable factor) and the image or GIF is displayed on it.
4. **Track & tear down** — the content follows the code while it's tracked; when the code leaves,
   the content instance is destroyed and its textures freed.

A loading spinner shows on detect; a shared error icon shows on any failure (bad URL, timeout,
oversized download, network error, or unsupported content type).

## Features & scope

**In scope (MVP)**
- QR detection through Quest passthrough (MRUK Trackables).
- QR payload is an `https://` URL, downloaded at runtime.
- Render **images and GIFs** at the QR's location.
- Track and render **multiple QR codes simultaneously** (one content instance per code).
- Loading and error visual states.

**Out of scope (deferred)**
- Website / non-image-and-GIF content rendering (the "unsupported → error" path is the seam for
  future website support).
- Spatial-anchor persistence / world-locking beyond MRUK's live tracking.
- Texture caching / LRU cache.

Adding any of these requires a new or updated ADR (see [conventions](docs/architecture.md#8-development-conventions)).

## Tech stack

- **Hardware:** Meta Quest 3 / 3S only.
- **Engine:** Unity 6 with the Universal Render Pipeline (URP).
- **XR:** Meta XR SDK (`com.meta.xr.sdk.all`, v203) + MRUK, on OpenXR.
- **Networking:** Unity web request modules (runtime `https` download).
- **GIF decoding:** [mgGif](https://github.com/gwaredd/mgGif) — *planned dependency, not yet added to
  the project* (see task M3-T1 in the plan).

## Getting started

> **On-device only.** All QR-detection testing must use **on-device builds**. Play-over-Link has a
> first-run-only QR detection bug — do not rely on it for QR testing.

**Prerequisites**
- Unity 6 with Android build support.
- A Meta Quest 3 / 3S in developer mode.
- The Meta XR SDK v203 packages (imported via the Unity Package Manager / `Packages/manifest.json`).

**High-level build & deploy**
1. Open the project in Unity 6.
2. Ensure the Android build target, URP, and OpenXR are configured (see device setup below).
3. Build an Android APK and deploy it to the headset (on-device — not Link).
4. Launch into passthrough and point the headset at a compliant QR code.

A detailed, authoritative build-and-deploy loop will live in `docs/` (task **M0-T7**, *planned* — not
written yet).

### Required device setup for QR tracking
- **Spatial Data** permission enabled on the device.
- **Camera Rig + Passthrough Layer** building blocks in the scene.
- **OVRManager:** Scene Support = *Required*, Anchor Support = *Enabled*.
- **MRUK → Tracker Configuration → "QR Code Tracking Enabled"**.

## Authoring a compliant QR code

The content of a QR code is a single `https://` URL pointing at an image or GIF. For reliable
detection, the printed code must be:

- QR **version ≤ 10**.
- **Not** a micro-QR code.
- **Not** logo'd / stylised / customised.
- Printed **reasonably large**, and viewed **close and well-lit**.

The URL is treated as **untrusted input**. Downloads are guarded by (all configurable):
**HTTPS-only**, a **10 s timeout**, and a **~25 MB download cap**.

## Architecture

The runtime is split into a detection source (MRUK Trackables), a per-QR lifecycle manager, a
content resolver (download + guards), a content-type classifier, media decoders (image / GIF), and
a renderer that draws the quad and the feedback states. The untrusted-input download path is kept
isolated behind the resolver. See [`docs/architecture.md`](docs/architecture.md) for the full
picture and component responsibilities.

## Documentation

| Doc | What it covers |
|-----|----------------|
| [Project context](docs/project-context.md) | Decisions, scope, requirements, constraints, rejected alternatives |
| [Architecture](docs/architecture.md) | How the system is organized to satisfy those decisions |
| [ADRs](docs/adr/) | [0001 — Meta XR Unity MCP Extension](docs/adr/0001-use-meta-xr-unity-mcp-extension.md) · [0002 — MRUK Trackables for QR](docs/adr/0002-detect-qr-with-mruk-trackables.md) · [0003 — QR payload is a runtime-downloaded URL](docs/adr/0003-qr-payload-is-runtime-downloaded-url.md) |
| [Implementation plan](docs/implementation-plan.md) | Milestones M0–M6, tasks, dependencies, risks |

**Read the docs before proposing changes.** Do not introduce a new technology or architectural
decision without a new ADR (or an update to an existing one) — see
[architecture §8](docs/architecture.md#8-development-conventions).

## Development tooling (Claude Code & MCP)

This project is built with AI-assisted tooling. These are **Editor / authoring-time** aids — none of
them ship on device.

- **[Claude Code](https://claude.com/claude-code)** — the AI development environment. Project
  conventions for AI (and human) contributors live in [`CLAUDE.md`](CLAUDE.md): decisions-before-code,
  the ADR rule, and the git workflow below.
- **Meta XR Unity MCP Extension** (`com.meta.xr.unity-mcp.extension`) — the preferred, authoritative
  tool for Meta-XR **Editor setup**: Camera Rig, passthrough, Android manifest / Spatial Data
  permission, MRUK trackable configuration, and Interaction SDK setup ([ADR 0001](docs/adr/0001-use-meta-xr-unity-mcp-extension.md)).
- **Base Unity MCP** (`com.coplaydev.unity-mcp`) — general Unity Editor automation over MCP
  (GameObjects, scripts, scenes, tests, builds).

## Contributing — git workflow (required)

Never commit or push directly to `main`. For **any** change:

1. Create a descriptive branch off `main` (`feat/…`, `fix/…`, `docs/…`, `chore/…`).
2. Commit with a clear message.
3. Push the branch and open a **pull request** describing what changed and why.
4. **Leave merging the PR to the repo owner** — do not merge, and do not push to `main`.

Group related work into one branch/PR; never force-push shared branches or `main`. See
[`CLAUDE.md`](CLAUDE.md) for the authoritative version.

## License

**TBD / internal.** No license has been chosen yet; this project is **not licensed for
distribution**.
