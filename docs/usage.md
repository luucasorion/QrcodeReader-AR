# QR Reader — Usage Guide

> **M6-T5.** The end-to-end guide to *using* the app: author a compliant QR code, understand the
> configurable values, build and run on a Quest 3 / 3S, and verify it on device. This is the hub —
> each step links the authoritative doc rather than duplicating it. New to the project? Start with
> the [README](../README.md) and [project context](project-context.md); come here to actually run it.

## 1. Author a compliant QR code

A QR code's payload is a **single `https://` URL** pointing at an **image or GIF**. The app downloads
that URL at runtime, classifies it, and renders it in place of the code.

**Payload (content) rules**

- Must be `https://` — plain `http://` is rejected by the HTTPS-only guard (see §2).
- Must point at a **PNG / JPEG image** or an **animated GIF**. Anything else (a web page, PDF, plain
  text) shows the shared **error icon** — that's the deliberate
  [website-support seam](website-support-seam.md), not a bug.
- Keep the file comfortably under the **download cap** (default 25 MiB, §2).
- Classification is by **URL extension first** (`.png` / `.jpg` / `.jpeg` / `.gif`), then the
  server's `Content-Type` header — so a correct extension is the most reliable signal.

**Print (detection) rules** — per ADR [0002](adr/0002-detect-qr-with-mruk-trackables.md):

- QR **version ≤ 10**, and **not** a micro-QR code.
- **Not** logo'd / stylised / colour-customised.
- Printed **reasonably large**, kept **flat**, and viewed **close and well-lit**.

## 2. Configuration values

Four values are authored in the Editor (no code change needed). Full detail — assets, fields, min
clamps, and wiring — is in the [configuration reference](configuration.md); the defaults:

| Value | Default | What it does |
|-------|---------|--------------|
| HTTPS-only | on | rejects non-`https://` payloads before any request |
| Timeout | 10 s | aborts a slow download → error icon |
| Download cap | 25 MiB | aborts an oversized download → error icon |
| Render scale factor | 3× | content quad size = QR `PlaneRect` × this factor |

The guards live on `ContentResolverConfig.asset`; the scale factor on `ContentRendererConfig.asset`
(both under `Assets/QRReader/Runtime/Config/`).

## 3. Build and run on device

> **On-device only.** QR detection must run from an installed on-device build — Play-over-Link has a
> first-run-only detection bug. See the deploy doc's
> [warning](build-and-deploy.md#-on-device-only--do-not-test-qr-detection-over-link).

Full build-and-deploy loop: [build-and-deploy.md](build-and-deploy.md). In short:

1. Open the project in **Unity 6**; confirm the Android target, URP, and OpenXR are set.
2. Build the Android APK (Editor or the Unity CLI path) and `adb install -r` it to the headset.
3. Ensure device setup for QR tracking: **Spatial Data** permission, Camera Rig + Passthrough Layer,
   OVRManager Scene/Anchor support, and MRUK **"QR Code Tracking Enabled"**.
4. Launch into passthrough and point the headset at a compliant QR code (§1).

Expected: a **loading spinner** on detect, then the image/GIF rendered flush at the code's pose; the
content tracks the code and tears down when the code leaves view.

## 4. Verify on device

Three procedures cover the full acceptance surface; run whichever you need:

| Procedure | Verifies |
|-----------|----------|
| [Multi-QR integration test](qr-integration-test.md) | the happy path end-to-end: image + GIF + a failing code tracked, rendered, and torn down **simultaneously and independently** (the M5 acceptance test) |
| [Error-path verification](error-path-verification.md) | each failure cause — non-HTTPS, timeout, over-cap, network error, unsupported type — renders the shared error icon (M6-T2) |
| [GIF memory / leak validation](memory-validation.md) | a tracked GIF's textures are freed on removal and repeated cycles don't leak, via the profiler (M6-T3) |

Each has a result log to fill in on device. If something doesn't render, start with that doc's
troubleshooting table and `adb logcat -s Unity` (normal operation is quiet — only failures warn).

## 5. References

- [README](../README.md) — what the app is, features, tech stack.
- [Project context](project-context.md) · [Architecture](architecture.md) — decisions and how the system is built.
- [Configuration reference](configuration.md) — the four values in detail (M6-T1).
- [Build & deploy](build-and-deploy.md) — the on-device build/iteration loop.
- Verification: [integration test](qr-integration-test.md) · [error paths](error-path-verification.md) · [memory](memory-validation.md).
- ADRs: [0002 — MRUK Trackables](adr/0002-detect-qr-with-mruk-trackables.md) · [0003 — runtime-downloaded URL](adr/0003-qr-payload-is-runtime-downloaded-url.md).
