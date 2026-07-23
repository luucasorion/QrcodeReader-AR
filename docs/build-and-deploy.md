# QR Reader (Quest 3 AR) — Build & On-Device Deploy Loop

> How to build the app and get it running on a Meta Quest 3 / 3S, and the fast
> iterate-on-device loop to use while developing. Derived from
> [`project-context.md`](project-context.md), [`architecture.md`](architecture.md),
> and ADRs [0004](adr/0004-adopt-unity-cli-for-build-and-ci.md) /
> [0005](adr/0005-adopt-unity-cli-for-local-build-and-dev.md). Introduces **no** new
> architectural decisions.

## ⚠️ On-device only — do not test QR detection over Link

**All QR-detection testing must run from an on-device build.** Play-over-Link (Quest
Link / Air Link with Play mode in the Editor) has a **first-run-only QR detection bug**:
detection may work once and then stop, so Link results are **not trustworthy** for this
project. Treat on-device build behaviour as the source of truth for detection
(architecture.md §8, "Verify on device"; implementation plan risk **R1**).

Link is still fine for non-detection iteration (UI, rendering against stub data), but never
conclude anything about QR detection from a Link run.

---

## 1. Prerequisites

| Requirement | This project's value | Notes |
|-------------|----------------------|-------|
| Unity Editor | **6000.3.20f1** (Unity 6) | Pinned in [`ProjectSettings/ProjectVersion.txt`](../ProjectSettings/ProjectVersion.txt). Install the matching version via Unity Hub. |
| Unity module | **Android Build Support** (incl. OpenJDK, Android SDK & NDK) | Required to build the Quest APK. |
| Headset | Meta **Quest 3 / 3S** in **Developer Mode** | Enable via the Meta Horizon / Meta Quest phone app → *Headset settings → Developer mode*. |
| Cable / transport | USB-C data cable (or ADB over Wi-Fi) | Needed for install + logcat. |
| Deploy tool | **adb** (Android Platform Tools) and/or **Meta Quest Developer Hub (MQDH)** | `adb` ships with the Unity Android module; MQDH is optional but convenient. |
| Meta XR SDK | **v203** packages (via `Packages/manifest.json`) | Already in the project manifest. |

The device-side setup required for QR tracking (Spatial Data permission, Camera Rig +
Passthrough, OVRManager, MRUK "QR Code Tracking Enabled") is covered in
[project-context.md](project-context.md#device-setup-required-for-qr-tracking) and configured
by milestone **M0**; it is a prerequisite for detection but not for producing a build.

---

## 2. Build settings (already configured in the repo)

These are set in [`ProjectSettings/ProjectSettings.asset`](../ProjectSettings/ProjectSettings.asset)
(milestones M0-T5 / M0-T6). Verify, don't re-invent them:

| Setting | Value |
|---------|-------|
| Platform / target | **Android** (Quest 3 / 3S) |
| Product name | `QrcodeReader-VR` |
| Company name | `Valtech` |
| Application ID | `com.valtech.qrcodereadervr` |
| Scripting backend | **IL2CPP** |
| Target architecture | **ARM64** (only) |
| Minimum API level | **Android 12 (API 32)** |
| Target API level | **Android 14 (API 34)** |
| Render pipeline | **URP** |
| XR plug-in | **OpenXR** (Meta Quest feature group) |

> If the build platform is not already Android, switch it via **File → Build Profiles**
> (Unity 6) → select the Android profile → **Switch Platform**.

---

## 3. Building the APK

There are two supported paths. The **Editor build** is the reliable baseline; the **Unity CLI
build** is the ADR-adopted path for reproducible/headless local builds
([ADR 0004](adr/0004-adopt-unity-cli-for-build-and-ci.md),
[ADR 0005](adr/0005-adopt-unity-cli-for-local-build-and-dev.md)).

### Path A — Unity Editor (baseline)

1. Open the project in Unity **6000.3.20f1**.
2. **File → Build Profiles** → confirm the **Android** platform is active (Switch Platform if
   needed).
3. Confirm the passthrough MR scene is in **Scenes in Build**.
4. Connect the headset over USB and confirm it appears under **Run Device** (accept the *Allow
   USB debugging* prompt in the headset if shown).
5. Click **Build And Run** to build the APK and install + launch it on the headset in one step,
   or **Build** to produce an APK you deploy manually (see §4).

### Path B — Unity CLI (reproducible / headless)

Per ADR 0004 / 0005, the official Unity CLI is adopted for **local headless build/test** while
UnityMCP remains the authoring bridge. As recorded in those ADRs the CLI was verified on the
project owner's machine (binary at `%LOCALAPPDATA%\Unity\bin\unity.exe`, version
`1.0.0-beta.2`, seeing editor `6000.3.20f1` with Android modules).

```bash
# Verify the toolchain resolves the pinned editor + Android module
unity editors list

# Headless Android build (run from the project root)
unity build --platform Android
```

> **Beta caveat:** the Unity CLI is early (`1.0.0-beta.2`); flag/subcommand shapes may change
> across releases. Confirm the exact `build` flags for your installed version with
> `unity build --help`, and fall back to Path A if a CLI build misbehaves. Do **not** run the
> CLI's `mcp` mode against the same Editor that UnityMCP is driving (ADR 0004/0005: two bridges
> must not drive one Editor at once).

---

## 4. Deploying to the headset

If you used **Build And Run** (§3 Path A), the APK is already installed and launched — skip to
§5. Otherwise install the APK you built:

```bash
# List connected devices (should show the Quest's serial)
adb devices

# Install (or reinstall, keeping data) the APK
adb install -r path/to/QrcodeReader-VR.apk

# Launch it without taking off the headset
adb shell monkey -p com.valtech.qrcodereadervr -c android.intent.category.LAUNCHER 1
```

Alternatively, drag the APK onto **Meta Quest Developer Hub → Device → Apps → Install APK**,
then launch it from the headset's app library (filter by **Unknown Sources**).

---

## 5. Verify on device

1. Put on the headset and launch the app (**Unknown Sources** in the app library if not
   auto-launched).
2. Confirm it opens into **passthrough** (you see the real room), not a black/VR scene.
3. On first launch, **grant the Spatial Data permission** prompt if it appears — QR tracking
   will not work without it.
4. Point the headset at a **compliant printed QR code** (version ≤ 10, no micro-QR, no logo,
   large, well-lit — see [README](../README.md#authoring-a-compliant-qr-code)).

While iterating, watch the device log:

```bash
# Follow this app's logs (Unity messages are tagged "Unity")
adb logcat -s Unity
```

For the full QR add/remove detection check (scene wiring, a known-good test QR, expected log
output, and a pass/fail checklist), follow
[qr-detection-verification.md](qr-detection-verification.md) (milestone **M1-T4**).

---

## 6. The iteration loop

The fast on-device loop while developing:

```
edit → build (Editor "Build And Run", or `unity build --platform Android`)
     → app launches on headset → observe passthrough + `adb logcat -s Unity`
     → repeat
```

Keep off-device work unit-testable so you are not forced through a device build for every
change: the resolver, classifier, decoder, and renderer are designed to be exercised with unit
tests / stub data (implementation plan risk **R1**; tasks M2-T7, M4). Reserve the device loop
for what genuinely needs the headset — **QR detection above all** (§ top of this doc).

---

## 7. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| `adb devices` shows nothing / `unauthorized` | Cable is charge-only, or the *Allow USB debugging* prompt in the headset wasn't accepted. Swap cable, re-plug, accept the prompt. |
| App launches into a black screen, not passthrough | Passthrough / Camera Rig not configured, or Spatial Data permission denied. Recheck M0 device setup and the permission prompt. |
| QR codes never detected (but detection "worked once") | You are testing over **Link** — rebuild and test **on device** (see top). Otherwise verify the QR is compliant and MRUK "QR Code Tracking Enabled" is set. |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` on install | A build with a different signing key is installed. `adb uninstall com.valtech.qrcodereadervr` then reinstall. |
| Build fails on missing Android SDK/NDK | The Unity **Android Build Support** module (with SDK/NDK/JDK) isn't installed for `6000.3.20f1`. Add it in Unity Hub. |

---

## References

- [Project context](project-context.md) — requirements, constraints, on-device-only rule.
- [Architecture §3.1, §8](architecture.md) — XR/passthrough foundation; "verify on device".
- [Implementation plan](implementation-plan.md) — milestone **M0**, risk **R1**.
- [ADR 0004](adr/0004-adopt-unity-cli-for-build-and-ci.md) /
  [ADR 0005](adr/0005-adopt-unity-cli-for-local-build-and-dev.md) — Unity CLI for build/local dev.
