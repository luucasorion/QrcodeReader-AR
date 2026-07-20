# CLAUDE.md — QR Reader (Quest 3 AR)

Project docs live in [`docs/`](docs/): [project context](docs/project-context.md),
[architecture](docs/architecture.md), the [ADRs](docs/adr/), and the
[implementation plan](docs/implementation-plan.md). Read these before proposing changes.
Do not introduce a new technology or architectural decision without a new ADR (or updating an
existing one) — per the project's own convention (architecture.md §8).

## Git workflow (required)

Never commit or push directly to `main`. For **any** change:

1. Create a descriptive branch off `main` (e.g. `feat/…`, `fix/…`, `docs/…`, `chore/…`).
2. Commit with a clear message.
3. Push the branch and open a **pull request** with a summary of what changed and why.
4. **Leave merging/accepting the PR to the user** — do not merge, and do not `git push` to `main`.

Notes:
- Group related work into one branch/PR; don't open a PR per tiny commit.
- Never force-push shared branches or `main`; never bypass hooks or signing unless asked.
- End commit messages with the `Co-Authored-By` trailer and PR bodies with the Claude Code footer,
  per the harness defaults.
