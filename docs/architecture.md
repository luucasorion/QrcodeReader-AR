# QR Reader (Quest 3 AR) — Architecture

> This document describes how the system is organized to satisfy the decisions recorded in
> [`project-context.md`](project-context.md) and the ADRs
> ([0001](adr/0001-use-meta-xr-unity-mcp-extension.md),
> [0002](adr/0002-detect-qr-with-mruk-trackables.md),
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md)).
> It introduces no new technologies or architectural decisions beyond those. Where it names a
> component boundary not spelled out verbatim in a decision, that boundary is an organizational
> choice for implementing the decisions — called out explicitly as such — not a new decision.

## 1. System Overview

An AR passthrough experience on Meta Quest 3 / 3S. The user sees the real world through
passthrough. When the headset detects a QR code, the app downloads the media referenced by the
QR's URL payload and renders that media (image or GIF) in place of the physical QR code, tracked
to its real-world pose.

The core flow, end to end:

1. **Detect** — Meta MRUK Trackables reports a QR code with a pose, physical size, and payload
   string (ADR 0002).
2. **Resolve** — the payload is an `https://` URL; the app downloads the bytes under safety guards
   and classifies the content type (ADR 0003).
3. **Render** — the app builds a flush, coplanar quad at the QR pose, sized from the QR's
   `PlaneRect` and scaled by a configurable factor, and displays the image or GIF on it
   (project context).
4. **Track & tear down** — while the trackable is tracked, content follows its pose; on
   `TrackableRemoved`, the content instance is destroyed and its textures freed (project context).

Multiple QR codes are tracked and rendered independently and simultaneously — one content instance
per QR.

Scope boundaries are as recorded: **in scope** = image + GIF content from a downloaded URL, loading
and error states, multi-QR. **Out of scope** = website/other content, spatial-anchor persistence
beyond MRUK live tracking, and texture caching.

```
 Real world              Meta XR / MRUK           App logic                     Rendering
┌──────────┐   passthrough  ┌───────────┐  event  ┌───────────────┐  media    ┌──────────────┐
│ QR code  │──────────────► │  MRUK     │───────► │ Detection →    │─────────► │ Content quad │
│ (printed)│                │ Trackable │  pose + │ Download →     │  texture  │ at QR pose   │
└──────────┘                └───────────┘  payload│ Classify →     │           └──────────────┘
                                                   │ (state mgmt)   │
                                                   └───────┬────────┘
                                                           │ https GET
                                                           ▼
                                                   ┌───────────────┐
                                                   │ Remote server │  (external)
                                                   │  image / GIF  │
                                                   └───────────────┘
```

## 2. Main Components

The runtime is organized into the following logical components. The detection source and the
external tooling are fixed by ADRs; the internal split between download, classification, rendering,
and per-QR lifecycle management is an **organizational choice** made to implement the decisions
cleanly and keep the untrusted-input path isolated.

| # | Component | Nature |
|---|-----------|--------|
| 1 | XR / Passthrough foundation | Scene + SDK config (Meta XR SDK v203, MRUK) |
| 2 | QR Detection source | MRUK Trackables (ADR 0002) |
| 3 | Trackable lifecycle manager | App logic (organizational) |
| 4 | Content resolver (download + safety guards) | App logic (ADR 0003) |
| 5 | Content-type classifier | App logic (ADR 0003) |
| 6 | Media decoders (image / GIF via mgGif) | App logic + mgGif library |
| 7 | Content renderer (quad + pose + feedback states) | App logic (project context) |
| 8 | Configuration | Serialized settings (project context) |
| 9 | Meta XR Unity MCP Extension | Editor-time tooling (ADR 0001) |

## 3. Responsibilities of Each Component

### 3.1 XR / Passthrough foundation
- Provides the passthrough MR scene: Camera Rig + Passthrough Layer building blocks.
- Holds the device/SDK configuration that QR tracking depends on: Spatial Data permission,
  OVRManager Scene Support = Required and Anchor Support = Enabled, and MRUK Tracker Configuration
  with "QR Code Tracking Enabled".
- Stack: Unity 6 + URP, Meta XR SDK (v203) + MRUK, OpenXR (per project manifest).

### 3.2 QR Detection source (MRUK Trackables)
- Subscribes to MRUK's `TrackableAdded` and filters `TrackableType == QRCode`.
- Surfaces, per QR: `MarkerPayloadString` (the URL), the trackable `Transform` (pose), `PlaneRect`
  (physical size), and `IsTracked`.
- Emits `TrackableRemoved` when a QR leaves tracking.
- Does **not** decode camera pixels — pose, payload, and lifecycle come first-party from MRUK
  (ADR 0002). Pose updates arrive at low frequency, so this component targets static QR codes.

### 3.3 Trackable lifecycle manager *(organizational)*
- Owns the mapping from each tracked QR to its content instance (one content instance per QR).
- On QR added: starts the resolve → render pipeline for that QR and shows the loading state.
- While tracked: keeps the content instance aligned to the trackable's current pose.
- On `TrackableRemoved`: destroys the content instance and frees its textures. No caching, no
  lingering content (project context).

### 3.4 Content resolver — download + safety guards *(ADR 0003)*
- Treats the payload URL as **untrusted input**.
- Enforces the safety guards, all configurable: **HTTPS-only**, **10 s timeout**,
  **~25 MB download cap**.
- Performs the runtime `https` GET (via Unity's web request modules) and returns raw bytes plus the
  response headers needed for classification.
- On any violation or failure (non-HTTPS, timeout, over-cap, network error) it reports failure so
  the renderer can show the error state.

### 3.5 Content-type classifier *(ADR 0003)*
- Decides whether downloaded bytes are an image, a GIF, or unsupported.
- **Extension hint first**, `Content-Type` **header fallback** — both, in that order.
- Routes: image → texture path; GIF → mgGif decode path; anything else → error state.
- The "unsupported type → error state" path is the documented seam for future website support.

### 3.6 Media decoders
- **Image:** turns downloaded bytes into a Unity texture.
- **GIF:** decodes bytes into per-frame textures using the **mgGif** library (chosen for
  performance and license over UniGif and Unity-GifDecoder).
- GIF per-frame textures are the primary memory risk on Quest; decoded textures are owned by the
  content instance so they can be freed on teardown.

### 3.7 Content renderer *(project context)*
- Builds a **flush / coplanar quad** at the QR pose.
- Sizes the quad from the QR's `PlaneRect`, scaled up by a **configurable factor**; non-square
  content is **letterboxed**.
- Renders the feedback states: **loading spinner** on detect, **error icon** on
  failure / timeout / unsupported type.
- Swaps in the resolved image or GIF once ready.

### 3.8 Configuration
- Central place for the configurable values called out in the decisions: download size cap, timeout,
  HTTPS-only flag, and the render scale factor. Kept configurable per the project context.

### 3.9 Meta XR Unity MCP Extension *(ADR 0001, Editor-time only)*
- The preferred, authoritative tool for Meta-XR **Editor** setup: Camera Rig, passthrough,
  Android manifest / Spatial Data permission, MRUK trackable configuration, and Interaction SDK
  setup.
- Not part of the shipped runtime — it configures the project (§3.1) rather than running on device.

## 4. How Components Communicate

- **MRUK → app (events):** detection is event-driven. MRUK raises `TrackableAdded` /
  `TrackableRemoved`; the trackable lifecycle manager (§3.3) subscribes and reacts. The manager
  reads pose/size/payload/`IsTracked` from the trackable each frame it needs them.
- **Within the app (pipeline):** for each QR, the lifecycle manager drives a sequential pipeline —
  resolver (download) → classifier → decoder → renderer. Each stage hands its output to the next;
  any stage can short-circuit to the renderer's **error state**.
- **App → renderer (state):** the renderer is told which visual state to show — loading, error, or
  ready-with-media — plus the pose and size to draw at.
- **Per-QR isolation:** because there is one content instance per QR, each QR runs its own pipeline
  instance; failures or teardown of one QR do not affect the others.
- **Tooling (out of band):** the Meta XR Unity MCP Extension communicates with the Unity Editor at
  authoring time only, over MCP; it has no runtime channel to the components above.

## 5. External Dependencies

**Runtime / on-device (from `Packages/manifest.json` and the decisions):**
- **Meta XR SDK — `com.meta.xr.sdk.all` v203** with **MRUK** — QR detection, passthrough, camera
  rig, anchors/trackables (ADR 0002).
- **Unity 6 + Universal Render Pipeline (`com.unity.render-pipelines.universal`)** — rendering
  (project context).
- **OpenXR (`com.unity.xr.openxr`)** and XR management — XR runtime plumbing (project manifest).
- **Unity web request modules (`com.unity.modules.unitywebrequest*`)** — runtime `https` download
  (ADR 0003).
- **mgGif** — runtime GIF decoding (`byte[]` → textures) (ADR 0003 / project context).
- **Remote content servers** — arbitrary `https://` hosts referenced by QR payloads; **untrusted**,
  outside our control. This is why the safety guards exist (§3.4).

**Editor / tooling (not shipped on device):**
- **Meta XR Unity MCP Extension — `com.meta.xr.unity-mcp.extension`** (ADR 0001).
- **Base Unity MCP — `com.coplaydev.unity-mcp`** (project manifest).

## 6. Data Flow

Per detected QR code:

1. **Detect.** MRUK raises `TrackableAdded`; the manager filters `TrackableType == QRCode` and
   reads `MarkerPayloadString` (URL), `Transform` (pose), and `PlaneRect` (size).
2. **Show loading.** The renderer immediately displays the loading spinner at the QR pose.
3. **Validate + download.** The resolver checks HTTPS-only, then GETs the URL under the 10 s timeout
   and ~25 MB cap. On violation/failure → **error state**, stop.
4. **Classify.** Extension hint first, then `Content-Type` header. Unsupported → **error state**,
   stop.
5. **Decode.** Image → texture; GIF → mgGif → per-frame textures.
6. **Render.** The renderer draws the flush quad at the QR pose, sized from `PlaneRect × scale
   factor`, letterboxing non-square content, and shows the media.
7. **Track.** While `IsTracked`, the content instance follows the trackable's (low-frequency) pose
   updates.
8. **Teardown.** On `TrackableRemoved`, destroy the content instance and free its textures.

Media flows **one way**: remote server → resolver bytes → decoded textures → quad. Nothing is
persisted; textures live only as long as their QR is tracked (no caching — deferred).

## 7. Important Technical Constraints

- **Target hardware:** Quest 3 / 3S only.
- **Memory:** Quest RAM is shared and limited; GIF per-frame textures are the main memory risk.
  Content and its textures are freed on `TrackableRemoved`; caching is deferred (not built now).
- **Untrusted input:** QR payloads are untrusted. Guards are mandatory: **HTTPS-only**, **10 s
  timeout**, **~25 MB cap** (all configurable).
- **Detection limits (MRUK):** pose updates are **low-frequency** — designed for static QR codes,
  not codes on objects moving in real time.
- **QR authoring limits:** version ≤ 10; no micro-QR; no logo'd / customized codes; printed
  reasonably large; viewed close and well-lit.
- **Required device setup for tracking:** Spatial Data permission enabled; Camera Rig + Passthrough
  Layer in the scene; OVRManager Scene Support = Required, Anchor Support = Enabled; MRUK Tracker
  Configuration → "QR Code Tracking Enabled".
- **Testing:** must use **on-device builds**. Play-over-Link has a first-run-only detection bug — do
  not rely on Link for QR testing.

## 8. Development Conventions

- **Decisions before code.** The ADRs record *what* was chosen; this document records *how* the
  system is organized. Do not introduce a new technology or architectural decision without a new ADR
  (or an update to an existing one) explaining it — per the project's own instruction.
- **Meta-XR Editor setup goes through the Meta XR Unity MCP Extension** (ADR 0001): rig, passthrough,
  Android manifest / permissions, MRUK config, Interaction SDK. Prefer it over hand-configuration
  for Meta-specific tasks.
- **First-party first.** Detection uses MRUK Trackables, not camera-pixel decoding (ADR 0002). Do
  not reintroduce Passthrough Camera API + third-party decoder or OpenCV — those were rejected.
- **Keep the untrusted-input path isolated.** All network access lives behind the content resolver
  and its guards (§3.4); nothing else should perform arbitrary `https` GETs from QR payloads.
- **Configurable values stay configurable.** Download cap, timeout, HTTPS-only, and render scale
  factor are exposed as configuration, not hard-coded (project context).
- **One content instance per QR**, created on detect and destroyed on removal — no lingering
  content, no cross-QR shared state.
- **Free textures on teardown.** Because GIFs are the main memory risk, every decoded texture must
  be owned by its content instance and released on `TrackableRemoved`.
- **Fail to the error state, never silently.** Any failure (non-HTTPS, timeout, over-cap, network
  error, unsupported type) surfaces the shared error visual; unsupported-type reuses the failure
  visual and marks the future website-support seam.
- **Verify on device.** Treat on-device build behavior as the source of truth for detection; don't
  conclude from Link runs.

## 9. Traceability

| Architectural element | Source decision |
|-----------------------|-----------------|
| MRUK Trackables detection, event model, pose/size/payload | ADR 0002; project context |
| URL payload, runtime download, HTTPS-only, timeout, cap, classification, mgGif | ADR 0003; project context |
| Meta XR Unity MCP Extension for Editor setup | ADR 0001 |
| Flush quad, `PlaneRect` sizing, scale factor, letterboxing, loading/error states | project context |
| One-instance-per-QR, destroy-on-removal, no caching | project context |
| Stack (Quest 3/3S, Unity 6 + URP, Meta XR SDK v203 + MRUK, OpenXR) | project context; manifest |
| Component split (lifecycle/resolver/classifier/decoder/renderer) | organizational, to implement the above |
