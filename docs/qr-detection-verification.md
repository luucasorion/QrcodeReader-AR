# QR Reader — On-Device Detection Verification (M1-T4)

> The procedure to confirm, **on device**, that MRUK reports QR codes and that the
> `TrackableAdded` / `TrackableRemoved` lifecycle fires with the correct payload, pose, and
> size. Closes the M1 milestone once the checklist passes. Derived from
> [`project-context.md`](project-context.md), [`architecture.md`](architecture.md), ADR
> [0002](adr/0002-detect-qr-with-mruk-trackables.md), and the
> [implementation plan](implementation-plan.md) (task **M1-T4**, risks **R1**/**R6**).
> Introduces **no** new architectural decisions.

## ⚠️ On-device only

QR-detection testing **must** run from an on-device build. Play-over-Link has a
first-run-only detection bug and its results are not trustworthy (see
[build-and-deploy.md](build-and-deploy.md#-on-device-only--do-not-test-qr-detection-over-link)).
Everything below assumes an installed on-device build.

---

## 1. What this verifies

| # | Claim | How it shows up |
|---|-------|-----------------|
| V1 | A compliant QR is **detected** (`TrackableAdded`, `TrackableType == QRCode`) | a `[QR] detected` log line appears |
| V2 | The **payload** string is read correctly | `payload='…'` matches the encoded URL |
| V3 | The **pose** is reported and plausible | `pos=` / `rotEuler=` track the physical code's location/orientation |
| V4 | The **physical size** is reported | `size=` is close to the printed code's real width/height |
| V5 | Removing the QR fires **teardown** (`TrackableRemoved`) | a `[QR] lost` log line appears |

The detection source and lifecycle skeleton under test are `QrDetectionSource`
(architecture.md §3.2) and `TrackableLifecycleManager` (§3.3); the diagnostics that surface
the above are the temporary `QrDebugOverlay` (M1-T3). There is **no content pipeline** at this
stage — nothing is downloaded or rendered; the payload URL only needs to be *readable*.

---

## 2. Prerequisites

- A build that includes **M1-T1** (`QrDetectionSource`), **M1-T2** (`TrackableLifecycleManager`),
  and **M1-T3** (`QrDebugOverlay`). If M1-T3's PR is not yet merged into `dev`, build from a
  branch that contains it.
- The M0 device setup complete (Spatial Data permission, Camera Rig + Passthrough, OVRManager
  Scene Support = Required / Anchor Support = Enabled, MRUK "QR Code Tracking Enabled"). See
  [project-context.md](project-context.md).
- The build + deploy loop working — see [build-and-deploy.md](build-and-deploy.md).

---

## 3. Wire the detection components into the scene

The M1 components are not yet placed in `Assets/Scenes/QRReaderMR.unity`. Add them once (this is
temporary M1 scaffolding, removed with the debug overlay in **M5-T5**):

1. Open `Assets/Scenes/QRReaderMR.unity`.
2. Create an empty GameObject, e.g. **`QRReader`**, at the scene root.
3. Add three components to it (all under **Add Component → QR Reader/…**):
   - **QR Detection Source** (`QrDetectionSource`)
   - **Trackable Lifecycle Manager** (`TrackableLifecycleManager`)
   - **QR Debug Overlay (temporary, M1)** (`QrDebugOverlay`)
4. Leave the serialized references empty — each component auto-resolves its sibling in `Awake`
   (`TrackableLifecycleManager` → `QrDetectionSource`; `QrDebugOverlay` → `TrackableLifecycleManager`).
   Optionally assign them explicitly in the Inspector.
5. Confirm `QRReaderMR` is in **Scenes in Build**, then build (see build-and-deploy.md §3).

> `QrDebugOverlay` logs regardless of platform; its on-screen IMGUI overlay is a convenience for
> the Editor / desktop mirror and is not the on-device source of truth — **logcat is**.

---

## 4. The test QR code

A known-good, compliant test code is committed at
[`assets/test-qr-example.png`](assets/test-qr-example.png):

| Property | Value |
|----------|-------|
| Payload | `https://example.com/qr-test.png` |
| QR version | **3** (29×29 modules) — well within the ≤ 10 limit |
| Error correction | **M** (~15 %) |
| Micro-QR / logo | none |

`example.com` is an IANA-reserved documentation host — the payload is intentionally *not* a
real image. That is fine for M1-T4, which verifies **detection only**; real image/GIF URLs come
with the resolver in **M2**. Regenerate or author your own with the same constraints if needed.

### Authoring / printing constraints (plan risk R6, architecture.md §7)

- Version **≤ 10**; **no** micro-QR; **no** logo'd / stylised codes.
- Print **large** — aim for **≥ 10 cm** per side — on **matte** (non-glossy) paper.
- Mount **flat** (no curl), viewed **close** and **well-lit**, roughly head-on.

---

## 5. Procedure

1. Install and launch the build on the headset (build-and-deploy.md §4–5); grant the **Spatial
   Data** permission prompt if shown.
2. Confirm the app opens into **passthrough** (you see the room).
3. Start following the log on the host:
   ```bash
   adb logcat -s Unity
   ```
   (`Debug.Log` output is tagged `Unity`.)
4. Look at the printed test QR, roughly head-on, ~0.3–0.7 m away, well-lit.
5. Watch for a **`[QR] detected`** line, then periodic **`[QR] tracking`** lines while it stays
   in view.
6. Cover the code, remove it from view, or look away until MRUK drops it — watch for a
   **`[QR] lost`** line.
7. Repeat with **two** codes present at once to confirm independent add/remove (optional but
   recommended ahead of M5).

### Expected log output

The overlay logs one line per event, with live pose/size (`QrDebugOverlay.Describe`):

```
[QR] detected — payload='https://example.com/qr-test.png' pos=(0.12, 1.03, 0.85) rotEuler=(0.4, 178.9, 0.1) size=0.100m x 0.100m tracked=True
[QR] tracking — payload='https://example.com/qr-test.png' pos=(0.12, 1.03, 0.85) rotEuler=(0.4, 178.9, 0.1) size=0.100m x 0.100m tracked=True
[QR] lost — payload='https://example.com/qr-test.png' pos=(0.12, 1.03, 0.85) rotEuler=(0.4, 178.9, 0.1) size=0.100m x 0.100m tracked=False
```

Exact numbers vary; what matters is that the payload is right, `pos`/`rotEuler` track the
physical code, and `size` ≈ the printed dimensions.

---

## 6. Pass / fail checklist

Detection is verified when **all** of these hold on device (not over Link):

- [ ] **V1** — a `[QR] detected` line appears when the code enters view.
- [ ] **V2** — `payload='https://example.com/qr-test.png'` (matches the encoded URL exactly).
- [ ] **V3** — `pos`/`rotEuler` correspond to the code's real position/orientation and stay
      roughly stable while it is held still.
- [ ] **V4** — `size` ≈ the printed code's real width/height (within a reasonable tolerance).
- [ ] **V5** — a `[QR] lost` line appears when the code is removed/occluded.
- [ ] (Optional) two codes tracked simultaneously each add and remove independently.

### Result log (fill in on device)

| Date | Build (branch / commit) | Device | V1 | V2 | V3 | V4 | V5 | Notes |
|------|-------------------------|--------|----|----|----|----|----|-------|
|      |                         |        |    |    |    |    |    |       |

---

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| No `[QR] detected` ever | Verify **on device**, not Link (top of doc). Confirm MRUK "QR Code Tracking Enabled" and the Spatial Data permission were granted. Improve lighting; move closer; flatten the print. |
| Detected once, then never again | Classic **Link** first-run bug — rebuild and run on device. |
| Detected but `payload` empty/garbled | Code is too small/blurry or a non-compliant (micro/logo) code — reprint compliant and larger. |
| `pos`/`size` look wrong | MRUK pose is **low-frequency** and for **static** codes (ADR 0002); hold the code still and re-check. |
| No log lines at all, app runs | `QrDebugOverlay` not in the scene, or its manager reference unresolved — recheck §3 wiring. |
| `adb logcat` shows nothing | Wrong tag/filter — use `adb logcat -s Unity`; confirm `adb devices` lists the headset. |

---

## References

- [Architecture §3.2, §3.3, §7, §8](architecture.md) — detection source, lifecycle, constraints,
  "verify on device".
- [ADR 0002](adr/0002-detect-qr-with-mruk-trackables.md) — MRUK Trackables detection.
- [Implementation plan](implementation-plan.md) — task **M1-T4**, risks **R1** (on-device loop)
  and **R6** (QR authoring).
- [Build & deploy](build-and-deploy.md) — producing and deploying the on-device build.
