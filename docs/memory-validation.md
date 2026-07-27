# QR Reader — On-Device GIF Memory / Leak Validation (M6-T3)

> Confirms that a tracked GIF's decoded frame textures are **actually freed** when its QR code is
> removed, and that **repeated** track → remove cycles don't leak (memory returns to baseline each
> time, no monotonic growth). GIF frame textures are the primary memory risk on Quest
> (architecture.md §7), so this is the profiler-backed check that the M5-T4 teardown holds up under
> real load. Derived from [`architecture.md`](architecture.md) (§3.3, §3.6, §7), ADR
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md), and the
> [implementation plan](implementation-plan.md) (task **M6-T3**). Introduces **no** new architectural
> decisions.

## ⚠️ On-device only

Measure on an **on-device build** connected to the profiler, not over Link. Quest RAM is shared and
limited (§7) and Link/Editor memory does not represent device behavior; QR detection over Link is
also unreliable (see
[build-and-deploy.md](build-and-deploy.md#-on-device-only--do-not-test-qr-detection-over-link)).

---

## 1. What this verifies

| # | Claim | How you confirm it |
|---|-------|--------------------|
| M1 | Tracking a GIF **allocates** frame textures | GFX / texture memory rises by roughly (frame count × frame resolution × 4 bytes) after the GIF renders |
| M2 | Removing the QR **frees** those textures | after teardown + a GC/unused-assets pass, the `Texture2D` count and GFX memory return to ~baseline |
| M3 | Repeated track → remove cycles **don't leak** | across N cycles the post-removal baseline stays flat — no monotonic climb in texture count or GFX memory |

### The teardown path under test (M5-T4)

On `TrackableRemoved`, `TrackableLifecycleManager.OnQrCodeLost` does, in order:

1. Marks the entry retired (an in-flight pipeline abandons its result) and removes it from the map.
2. `entry.Content?.Dispose()` →
   [`DecodedContent.Dispose`](../Assets/QRReader/Runtime/Scripts/Decoding/DecodedContent.cs) destroys
   **every** `GifFrame.Texture` via `TextureCleanup.Destroy` (`Object.Destroy` at runtime) and clears
   the frame list. This is the GIF frame memory.
3. `DestroyInstance(renderer)` destroys the content-instance GameObject, whose `OnDestroy` handlers
   free the remaining GPU objects: the renderer's media material
   ([`ContentRenderer.OnDestroy`](../Assets/QRReader/Runtime/Scripts/Rendering/ContentRenderer.cs)),
   and the child spinner/error material instances and textures
   ([`LoadingSpinner`](../Assets/QRReader/Runtime/Scripts/Rendering/LoadingSpinner.cs),
   [`ErrorIcon`](../Assets/QRReader/Runtime/Scripts/Rendering/ErrorIcon.cs)).

There is **no caching** (§7): a QR that returns is decoded from scratch, so a freed GIF must
re-allocate on re-detect — a useful secondary signal that the old textures really went away.

---

## 2. Prerequisites

- An **on-device build** including **M5-T4** teardown (all of M5), development build with
  **Autoconnect Profiler** (or connect manually over Wi-Fi / USB).
- The **Unity Profiler** (Memory module) and, strongly recommended, the **Memory Profiler package**
  (`com.unity.memory-profiler`) for snapshot diffing by object.
- A **large GIF** fixture at an `https://` URL under the 25 MiB download cap but with **many / large
  frames** (that's the memory pressure — the cap bounds the *download*, not the *decoded* size). Note
  its frame count and resolution so you can predict the expected allocation (M1).
- The `QRReaderMR` scene wired as in
  [`qr-integration-test.md`](qr-integration-test.md#2-prerequisites); device setup per ADR 0002.

---

## 3. Procedure

### 3a. Single cycle — allocate then free (M1, M2)

1. Launch the build with the profiler attached; let it reach a steady state with **no** QR in view.
   Take a **baseline** Memory Profiler snapshot (or note GFX memory + `Texture2D` count in the Memory
   module).
2. Reveal the **large GIF** code. Wait for it to download, decode, and **animate**.
3. Take a snapshot. Confirm GFX/texture memory **rose** and the `Texture2D` count increased by ~the
   GIF's frame count (**M1**).
4. Remove the code (cover / take away the sheet). Wait a moment for teardown.
5. Force reclamation so the check isn't fooled by lazily-released memory: trigger a GC / unused-asset
   unload — e.g. leave it idle a few seconds, or use a dev hook that calls `Resources.UnloadUnusedAssets()`
   followed by `GC.Collect()` if you have one wired.
6. Take another snapshot. Confirm the `Texture2D` count and GFX memory **returned to ~baseline**
   (**M2**). In the Memory Profiler, **diff** the post-removal snapshot against the peak and confirm
   the GIF's frame `Texture2D` objects are **gone**, not merely unreferenced.

### 3b. Repeat — leak check (M3)

7. Repeat 3a **at least 5–10 times** (reveal → animate → remove → reclaim). The GIF can be the same
   fixture each time.
8. Plot or eyeball the **post-removal** baseline across cycles. A healthy result is **flat**: each
   cycle returns to about the same texture count / GFX memory. A **rising** post-removal baseline
   (a staircase that never comes back down) indicates a leak — capture snapshots at cycle 1 and cycle
   N and diff to find the surviving `Texture2D` (or material) objects and their retaining references.

### 3c. Multi-QR variant (optional but recommended)

9. Track the GIF alongside an image and a failing code (the M5-T6 set), then remove them **one at a
   time**, confirming each teardown frees only its own instance's textures (§4 isolation) and the
   others' memory is untouched.

---

## 4. Pass / fail checklist

- [ ] **M1** — tracking the GIF raises GFX/texture memory by ~(frames × resolution × 4 B).
- [ ] **M2** — after removal + a reclaim pass, `Texture2D` count and GFX memory return to ~baseline;
      a snapshot diff shows the GIF frame textures are **destroyed**, not just dereferenced.
- [ ] **M3** — across 5–10 track/remove cycles the post-removal baseline stays **flat** (no leak).
- [ ] (optional) removing one code in a multi-QR scene frees only that instance's textures.

**M6-T3 is complete when M1–M3 hold on device.** If M3 fails, treat the M5-T4 teardown path (§1) as
regressed and capture the diffed snapshots on the issue.

### Result log (fill in on device)

| Date | Build (branch / commit) | Device | GIF (frames × res) | Baseline MB | Peak MB | Post-removal MB | Cycles | Leak? | Notes |
|------|-------------------------|--------|--------------------|-------------|---------|-----------------|--------|-------|-------|
|      |                         |        |                    |             |         |                 |        |       |       |

---

## 5. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| Post-removal memory doesn't drop | Reclamation is lazy — force `Resources.UnloadUnusedAssets()` + `GC.Collect()` and re-snapshot before concluding. Destroyed textures free GFX memory but the accounting can lag a frame/GC. |
| `Texture2D` count drops but GFX memory doesn't | Something still references a render target / material — diff snapshots to find the retainer; check `ContentRenderer._mediaMaterial` and spinner/error material instances are freed by their `OnDestroy`. |
| Memory climbs even without removing codes | Not a teardown leak — likely GIF playback allocating per frame. Frames are pre-decoded and reused (`ContentRenderer.Update` swaps `mainTexture`, no per-frame alloc); investigate separately. |
| Baseline rises only on **re-detect** | Expected: no caching (§7), so a returning QR re-allocates. The leak signal is the **post-removal** baseline, not the re-detect peak. |
| Numbers unstable over Link | Measure **on device** (top of doc); Link/Editor memory is not representative. |

---

## 6. References

- [Implementation plan](implementation-plan.md) — task **M6-T3**, milestone **M6**; risk **R3** (GIF memory).
- [Architecture](architecture.md) — §3.6 decoders, §7 memory (GIF frame textures), §4 per-QR isolation.
- ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md) — GIF frames as the main memory risk; destroy-on-removal, no caching.
- [On-device multi-QR integration test](qr-integration-test.md) — V8 (no lingering content) is the visual precursor to this profiler check.
- [Error-path verification](error-path-verification.md) — the sibling M6-T2 procedure.
