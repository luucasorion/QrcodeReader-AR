# QR Reader — On-Device Error-Path Verification (M6-T2)

> Exercises **each individual error path** end-to-end on device and confirms every one routes to the
> **single shared error icon** (architecture.md §4). The M5-T6 integration test
> ([`qr-integration-test.md`](qr-integration-test.md)) only needed *one* failing code to prove the
> error path renders; this procedure drives **all five** causes — non-HTTPS URL, timeout, over-cap
> download, network error, unsupported content type — one at a time. Derived from
> [`architecture.md`](architecture.md) (§3.4, §3.5, §4, §7), ADR
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md), and the
> [implementation plan](implementation-plan.md) (task **M6-T2**). Introduces **no** new architectural
> decisions.

## ⚠️ On-device only

QR-detection testing **must** run from an on-device build — Play-over-Link has a first-run-only
detection bug (see
[build-and-deploy.md](build-and-deploy.md#-on-device-only--do-not-test-qr-detection-over-link)).
Verification is **visual**: you confirm the shared error icon actually renders in passthrough. The
`adb logcat` cause line (where one exists — see §5) is a secondary confirmation, not the pass
criterion.

---

## 1. What this verifies

Every resolve/classify/decode failure is designed to converge on the **one** error visual (§4); the
underlying cause is kept distinct only so the resolver/decoder can log honestly
([`ResolveFailure`](../Assets/QRReader/Runtime/Scripts/Resolver/ResolveResult.cs),
[`DecodeResult`](../Assets/QRReader/Runtime/Scripts/Decoding/DecodeResult.cs)).

| # | Error path | Code cause | How it shows up |
|---|-----------|-----------|-----------------|
| E1 | **Non-HTTPS URL** | `ResolveFailure.BlockedByPolicy` — HTTPS-only gate rejects before any request (`ContentResolver.IsRequestAllowed`) | shared error icon |
| E2 | **Timeout** | `ResolveFailure.Timeout` — request exceeds the configured timeout (default 10 s) | shared error icon |
| E3 | **Over-cap download** | `ResolveFailure.ExceededSizeCap` — declared or streamed body exceeds the cap (default 25 MiB); transfer aborted mid-stream | shared error icon |
| E4 | **Network error** | `ResolveFailure.NetworkError` — DNS/connection/HTTP-status/protocol failure, or an unexpected transport exception | shared error icon |
| E5 | **Unsupported content type** | `DecodeFailure.Unsupported` — classifier returns `ContentKind.Unsupported` (neither image nor GIF), so no decode is attempted | shared error icon |

The components under test: `ContentResolver` (§3.4) → `ContentTypeClassifier` (§3.5) →
`MediaDecoder` (§3.6) → `ContentRenderer.ShowError` (§3.7), driven by `TrackableLifecycleManager`
(§3.3).

---

## 2. Prerequisites

- An **on-device build** including all of **M5** (wired per-QR pipeline, teardown) and **M6-T1** (the
  finalized configuration surface).
- The **`QRReaderMR` scene** ([`Assets/Scenes/QRReaderMR.unity`](../Assets/Scenes/QRReaderMR.unity))
  wired with `QrDetectionSource` + `TrackableLifecycleManager`, the content instance prefab, and the
  **`ContentResolverConfig`** asset
  ([`Assets/QRReader/Runtime/Config/ContentResolverConfig.asset`](../Assets/QRReader/Runtime/Config/ContentResolverConfig.asset)).
- Device setup per ADR 0002: Spatial Data permission granted, passthrough on, MRUK **QR Code Tracking
  Enabled**.
- A network the headset can reach the test URLs on, and hosts you control for the E2–E5 fixtures.

### Configuration in effect

The committed guard values (see [`configuration.md`](configuration.md)):

| Guard | Value |
|-------|-------|
| HTTPS-only | **on** |
| Timeout | **10 s** |
| Download cap | **25 MiB** |

> **Tip — faster E2/E3 runs.** Temporarily lowering the timeout (e.g. 3 s) or the cap (e.g. 1 MiB) in
> `ContentResolverConfig.asset` makes E2 and E3 quicker to trigger and lets a smaller fixture exceed
> the cap. **Restore the shipping values (10 s / 25 MiB) before recording a pass** — the point is to
> verify the shipping configuration.

---

## 3. The error-path QR set

Author one compliant code (ADR 0002: version ≤ 10, no micro-QR, no logo, printed large, flat,
well-lit) **per path** — or reuse a single sheet you re-print/replace between runs. Substitute hosts
you control.

| Code | Payload (example) | Triggers |
|------|-------------------|----------|
| **E1 — non-HTTPS** | `http://<host>/small.png` (valid image, but `http://`) | HTTPS gate rejects before any request |
| **E2 — timeout** | `https://<host>/slow` — an endpoint that stalls > 10 s before responding (or an unroutable but valid HTTPS host) | request exceeds the timeout |
| **E3 — over-cap** | `https://<host>/big.png` — an image **> 25 MiB** (or declaring an oversized `Content-Length`) | size cap aborts the download |
| **E4 — network error** | `https://<nonexistent-subdomain>.<host>/x.png` (DNS failure) or a URL that returns **HTTP 404/500** | connection/protocol failure |
| **E5 — unsupported type** | `https://<host>/page.html` (or `.txt`, `.pdf`) — reachable, under cap, but not image/GIF | classifier → `Unsupported`, no decode |

Notes:
- **E1** needs HTTPS-only **on** (the default). If it were disabled, an `http://` URL would instead be
  fetched and fail as E4/E5 depending on the response.
- **E3**: enforcement is on the declared `Content-Length` **or** the streamed bytes, so either an
  honestly-large file or a server lying big in the header will trip it.
- **E5** is the documented **future website-support seam** (§3.5) — "unsupported" is a routing
  decision, not a crash. Confirming this seam stays clean is **M6-T4**.

---

## 4. Procedure

Run each path independently so its cause is unambiguous:

1. **Launch** the on-device build; grant Spatial Data permission if prompted.
2. For **each** of E1–E5:
   1. Reveal that code in view. It should show the **loading spinner** immediately.
   2. Within a moment (for E2, after the ~10 s timeout), the instance switches to the **shared error
      icon** — the *same* visual for every cause.
   3. Remove the code and confirm the error instance **tears down** (disappears, no ghost).
3. Optionally reveal a **known-good** image/GIF code alongside a failing one to confirm the failure is
   isolated to its own instance (§4) — one code's error never affects another's render.

---

## 5. Confirming the distinct cause (`adb logcat`)

Secondary check only. Attach with `adb logcat -s Unity` and watch as each code is detected. Normal
operation is quiet (M1 debug logging was removed in M5-T5); only failures warn — **and not all paths
log**:

| Path | Logs a cause? | Example line |
|------|---------------|--------------|
| E1 non-HTTPS | **No** (rejected pre-request; silent by design) | — |
| E2 timeout | Yes | `ContentResolver: GET failed for '…': Timeout (ConnectionError: …)` |
| E3 over-cap | Yes | `ContentResolver: download for '…' exceeded the 25 MiB cap; aborted.` |
| E4 network error | Yes | `ContentResolver: GET failed for '…': NetworkError (…)` (or `… threw: …`) |
| E5 unsupported type | **No** (routing decision; no decode attempted, silent by design) | — |

For **E1** and **E5** the error icon rendering **is** the verification — there is no log line to
expect. (If future telemetry needs these paths visible, that's a separate change; today they are
intentionally quiet.)

---

## 6. Pass / fail checklist

Each passes when the code shows the **shared error icon** (and, where applicable, logs its cause):

- [ ] **E1** — non-HTTPS URL → error icon (no log expected).
- [ ] **E2** — timeout → error icon after ~10 s (logs `Timeout`).
- [ ] **E3** — over-cap download → error icon (logs `exceeded … cap; aborted`).
- [ ] **E4** — network error (DNS/404/500) → error icon (logs `NetworkError`).
- [ ] **E5** — unsupported content type → error icon (no log expected).
- [ ] All five render the **same** error visual (no per-cause difference on screen, §4).
- [ ] Each error instance **tears down** cleanly on code removal (no ghost).

**M6-T2 is complete when E1–E5 all pass on the shipping configuration (HTTPS-only on, 10 s, 25 MiB).**

### Result log (fill in on device)

| Date | Build (branch / commit) | Device | E1 | E2 | E3 | E4 | E5 | Notes |
|------|-------------------------|--------|----|----|----|----|----|-------|
|      |                         |        |    |    |    |    |    |       |

---

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| A failing code shows nothing (no spinner, no icon) | Verify **on device**, not Link. Confirm MRUK "QR Code Tracking Enabled" and Spatial Data permission; improve lighting; flatten the print. |
| E1 fetches instead of erroring | HTTPS-only is **off** in `ContentResolverConfig` — re-enable it (it's the transport guard). |
| E2 never times out | Host responded within the timeout, or the URL is actually reachable/fast. Use a genuinely slow/unroutable HTTPS endpoint, or temporarily lower the timeout (then restore 10 s). |
| E3 renders instead of erroring | Fixture is under the 25 MiB cap. Use a larger file, or temporarily lower the cap (then restore 25 MiB). |
| E5 shows the image/GIF | The URL actually served an image/GIF (extension hint or `Content-Type`). Use a genuinely unsupported type (`text/html`, `.txt`, `.pdf`). |
| Not sure which cause fired | Use `adb logcat -s Unity` (§5) — but remember **E1 and E5 are silent by design**; the on-screen error icon is their verification. |

---

## 8. References

- [Implementation plan](implementation-plan.md) — task **M6-T2**, milestone **M6**.
- [Configuration reference](configuration.md) — the guard values under test (M6-T1).
- [On-device multi-QR integration test](qr-integration-test.md) — the M5-T6 end-to-end procedure.
- [Architecture](architecture.md) — §3.4 resolver/guards, §3.5 classifier, §4 per-QR isolation, §7 memory.
- ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md) — HTTPS-only, timeout, cap, image/GIF only.
