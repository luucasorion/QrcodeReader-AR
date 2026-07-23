# QR Reader (Quest 3 AR) — Implementation Plan

> Derived from [`project-context.md`](project-context.md), [`architecture.md`](architecture.md), and
> ADRs [0001](adr/0001-use-meta-xr-unity-mcp-extension.md),
> [0002](adr/0002-detect-qr-with-mruk-trackables.md),
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md).
> Introduces **no** new architectural decisions. Component boundaries follow architecture.md §2.
> Tasks are sized to become single GitHub Issues / Jira tickets.

## How to read this

- **Milestones (M0–M6)** are shippable/verifiable increments, ordered by dependency.
- **Features** group tasks within a milestone and map to architecture.md components.
- **Tasks** are prefixed with the milestone (e.g. `M2-T3`) and are individually estimatable.
- **Dependencies** are listed per task by ID. `→` means "must complete before".
- The critical path runs M0 → M1 → (M2 ∥ M4) → M3 → M5 → M6.

---

## Milestone overview

| ID | Milestone | Goal / exit criteria | Depends on |
|----|-----------|----------------------|-----------|
| M0 | Foundation & device setup | Passthrough app builds and runs on Quest 3; QR tracking prerequisites configured; repo conventions in place | — |
| M1 | QR detection + lifecycle skeleton | MRUK reports QR codes; app logs payload/pose/size per QR and tracks add/remove | M0 |
| M2 | Content resolver + classifier | Untrusted URL downloaded under guards; classified image / GIF / unsupported | M1 (event wiring) |
| M3 | Media decoders | Bytes → image texture; bytes → GIF frame textures (mgGif) | M2 |
| M4 | Renderer + feedback states | Flush quad at QR pose, sized/letterboxed; loading + error visuals | M1 |
| M5 | Multi-QR integration & teardown | Full pipeline per QR, N simultaneous, textures freed on removal | M2, M3, M4 |
| M6 | Hardening & on-device verification | Config surface complete, error paths verified on device, memory validated | M5 |

Configuration (architecture.md §3.8) is cross-cutting — introduced in M2 as a stub and completed in M6.

---

## M0 — Foundation & device setup

**Features:** XR/Passthrough foundation (§3.1), Meta XR MCP tooling (§3.9), repo conventions (§8).

| Task | Description | Depends on |
|------|-------------|-----------|
| M0-T1 | Create runtime + editor assembly definitions (`QRReader.Runtime`, `QRReader.Editor`) and a `Scripts/` folder structure mirroring the component split | — |
| M0-T2 | Set up passthrough MR scene: Camera Rig + Passthrough Layer building blocks (via Meta XR MCP Extension per ADR 0001) | M0-T1 |
| M0-T3 | Configure OVRManager: Scene Support = Required, Anchor Support = Enabled | M0-T2 |
| M0-T4 | Configure MRUK → Tracker Configuration → "QR Code Tracking Enabled" | M0-T2 |
| M0-T5 | Android manifest / Spatial Data permission configuration (via Meta XR MCP Extension) | M0-T2 |
| M0-T6 | Verify Android build settings (Quest 3/3S target, URP, OpenXR) and produce a first on-device build that launches into passthrough | M0-T3, M0-T4, M0-T5 |
| M0-T7 | Document the build + on-device deploy loop in `docs/` (Link is not reliable for QR — on-device only) | M0-T6 |

**Exit:** an empty passthrough app runs on-device with all QR-tracking prerequisites enabled.

---

## M1 — QR detection + lifecycle skeleton

**Features:** QR Detection source (§3.2), Trackable lifecycle manager skeleton (§3.3).

| Task | Description | Depends on |
|------|-------------|-----------|
| M1-T1 | `QrDetectionSource`: subscribe to MRUK `TrackableAdded`/`TrackableRemoved`, filter `TrackableType == QRCode`, expose a C# event surfacing `MarkerPayloadString`, `Transform`, `PlaneRect`, `IsTracked` | M0-T1, M0-T4 |
| M1-T2 | `TrackableLifecycleManager` skeleton: maintain a `QR → tracked-entry` dictionary; create on add, remove on `TrackableRemoved` | M1-T1 |
| M1-T3 | Debug HUD / logging: render payload + pose + size per detected QR (temporary, no content yet) | M1-T2 |
| M1-T4 | On-device verification: print/prepare a valid QR (version ≤ 10, no micro/logo, large, well-lit) and confirm add/remove events fire | M1-T3, M0-T6 |

**Exit:** detecting a printed QR on-device logs its URL, pose, and size; removing it fires teardown.

---

## M2 — Content resolver + content-type classifier

**Features:** Content resolver + safety guards (§3.4), Content-type classifier (§3.5), Configuration stub (§3.8).

| Task | Description | Depends on |
|------|-------------|-----------|
| M2-T1 | `ContentResolverConfig` (ScriptableObject or serialized settings): HTTPS-only flag, timeout (default 10 s), download cap (default ~25 MB) | M0-T1 |
| M2-T2 | `ContentResolver`: HTTPS-only pre-check; reject non-`https://` before any request | M2-T1 |
| M2-T3 | `ContentResolver`: perform `https` GET via UnityWebRequest with the configured timeout | M2-T2 |
| M2-T4 | `ContentResolver`: enforce download-size cap (fail if `Content-Length` or streamed bytes exceed cap) | M2-T3 |
| M2-T5 | `ContentResolver`: return a typed result (`bytes + headers` on success, or a failure reason) — never throw into the pipeline | M2-T4 |
| M2-T6 | `ContentTypeClassifier`: extension hint first, then `Content-Type` header fallback → `Image` / `Gif` / `Unsupported` | M2-T5 |
| M2-T7 | Unit tests for classifier (extension vs header precedence) and resolver guard logic (non-HTTPS, over-cap, timeout paths) | M2-T5, M2-T6 |

**Exit:** given a URL, the resolver returns bytes or a failure reason under all guards; classifier routes correctly. Isolated from rendering (architecture.md §8 "keep the untrusted-input path isolated").

> **Testing & CI.** The M2-T7 tests live in the `QRReader.Tests.EditMode` assembly
> (`Assets/Tests/EditMode/`) and run headless in CI on every PR via the **Unity CLI**
> (`unity test --mode EditMode`) on a **self-hosted runner** — EditMode only, per
> [ADR 0004](adr/0004-adopt-unity-cli-for-build-and-ci.md). Detection, rendering, and memory stay
> on-device (M5-T6, M6). Keep tested logic decoupled from `MonoBehaviour`/MRUK so it stays
> EditMode-runnable (plan R1).

---

## M3 — Media decoders

**Features:** Media decoders — image + GIF (§3.6).

| Task | Description | Depends on |
|------|-------------|-----------|
| M3-T1 | Add the **mgGif** library to the project and record it as a dependency | M0-T1 |
| M3-T2 | `ImageDecoder`: `byte[]` → Unity `Texture2D`; expose the texture for ownership/teardown by the caller | M2-T6 |
| M3-T3 | `GifDecoder`: `byte[]` → per-frame textures + frame delays via mgGif | M3-T1, M2-T6 |
| M3-T4 | Decoded-content handle that owns its texture(s) so they can be released on teardown (feeds M5-T4) | M3-T2, M3-T3 |
| M3-T5 | Decode-failure path: malformed image/GIF bytes → failure reason (routes to error state, not a crash) | M3-T2, M3-T3 |

**Exit:** valid image/GIF bytes decode to textures owned by a releasable handle; malformed bytes fail cleanly.

---

## M4 — Renderer + feedback states

**Features:** Content renderer + feedback states (§3.7). Can proceed in parallel with M2/M3 (depends only on M1 pose data).

| Task | Description | Depends on |
|------|-------------|-----------|
| M4-T1 | Content quad prefab + material (URP, unlit/transparent as appropriate); flush/coplanar orientation at a given pose | M0-T1 |
| M4-T2 | `ContentRenderer`: place/size the quad from `PlaneRect × configurable scale factor`; add scale factor to config | M4-T1, M2-T1 |
| M4-T3 | Letterboxing for non-square content (fit texture aspect within the scaled quad) | M4-T2 |
| M4-T4 | Loading state visual (spinner) shown on detect | M4-T1 |
| M4-T5 | Error state visual (error icon) — shared visual reused for failure / timeout / unsupported type | M4-T1 |
| M4-T6 | State API: `ShowLoading` / `ShowError` / `ShowMedia(texture[])` driven by the lifecycle manager; GIF frame playback (advance frames by delay) | M4-T3, M4-T4, M4-T5 |

**Exit:** renderer can show loading, error, a static image, and an animated GIF on a correctly posed/sized quad (driven by stub data).

---

## M5 — Multi-QR integration & teardown

**Features:** Trackable lifecycle manager full pipeline (§3.3), per-QR isolation & memory (§7).

| Task | Description | Depends on |
|------|-------------|-----------|
| M5-T1 | Wire the per-QR pipeline in the lifecycle manager: on add → loading → resolve → classify → decode → render; any stage short-circuits to error state | M2-T6, M3-T4, M4-T6 |
| M5-T2 | Keep each content instance aligned to its trackable's (low-frequency) pose each update while `IsTracked` | M5-T1, M1-T2 |
| M5-T3 | Support N simultaneous QRs — one independent pipeline + content instance per QR; no cross-QR shared state | M5-T1 |
| M5-T4 | Teardown on `TrackableRemoved`: destroy the content instance and release all owned textures (esp. GIF frames) | M5-T1, M3-T4 |
| M5-T5 | Remove the M1 debug HUD / temporary logging | M5-T1 |
| M5-T6 | On-device integration test: multiple QRs (image + GIF + a failing URL) tracked and rendered simultaneously; each tears down independently | M5-T2, M5-T3, M5-T4 |

**Exit:** end-to-end flow works on-device for multiple simultaneous QRs; content and textures are freed on removal.

---

## M6 — Hardening & on-device verification

**Features:** Configuration completion (§3.8), constraints validation (§7), conventions (§8).

| Task | Description | Depends on |
|------|-------------|-----------|
| M6-T1 | Finalize the configuration surface: HTTPS-only, timeout, download cap, render scale factor — all editor-exposed and documented | M5-T1 |
| M6-T2 | Verify each error path on-device: non-HTTPS URL, timeout, over-cap download, network error, unsupported content type → error visual | M5-T6 |
| M6-T3 | Memory validation: track a large GIF, remove the QR, confirm textures are freed (profiler); repeat to check for leaks | M5-T4 |
| M6-T4 | Confirm the "unsupported type → error state" seam is clean and documented as the future website-support hook | M6-T2 |
| M6-T5 | README / usage doc: how to author a compliant QR, the config values, and the on-device test procedure | M6-T1 |

**Exit:** all guards and error paths verified on-device; no texture leaks across track/remove cycles; config and usage documented.

---

## Dependency graph (critical path)

```
M0 ──► M1 ──┬──────────────► M4 ──┐
            │                     │
            └──► M2 ──► M3 ───────┴──► M5 ──► M6
```

- **M4 runs in parallel** with M2+M3 (renderer needs only M1 pose data + config stub from M2-T1).
- **M3 depends on M2** for the classifier routing contract, but decoder work (M3-T1/T2/T3) can start against sample bytes before M2 is fully wired.
- **M5 is the integration join** — it needs the resolver/classifier (M2), decoders (M3), and renderer (M4).

---

## Risks & unknowns

| # | Risk / unknown | Impact | Mitigation |
|---|----------------|--------|-----------|
| R1 | **On-device-only testing loop.** Link has a first-run-only QR detection bug; iteration requires device builds. | Slower iteration on M1/M5/M6. | Establish a fast build+deploy loop early (M0-T7); keep decoder/classifier/renderer unit-testable off-device (M2-T7, M4 stub data). |
| R2 | **MRUK pose is low-frequency.** Only supports static QRs; jitter/lag possible. | Content may lag or appear misaligned on moving codes. | Scope to static QRs (already decided); verify alignment quality on-device (M5-T2). |
| R3 | **GIF per-frame texture memory.** Main memory risk on shared Quest RAM. | OOM / thermal issues with large/multiple GIFs. | Enforce download cap (M2-T4); own+free textures per instance (M3-T4, M5-T4); profiler validation (M6-T3). |
| R4 | **mgGif integration unknowns.** API surface, threading, frame-timing accuracy on device not yet validated. | M3 slippage. | Spike M3-T1/T3 against sample GIFs early; decode failure path (M3-T5) prevents crashes. |
| R5 | **Meta XR MCP Extension beta gating.** Some features need Unity Assistant + AI Gateway (beta), not confirmed here (ADR 0001). | Some M0 setup tasks may need manual fallback. | Confirm available MCP capabilities in M0; fall back to documented manual Editor config where gated. |
| R6 | **QR authoring constraints** (version ≤ 10, no micro/logo, large, well-lit). | Detection failures mistaken for code bugs. | Standardize a known-good test QR set (M1-T4); document authoring rules (M6-T5). |
| R7 | **UnityWebRequest size-cap enforcement.** `Content-Length` may be absent; must cap streamed bytes. | Cap could be bypassed by a chunked response. | Enforce cap on the streamed download handler, not just the header (M2-T4). |
| R8 | **Untrusted-input isolation regressions.** Future changes could add ad-hoc `https` GETs outside the resolver. | Security guard bypass. | Keep all network access behind `ContentResolver` (architecture.md §8); review in code review. |

---

## Explicitly out of scope (deferred — do **not** build now)

- Website / non-image-and-GIF content rendering (the classifier's "unsupported → error" is the seam).
- Spatial-anchor persistence / world-locking beyond MRUK live tracking.
- Texture caching / LRU cache.

Any of these would require a new or updated ADR before implementation (architecture.md §8).
