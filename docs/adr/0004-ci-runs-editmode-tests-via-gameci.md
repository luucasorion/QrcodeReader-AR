# ADR 0004 — CI Runs EditMode Tests via GameCI on GitHub Actions

- **Status:** Accepted
- **Date:** 2026-07-20
- **Deciders:** Project owner
- **Context of decision:** Test strategy planning session.

## Context
The [implementation plan](../implementation-plan.md) commits to automated tests for the
security-sensitive, off-device logic: the content-type classifier (M2-T6) and the content-resolver
safety guards — HTTPS-only, timeout, and the ~25 MB download cap (M2-T7). These guards protect an
**untrusted-input** path (architecture.md §7, §8), so a regression in them should fail loudly and
early, on the PR that introduces it, not on-device weeks later.

Unity ships the Unity Test Framework (`com.unity.test-framework`, already in the manifest), so the
*framework* choice needs no decision. What needs deciding is **whether and how to run those tests in
CI** — which is new tooling/infrastructure and therefore takes an ADR per architecture.md §8.

Constraints that shape the choice:
- QR detection, pose tracking, rendering, and texture teardown can only be verified **on device**
  (architecture.md §7; plan R1). CI cannot run a headset, MRUK, or the play loop meaningfully.
- Running Unity headless in CI requires an **activated Unity license** inside the runner, which
  means storing license credentials as CI secrets.

## Decision
Run **EditMode tests only** in CI, using **GameCI** (`game-ci/unity-test-runner`) on **GitHub
Actions**, gating pull requests into `dev` and `main`.

- Scope CI to `testMode: editmode` — the pure-C# logic layer (classifier, resolver guards, decode
  failure paths). PlayMode / on-device behaviour is explicitly **out of CI scope**.
- The workflow triggers on `pull_request` into `dev`/`main` and on manual `workflow_dispatch`.
- Unity activation credentials are provided via repository secrets (`UNITY_LICENSE`, `UNITY_EMAIL`,
  `UNITY_PASSWORD`). Managing/rotating these is owned by whoever holds the Unity licensing — they
  are **not** committed to the repo.

## Rationale
- Directly protects the untrusted-input guards (§8) at the point of change.
- EditMode tests are fast and headset-free, matching plan R1's "keep decoder/classifier/renderer
  unit-testable off-device."
- GameCI is the de-facto standard for Unity on GitHub Actions and auto-detects the editor version
  from `ProjectSettings/ProjectVersion.txt`, so CI tracks the project's Unity version automatically.
- GitHub Actions is already where the repo's ecosystem lives (issue/PR templates, CODEOWNERS).

## Consequences
- Positive: security guards and classifier routing are regression-gated on every PR; contributors
  get a red check before merge, not a field bug.
- Positive: forces the tested logic to stay decoupled from `MonoBehaviour`/MRUK (a design good in
  its own right, per §8).
- Negative / constraints:
  - Requires **Unity license secrets** in the CI environment — a credential surface that must be
    owned and rotated by the licensing owner; never checked into the repo.
  - Unity CI Docker images are large; cold runs take minutes even for a few tests. Mitigated by
    caching `Library/` and keeping CI to EditMode only.
  - CI gives **no coverage** of detection/rendering/memory — those remain on-device checks
    (plan M5-T6, M6). CI is a floor, not the whole test story.

## Alternatives Considered
- **No CI (run tests locally only)** — rejected: guards on untrusted input are exactly what should
  not depend on a human remembering to run the Test Runner.
- **Run PlayMode / device tests in CI** — rejected: no headset or MRUK in CI; detection/rendering
  are on-device concerns by decision (architecture.md §7).
- **Self-hosted runner with a seat-based license** — deferred: heavier to operate; revisit only if
  GameCI's hosted-runner activation proves insufficient.
