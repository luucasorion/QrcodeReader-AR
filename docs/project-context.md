# QR Reader (Quest 3 AR) — Project Context

> This document captures the decisions and conclusions reached during a planning ("Grill Me") session. It is intended as context for future AI coding sessions. It contains only what was decided — no architecture or implementation plans beyond the decisions listed.

## Project Overview
An AR passthrough experience on Meta Quest 3. The user sees the real world through passthrough; when the headset detects a QR code, the app renders content in place of the QR code. For the MVP, that content is images and GIFs, sourced from a URL encoded in the QR code.

## Goals
- Detect QR codes through Quest passthrough.
- Render content in the place of the detected QR code.
- Support **images** and **GIFs** as content in the MVP.

## Scope
- QR detection via passthrough.
- QR payload is an `https://` URL, downloaded at runtime.
- Render images and GIFs at the QR code's location.
- Track and render **multiple QR codes simultaneously** (one content instance per QR).
- Provide loading and error visual states.

## Out of Scope (Deferred)
- Website / non-image-and-GIF content rendering.
- Spatial-anchor persistence (world-locking beyond MRUK's live tracking).
- Texture caching / LRU cache (deferred — "think about cache later").

## Requirements
- Target hardware: Quest 3 / 3S only.
- Content source: QR code encodes a URL; content is downloaded at runtime.
- Content type classification: extension hint first, `Content-Type` header fallback.
- Networking guards: **HTTPS-only**, **10 s timeout**, **~25 MB download cap** (all configurable).
- Rendering: flush/coplanar quad at the QR pose, scaled up by a configurable factor, non-square content letterboxed, sized from the QR's `PlaneRect`.
- Feedback: loading spinner on detect; error icon on failure, timeout, or unsupported type.
- Lifecycle: destroy the content instance and free textures on `TrackableRemoved`.

### Device setup required for QR tracking
- Spatial Data permission enabled on the device.
- Camera Rig + Passthrough Layer building blocks in the scene.
- OVRManager: Scene Support = Required, Anchor Support = Enabled.
- MRUK → Tracker Configuration → "QR Code Tracking Enabled".

### QR content authoring constraints
- QR version ≤ 10.
- No micro-QR codes.
- No logo'd / customized codes.
- Printed reasonably large; viewed close and well-lit.

## Constraints
- Quest RAM is shared and limited; GIFs (per-frame textures) are the main memory risk.
- QR payloads are untrusted input and must be treated as such.
- Testing must use **on-device builds**, not Play-over-Link (Link has a first-run-only detection bug).

## Technical Decisions
- **Detection:** Meta MRUK Trackables (first-party). Subscribe to `TrackableAdded`, filter `TrackableType == QRCode`, read `MarkerPayloadString`.
- **Content source model:** QR encodes a URL, downloaded at runtime.
- **Type classification:** extension hint first, `Content-Type` header fallback.
- **GIF decoding:** mgGif library (runtime, `byte[]` → textures).
- **Rendering:** flush/coplanar quad at the QR pose; scaled up by a configurable factor; non-square content letterboxed; sized from `PlaneRect`.
- **Multiplicity:** multiple QR codes rendered independently and simultaneously.
- **Feedback:** loading spinner on detect; error icon on failure / timeout / unsupported type.
- **Unsupported type handling:** show the error state (reuse the failure visual); marks the future website-support seam.
- **Lifecycle:** destroy the content instance and free textures on `TrackableRemoved`; no caching.
- **Safety guards:** HTTPS-only + configurable download size cap.
- **Stack:** Quest 3 / 3S, passthrough MR, Unity 6 + URP, Meta XR SDK (v203) + MRUK.

## Rejected Alternatives
- **Passthrough Camera API + ZXing.Net + manual pose math** — rejected in favor of MRUK Trackables.
- **OpenCV for Unity** (QR detection) — rejected (overkill / paid).
- **Bundled ID → asset** content model, and **hybrid** content model — rejected in favor of URL download.
- **Classification by extension only**, and **by Content-Type only** — rejected in favor of both (extension hint + Content-Type fallback).
- **GIF decoders UniGif and Unity-GifDecoder** — rejected (UniGif: slower + Unity-Chan license restrictions).
- **Anchoring alternatives** — view-only live-tracked without MRUK, world-locked Spatial Anchor building block, world-locked + re-detection — superseded by MRUK's built-in tracked trackable.
- **Billboard orientation**, and **exact-QR-size** rendering — rejected in favor of flush + scaled-up.
- **Single QR at a time** — rejected in favor of many.
- **Persist / linger content after removal** — rejected in favor of destroy on removal.
- **Bounded LRU cache now** — deferred (not rejected).
- **Download feedback "nothing until ready"** — rejected in favor of loading + error states.
- **Silent ignore of unsupported types** — rejected in favor of error state.

## Open Questions
- _(Resolved 2026-07-20)_ ~~Verify that `OVRAnchor.TrackableType.QRCode` exists in the installed SDK (v203).~~ Confirmed via Unity reflection: `OVRAnchor.TrackableType` (assembly `Oculus.VR`) has values `None, Keyboard, QRCode`, and `Meta.XR.MRUtilityKit.MRUKTrackable` exposes `TrackableType`, `MarkerPayloadString`, `MarkerPayloadBytes`, and `IsTracked`.
