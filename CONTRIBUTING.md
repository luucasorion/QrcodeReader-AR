# Contributing

Thanks for working on **QR Reader (Quest 3 AR)**. This is currently an internal, early-development
project (pre-M0 — see the [implementation plan](docs/implementation-plan.md)). These guidelines keep
changes consistent with the project's decisions.

## Before you start

Read the docs — they are the source of truth, and changes are expected to follow them:

- [Project context](docs/project-context.md) — scope, requirements, constraints, rejected alternatives
- [Architecture](docs/architecture.md) — how the system is organized (see **§8 Development Conventions**)
- [ADRs](docs/adr/) — the architectural decisions and their rationale
- [Implementation plan](docs/implementation-plan.md) — milestones, tasks, dependencies

**Decisions before code.** Do not introduce a new technology or architectural decision without a
**new or updated ADR** (architecture §8). Out-of-scope items (websites, spatial-anchor persistence,
texture caching) are deferred and also require an ADR before implementation.

## Git workflow (required)

Never commit or push directly to `main`. For **any** change:

1. Create a descriptive branch off `main`: `feat/…`, `fix/…`, `docs/…`, or `chore/…`.
2. Commit with a clear message (imperative mood, e.g. `fix: free GIF textures on removal`).
3. Push the branch and open a **pull request** describing what changed and why, using the PR template.
4. **Leave merging to the repo owner** — do not merge, and do not push to `main`.

Group related work into one branch/PR — don't open a PR per tiny commit. Never force-push shared
branches or `main`, and don't bypass hooks or signing unless asked.

## Testing & verification

- **Detection, rendering, tracking, and teardown must be verified on-device (Quest 3 / 3S).**
  Play-over-Link has a first-run-only QR detection bug — do not rely on Link for QR behaviour.
- Keep the off-device-testable pieces (content resolver, classifier, decoders) covered by unit
  tests so they can be validated without a headset.
- For memory-sensitive changes (especially GIF frame textures), validate with the profiler that
  textures are freed on `TrackableRemoved`.

## Code conventions

- Match the surrounding code's style, naming, and structure.
- Keep the **untrusted-input path isolated**: all network access lives behind the content resolver
  and its guards (HTTPS-only, timeout, download cap). No ad-hoc `https` GETs from QR payloads.
- Keep configurable values (download cap, timeout, HTTPS-only, render scale) configurable, not
  hard-coded.
- Fail to the shared **error state**, never silently.

## Security

- Never commit secrets, tokens, API keys, or private URLs — not in code, configs, logs, or issue/PR
  text. If one is exposed, rotate it and remove it from history.
- Treat QR payloads as untrusted input.

## Editor tooling

Meta-XR Editor setup (rig, passthrough, Android manifest / permissions, MRUK config) goes through the
**Meta XR Unity MCP Extension** ([ADR 0001](docs/adr/0001-use-meta-xr-unity-mcp-extension.md)) rather
than hand-configuration where possible.
