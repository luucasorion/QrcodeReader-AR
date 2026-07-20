# ADR 0003 — QR Payload Is a URL Downloaded at Runtime

- **Status:** Accepted
- **Date:** 2026-07-20
- **Deciders:** Project owner
- **Context of decision:** Grill Me planning session.

## Context
When a QR code is detected, the app must decide what content to render in its place (image or GIF for the MVP). The content-sourcing model shapes networking, offline behavior, rebuild cadence, and how untrusted input is handled.

Options considered:
- **A) URL:** the QR encodes a URL; content is downloaded at runtime.
- **B) Bundled ID → asset:** the QR encodes an ID mapped to assets shipped inside the app.
- **C) Hybrid:** support both.

## Decision
The QR payload is a **URL**, and content is **downloaded at runtime** (option A).

- Accept **`https://` only**.
- Classify content type by **file-extension hint first, `Content-Type` header fallback**.
- Route: image → texture; GIF → decode with **mgGif**; anything else → error state.

## Rationale
- No rebuild required to change content; any QR pointing at a valid media URL works.
- Aligns with the deferred "website" ambition (a URL model extends naturally to `text/html`).

## Consequences
- Positive: fully dynamic content; natural path to future website support.
- Negative / constraints:
  - The app owns **networking**: latency, failures, timeouts.
  - QR payloads are **untrusted input**, requiring safety guards (see below).
  - Requires runtime loading/error UX: loading spinner on detect, error icon on failure/timeout/unsupported type.
- **Safety guards decided:** HTTPS-only; **~25 MB download cap** (configurable); **10 s timeout** (configurable).
- **Memory:** GIFs decode to per-frame textures (main memory risk on Quest). Content is destroyed on `TrackableRemoved`; caching is deferred.

## Alternatives Considered
- **B) Bundled ID → asset** — rejected: requires a rebuild (or asset-bundle system) for every content change; no path to dynamic/web content.
- **C) Hybrid** — rejected for the MVP: added complexity without MVP benefit.
- **Type classification by extension only / Content-Type only** — rejected in favor of extension hint + Content-Type fallback.
- **GIF decoders UniGif / Unity-GifDecoder** — rejected: UniGif is slower and under the Unity-Chan license; mgGif chosen for performance and license.
