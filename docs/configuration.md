# Configuration surface

The QR Reader exposes a small, fixed set of configurable values. Three protect the
**untrusted-input** download path (ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md));
one controls how large the rendered content appears. All four are authored in the Unity Editor as
`ScriptableObject` assets — nothing here is hard-coded, per the "configurable values stay
configurable" convention ([architecture §8](architecture.md#8-development-conventions)).

## Values

| Value | Asset · field | Default | Min (clamped) | Effect |
|-------|---------------|---------|---------------|--------|
| **HTTPS-only** | `ContentResolverConfig` · `_httpsOnly` | `true` (on) | — | Rejects any payload URL that is not `https://` **before** a request is made. Disabling it removes the transport guard on untrusted input — keep it on. |
| **Timeout** | `ContentResolverConfig` · `_timeoutSeconds` | `10` s | `1` | Aborts the download if the request exceeds this many seconds → error state. |
| **Download cap** | `ContentResolverConfig` · `_maxDownloadMebibytes` | `25` MiB | `1` | Fails the download if the content exceeds this size — whether reported by `Content-Length` or observed mid-stream → error state. Bounds memory on Quest. |
| **Render scale factor** | `ContentRendererConfig` · `_renderScaleFactor` | `3×` | `1` | Multiplier applied to the QR's `PlaneRect` to size the content quad. `1` = exact QR size; larger scales the content up. Never below `1` (content is scaled up, not down). |

The `Min` column is enforced in each asset's `OnValidate`, so an asset accidentally edited to
`0`/negative can never silently disable a guard or shrink content below the physical QR.

## Where the assets live and how they're wired

Both assets are committed under `Assets/QRReader/Runtime/Config/`:

- **`ContentResolverConfig.asset`** — the three download guards. Wired into the
  `TrackableLifecycleManager` (its `resolverConfig` field); the manager constructs the
  `ContentResolver` from it. If unassigned, every QR routes to the error state (the payload can't be
  resolved).
- **`ContentRendererConfig.asset`** — the render scale factor. Wired into the `ContentRenderer` on
  the `QrContentInstance` prefab (its `_config` field). If unassigned, the renderer falls back to
  the default scale factor (`ContentRendererConfig.DefaultRenderScaleFactor`, `3×`).

## Editing the values

1. In the Project window, select the asset under `Assets/QRReader/Runtime/Config/`.
2. Edit the fields in the Inspector. Changes take effect on the next play/build; no code change is
   needed.
3. To author a fresh asset, use `Assets ▸ Create ▸ QR Reader ▸ Content Resolver Config` or
   `… ▸ Content Renderer Config`, then drag it into the corresponding component field above.

## See also

- [Architecture §3.4](architecture.md) — content resolver, download + safety guards.
- [Architecture §3.7–3.8](architecture.md) — renderer and configuration.
- ADR [0003](adr/0003-qr-payload-is-runtime-downloaded-url.md) — why the payload is a
  runtime-downloaded URL and why the guards are mandatory.
- [On-device QR integration test](qr-integration-test.md) — exercising the guards on device.
