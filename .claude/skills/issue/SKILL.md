---
name: issue
description: Start work on a QR Reader milestone/task issue by its M#-T# marker (e.g. "/issue m0 t7"). Resolves the marker to the GitHub issue, then runs the full project workflow — branch off dev, implement per the ADRs, PR into dev with `Closes #n`. Use whenever the user names an issue as "M<x> T<y>", "M<x>-T<y>", or similar.
---

# Work an issue by M#-T# marker

The user invokes this with a milestone/task marker instead of a full prompt, e.g.
`/issue m0 t7`, `/issue M2-T7`, `/issue 0 7`. Turn that into a resolved GitHub issue
and drive it through the standard project workflow.

## 1. Normalize the marker

Accept any of: `m0 t7`, `M0-T7`, `m0t7`, `0 7`. Normalize to the canonical form
`M<x>-T<y>` (uppercase, hyphen). Issue titles in this repo are `M<x>-T<y>: <summary>`
(e.g. `M2-T7: EditMode tests for content classifier + resolver guards`).

If no marker is given, ask which issue.

## 2. Resolve the marker to a GitHub issue

`gh` is installed but not on PATH — always call it by full path:
`"$env:ProgramFiles\GitHub CLI\gh.exe"`.

Find the open issue whose title starts with the normalized marker:

```powershell
$gh = "$env:ProgramFiles\GitHub CLI\gh.exe"
& $gh issue list --state open --limit 100 --json number,title,labels |
  ConvertFrom-Json |
  Where-Object { $_.title -match '^M0-T7\b' }   # <- substitute the normalized marker
```

- **Exactly one match** → that's the issue. Read its body with
  `& $gh issue view <n> --json number,title,body,labels`.
- **No match** → report it; list nearby open issues in that milestone so the user can pick.
- **Multiple matches** → show them and ask which one.

## 3. Priority-order guard

Before starting, honor the user's standing rule: work high → medium → low priority, and
never open a lower-priority issue while a higher-priority one is still open. Check the
matched issue's labels against other open issues. If a higher-priority issue is open,
surface it and ask before proceeding.

## 4. Run the standard workflow

Follow the git workflow in [CLAUDE.md](../../CLAUDE.md) exactly — do not restate or
override it. In short:

1. Branch **off `dev`** with a descriptive name derived from the marker + slug,
   e.g. `feat/m0-t7-build-deploy-doc` (pick `feat`/`fix`/`docs`/`chore` from the issue).
2. Read the relevant [`docs/`](../../docs/) — project context, architecture, ADRs,
   implementation plan — before proposing changes. Do not introduce a new technology or
   architectural decision without a new/updated ADR (architecture.md §8).
3. Implement the task described in the issue body.
4. Commit with a clear message and the `Co-Authored-By` trailer.
5. Push the branch and open a **PR targeting `dev`** whose body includes `Closes #<n>`
   and the Claude Code footer.
6. **Leave merging to the user.** Never push to `dev` or `main`.

## 5. Report

Tell the user: which issue was resolved (`#<n> — <title>`), the branch created, what was
implemented, and the PR link.
