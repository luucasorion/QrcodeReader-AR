# QR Reader — On-Device Multi-QR Integration Test (M5-T6)

> The end-to-end acceptance procedure for the full pipeline: several QR codes — an **image**, an
> **animated GIF**, and a **failing URL** — tracked and rendered **simultaneously**, each running its
> own pipeline, staying aligned to its code's pose, and tearing down **independently** when its code
> is removed. Closes the **M5** milestone once the checklist passes. Derived from
> [`project-context.md`](project-context.md), [`architecture.md`](architecture.md) (§3.3, §3.7, §4,
> §7), ADR [0002](adr/0002-detect-qr-with-mruk-trackables.md) /
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md), and the
> [implementation plan](implementation-plan.md) (task **M5-T6**, risks **R1**/**R2**/**R3**).
> Introduces **no** new architectural decisions.

## ⚠️ On-device only

QR-detection testing **must** run from an on-device build. Play-over-Link has a first-run-only
detection bug and its results are not trustworthy (see
[build-and-deploy.md](build-and-deploy.md#-on-device-only--do-not-test-qr-detection-over-link)).
Everything below assumes an installed on-device build. Unlike the M1 detection check
([qr-detection-verification.md](qr-detection-verification.md)), the temporary debug HUD/logging is
gone (removed in M5-T5) — **verification here is visual**: you confirm what actually renders in
passthrough.

---

## 1. What this verifies

| # | Claim | How it shows up (in passthrough) |
|---|-------|----------------------------------|
| V1 | Each detected QR shows the **loading** state immediately | a loading spinner appears on each code the instant it's detected, before its download finishes |
| V2 | An **image** URL renders as a still quad | the image appears, letterboxed to its aspect, flush at the code's pose |
| V3 | A **GIF** URL renders and **animates** | the GIF plays its frames in place |
| V4 | A **failing** URL shows the **shared error icon** | the single error visual appears (same icon for every failure cause, §4) |
| V5 | All three render **simultaneously & independently** | image, GIF, and error are all visible at once; one code's outcome doesn't affect another's (§4) |
| V6 | Content stays **aligned** to each code's pose while tracked | as you move your head / the codes, each quad tracks its code's (low-frequency) pose (M5-T2, ADR 0002) |
| V7 | Removing one code **tears it down independently** | that code's content disappears; the others keep rendering unchanged (M5-T4) |
| V8 | No lingering content / no leak after removal | removed content does not reappear or leave a ghost; memory returns (profiler leak check is **M6-T3**) |

The components under test are the wired pipeline: `QrDetectionSource` (§3.2) →
`TrackableLifecycleManager` (§3.3) → `ContentResolver` (§3.4) → `ContentTypeClassifier` (§3.5) →
`MediaDecoder` (§3.6) → `ContentRenderer` (§3.7), one independent instance per QR.

---

## 2. Prerequisites

- An **on-device build** that includes all of **M5** (M5-T1…T5): the wired per-QR pipeline, pose
  follow, N-simultaneous support, teardown, and with the M1 debug HUD removed.
- The **`QR Pipeline` scene** ([`Assets/Scenes/QRReaderMR.unity`](../Assets/Scenes/QRReaderMR.unity))
  wired with, on one GameObject: `QrDetectionSource` and `TrackableLifecycleManager`, the manager's
  **content instance prefab** assigned, and its **`ContentResolverConfig`** assigned
  ([`Assets/QRReader/Runtime/Config/ContentResolverConfig.asset`](../Assets/QRReader/Runtime/Config/ContentResolverConfig.asset)).
- Device setup per ADR 0002: Spatial Data permission granted, passthrough on, MRUK **QR Code
  Tracking Enabled**.
- A network the headset can reach the test URLs on (HTTPS).

### Configuration in effect (the committed asset)

| Guard | Value | Notes |
|-------|-------|-------|
| HTTPS-only | **on** | non-`https://` payloads fail fast → error icon (ADR 0003) |
| Timeout | **10 s** | slow/unreachable host → error icon |
| Download cap | **25 MiB** | oversized body is aborted mid-stream → error icon (§7) |

(Finalizing/tuning this configuration surface is **M6-T1**; exercising every individual error path
on device is **M6-T2** — see [`error-path-verification.md`](error-path-verification.md). This test
only needs *one* failing code to prove the error path renders.)

---

## 3. The test QR set

Author **three** codes (compliant per ADR 0002: version ≤ 10, no micro-QR, no logo, printed large,
flat, well-lit). Suggested payloads — substitute hosts you control:

| Code | Payload (example) | Expected on device |
|------|-------------------|--------------------|
| **A — image** | `https://<host>/small.png` (a modest PNG/JPG) | still image quad, letterboxed (V2) |
| **B — GIF** | `https://<host>/anim.gif` (a small animated GIF) | animating quad (V3) |
| **C — failing** | any payload that fails, e.g. `http://<host>/x.png` (non-HTTPS), a 404, or an unsupported type (`text/html`, `.txt`) | shared error icon (V4) |

Keep A and B **well under the 25 MiB cap** so they succeed; the point of C is to fail. Print each on
its own sheet so they can be revealed/removed independently.

---

## 4. Procedure

1. **Launch** the on-device build; grant Spatial Data permission if prompted.
2. **Reveal all three** codes in view at once (or in quick succession).
   - Each should show a **loading spinner** immediately (**V1**).
   - Within a moment: **A** shows the image (**V2**), **B** animates (**V3**), **C** shows the error
     icon (**V4**) — all at the same time (**V5**).
3. **Move** your head around and, if practical, reposition the sheets slightly: each quad should stay
   **flush and aligned** to its code while tracked (**V6**). (Poses are low-frequency — move slowly;
   these are static codes by design, ADR 0002.)
4. **Remove codes one at a time** (cover or take away a sheet):
   - The removed code's content **disappears** and the **others keep rendering** unchanged (**V7**).
   - Re-reveal it: it re-runs its pipeline from loading — no stale/ghost content (**V8**).
5. Repeat removing **A**, then **B** (the GIF — the main memory risk), then **C**, in any order, to
   confirm each tears down on its own.

---

## 5. Pass / fail checklist

- [ ] **V1** — every detected code shows loading immediately.
- [ ] **V2** — image code renders a letterboxed still quad at its pose.
- [ ] **V3** — GIF code renders and animates in place.
- [ ] **V4** — failing code shows the shared error icon.
- [ ] **V5** — all three render simultaneously and independently.
- [ ] **V6** — content stays aligned to each code's pose while tracked.
- [ ] **V7** — removing one code tears down only that code; others unaffected.
- [ ] **V8** — no lingering/ghost content after removal; content rebuilds on re-detect.

**M5 is complete when V1–V8 all pass.** Profiler-based GIF memory/leak validation is **M6-T3**.

### Result log (fill in on device)

| Date | Build (branch / commit) | Device | V1 | V2 | V3 | V4 | V5 | V6 | V7 | V8 | Notes |
|------|-------------------------|--------|----|----|----|----|----|----|----|----|-------|
|      |                         |        |    |    |    |    |    |    |    |    |       |

---

## 6. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| Nothing appears on any code | Verify **on device**, not Link (top of doc). Confirm MRUK "QR Code Tracking Enabled" and Spatial Data permission. Improve lighting; move closer; flatten the print. |
| A code stays on the loading spinner forever | Host slow/unreachable → should hit the **10 s timeout** and switch to the error icon; confirm the headset's network can reach the URL. |
| The image/GIF code shows the **error** icon instead | Not `https://`, over the **25 MiB** cap, wrong/blocked content type, or a decode failure — every cause routes to the same error visual by design (§4). Check the URL over `adb logcat -s Unity` (the resolver/decoder log the distinct cause as a warning). |
| Content misaligned or laggy on movement | MRUK pose is **low-frequency** for **static** codes (ADR 0002) — hold codes still; expected behavior, not a bug. |
| Removed content doesn't disappear | Teardown (M5-T4) regression — capture `adb logcat` and re-check `TrackableLifecycleManager.OnQrCodeLost`. |
| `adb logcat` shows nothing | Use `adb logcat -s Unity`; confirm `adb devices` lists the headset. Note: normal operation is now **quiet** (the M1 debug logging was removed in M5-T5); only warnings/errors are logged. |

---

## 7. References

- [Implementation plan](implementation-plan.md) — task **M5-T6**, milestone **M5**.
- [Architecture](architecture.md) — §3.3 lifecycle, §3.7 renderer, §4 per-QR isolation, §7 memory.
- ADR [0002](adr/0002-detect-qr-with-mruk-trackables.md) — MRUK detection, low-frequency pose.
- ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md) — HTTPS-only, image/GIF only, classification.
- [M1 detection verification](qr-detection-verification.md) — the earlier (historical) M1 check.
