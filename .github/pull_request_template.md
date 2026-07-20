<!--
Thanks for contributing! Keep PRs focused. See CONTRIBUTING.md for the full workflow.
Reminder: branches are made off `main`; the repo owner merges — do not merge to `main` yourself.
-->

## What & why

<!-- What does this change, and why? Link the issue it closes, e.g. "Closes #123". -->

## Related decisions

<!-- If this touches an architectural decision, link the relevant ADR. If it introduces a NEW
     technology or architectural decision, this PR must add or update an ADR (architecture §8). -->

- ADR(s):
- Introduces a new tech/architectural decision? **No / Yes** (if Yes, ADR added: #…)

## How it was verified

<!-- Detection/tracking behaviour must be confirmed on-device — Play-over-Link is unreliable. -->

- [ ] Builds in Unity 6
- [ ] Off-device unit tests pass (resolver / classifier / decoder where applicable)
- [ ] Verified on-device (Quest 3 / 3S) — required for detection, rendering, or teardown changes
- [ ] Not applicable (docs-only change)

Notes on verification:

## Checklist

- [ ] Branch is off `main` and named `feat/…`, `fix/…`, `docs/…`, or `chore/…`
- [ ] No secrets, tokens, or private URLs in the diff or logs
- [ ] Untrusted-input handling stays behind the content resolver (no ad-hoc `https` GETs)
- [ ] Docs updated if behaviour or setup changed
