# ADR 0002 — Detect QR Codes with MRUK Trackables

- **Status:** Accepted
- **Date:** 2026-07-20
- **Deciders:** Project owner
- **Context of decision:** Grill Me planning session.

## Context
The app must detect QR codes through Quest 3 passthrough, obtain each code's payload, and know its position/orientation in the real world so content can be rendered in its place.

Two broad approaches exist on Quest:
1. Read the raw camera feed via the **Passthrough Camera API**, run a third-party QR decoder (e.g. ZXing.Net) over the pixels, and compute the QR's 3D pose from the detected 2D corners.
2. Use **Meta MRUK Trackables**, a first-party API that detects QR codes and reports pose + payload directly.

## Decision
Use **Meta MRUK Trackables** for QR detection.

- Subscribe to `TrackableAdded`, filter `TrackableType == QRCode`.
- Read the payload from `MarkerPayloadString`.
- Use the trackable's `Transform` for pose and `PlaneRect` for physical size.

## Rationale
- First-party: pose, payload, and lifecycle are provided directly — no camera-permission plumbing, no third-party decoder, no manual pose math.
- Collapses the whole front half of the pipeline (camera access → decode → pose) into an SDK event.

## Consequences
- Positive: far less custom code; Meta manages the trackable lifecycle and multi-trackable tracking.
- Negative / constraints (from Meta docs):
  - Pose updates at **low frequency** — unsuitable for QR codes on objects moving in real time.
  - Requires device setup: Spatial Data permission; Camera Rig + Passthrough Layer; OVRManager Scene Support = Required, Anchor Support = Enabled; MRUK Tracker Configuration → "QR Code Tracking Enabled".
  - QR content limits: version ≤ 10, no micro-QR, no logo'd codes; must be relatively large, close, well-lit.
  - **PC Link caveat:** detection may only work on first run over Link — testing must use on-device builds.
  - Requires Meta XR Core SDK + MRUK v83+ (project is on v203). **Verified (2026-07-20)** via Unity reflection: `OVRAnchor.TrackableType` (assembly `Oculus.VR`) exposes `None, Keyboard, QRCode`, and `Meta.XR.MRUtilityKit.MRUKTrackable` exposes `TrackableType`, `MarkerPayloadString`, `MarkerPayloadBytes`, `IsTracked`.

## Alternatives Considered
- **Passthrough Camera API + ZXing.Net + manual pose math** — rejected: significantly more code (camera access, decoding, pose estimation) for a result MRUK provides first-party.
- **OpenCV for Unity** for detection — rejected: overkill and paid.
