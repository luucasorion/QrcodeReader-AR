# The unsupported-type → error seam (future website-support hook)

> **M6-T4.** Confirms that the "unsupported content type → error state" path is a **clean, single
> seam**, and documents it as the hook where **future website support** would attach. This is a
> design/confirmation note — it changes **no** runtime behavior. Actually building website rendering
> is **out of scope** (project-context §"Out of scope") and would require a **new ADR**
> ([architecture §8](architecture.md#8-development-conventions)); this doc only describes where and
> how it would plug in. Grounded in [`architecture.md`](architecture.md) §3.5, ADR
> [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md).

## 1. What "the seam" is

The QR payload is a URL to *some* content. Today the app renders **images** and **GIFs**; anything
else (a `text/html` page, a PDF, plain text, …) is **not an error condition** — it's simply a type
we don't render *yet*. The pipeline treats it as a first-class routing decision (`Unsupported`) that
currently terminates in the shared error visual. That single decision point is the seam: swap the
"unsupported → error" edge for "unsupported → website renderer" and the app gains website support
without touching resolve, decode, teardown, or isolation.

## 2. The seam, traced in code (confirmation it's clean)

The unsupported path is **one decision** and **one routing arm** — there is no scattered
type-sniffing anywhere else in the pipeline:

1. **Classification — the only place `Unsupported` is decided.**
   [`ContentTypeClassifier.Classify`](../Assets/QRReader/Runtime/Scripts/Classification/ContentTypeClassifier.cs)
   returns exactly one of `Image` / `Gif` / `Unsupported`
   ([`ContentKind`](../Assets/QRReader/Runtime/Scripts/Classification/ContentKind.cs)). Extension hint
   first, then `Content-Type` header; **everything** that isn't a recognized image/GIF falls through
   to `ContentKind.Unsupported`. `ContentKind` is *produced* nowhere else.

2. **Routing — the only place `Unsupported` is acted on.**
   [`MediaDecoder.Decode`](../Assets/QRReader/Runtime/Scripts/Decoding/MediaDecoder.cs) switches on
   the kind: `Image` → `ImageDecoder`, `Gif` → `GifDecoder`, and the **`default`** arm maps
   `Unsupported` to `DecodeResult.Failed(DecodeFailure.Unsupported)` **without invoking any decoder**.
   `ContentKind` is *consumed* nowhere else.

3. **A named, distinct outcome.**
   [`DecodeFailure.Unsupported`](../Assets/QRReader/Runtime/Scripts/Decoding/DecodeResult.cs) is a
   separate value from `Undecodable` (a *malformed* image/GIF). So "we don't render this type" is a
   distinct, named concept from "this was the right type but broken" — a future feature can branch on
   exactly the former without disturbing the malformed-media path.

4. **Termination — generic, not special-cased.**
   `TrackableLifecycleManager.ResolveClassifyDecodeRenderAsync` branches only on
   `decoded.Success`; on failure it calls `renderer.ShowError(qr)`. It does **not** inspect *why* it
   failed — `Unsupported` and `Undecodable` reach the error visual through the same generic edge (§4,
   the shared error visual).

**Conclusion (M6-T4 confirmation):** the seam is clean. One producer (classifier), one consumer
(decoder switch), one distinct outcome (`DecodeFailure.Unsupported`), and a generic termination. No
`if (type == "text/html")` checks leak into the resolver, renderer, or lifecycle manager. Per-QR
isolation (§4) and teardown (§7) are unaffected because an unsupported instance holds no decoded
content to free.

## 3. Where website support would attach

A future website feature (behind a new ADR) would extend the seam at the two points above, and add a
render path — **without** reopening the untrusted-input download guards or the per-QR lifecycle:

1. **Distinguish web content at classification.** Either add a `ContentKind.Website` and classify
   `text/html` (and friends) into it, or carry the concrete media type forward. This is the one
   place that decides "this is a website."
2. **Route it instead of failing.** In `MediaDecoder.Decode` (or a sibling stage), route the new kind
   to a website-rendering path rather than the `default` → `DecodeFailure.Unsupported` arm. Content
   that is *still* neither media nor website continues to fall through to `Unsupported` → error,
   so the seam never fully closes — there is always a residual unsupported set.
3. **Add a renderer state.** `ContentRenderer` gains a website surface/state alongside
   `Loading` / `Error` / `Media` (e.g. a textured web-view quad), and the lifecycle manager shows it
   on success of the website path. Its resources join the same teardown (§7) so removal still frees
   everything.

### Deliberately unchanged by that work

- **Transport guards** (HTTPS-only, timeout, cap) — a website is still an untrusted `https` fetch and
  stays behind the resolver (§3.4, §8). No new network path.
- **Per-QR isolation & teardown** (§4, §7) — one instance per QR, destroyed on removal, regardless of
  content kind.
- **The error seam itself** — anything still unsupported keeps routing to the shared error visual.

## 4. Why it's a hook and not built now

Website rendering is explicitly **out of scope** for the MVP (project-context §"Out of scope",
[implementation plan](implementation-plan.md)). Rendering arbitrary web content on Quest raises
questions this project hasn't decided — a web-view/texture strategy, input, performance, and the
security surface of rendering untrusted HTML — so it belongs in its **own ADR** when the time comes,
not smuggled in here. M6-T4's job is only to guarantee the door is in the right place and easy to
open, which §2 confirms it is.

## 5. References

- [Architecture §3.5](architecture.md) — classifier and the documented seam; §4 shared error visual; §8 conventions (new ADR rule).
- ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md) — URL payload; "natural path to future website support".
- [Error-path verification §E5](error-path-verification.md) — the on-device confirmation that unsupported types render the error icon (M6-T2).
- Code: [`ContentTypeClassifier`](../Assets/QRReader/Runtime/Scripts/Classification/ContentTypeClassifier.cs), [`ContentKind`](../Assets/QRReader/Runtime/Scripts/Classification/ContentKind.cs), [`MediaDecoder`](../Assets/QRReader/Runtime/Scripts/Decoding/MediaDecoder.cs), [`DecodeResult`](../Assets/QRReader/Runtime/Scripts/Decoding/DecodeResult.cs).
