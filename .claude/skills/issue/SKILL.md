---
name: issue
description: Start work on a QR Reader milestone/task issue by its M#-T# marker (e.g. "/issue m0 t7"), or the next issue after the last one completed (e.g. "/issue next", "do the next issue"). Resolves the marker to the GitHub issue, then runs the full project workflow — branch off dev, implement per the ADRs, PR into dev with `Closes #n`. Use whenever the user names an issue as "M<x> T<y>", "M<x>-T<y>", asks for "next"/"the next issue", or similar.
---

# Work an issue by M#-T# marker

The user invokes this with a milestone/task marker instead of a full prompt, e.g.
`/issue m0 t7`, `/issue M2-T7`, `/issue 0 7`. Turn that into a resolved GitHub issue
and drive it through the standard project workflow.

## 1. Normalize the marker

Accept any of: `m0 t7`, `M0-T7`, `m0t7`, `0 7`. Normalize to the canonical form
`M<x>-T<y>` (uppercase, hyphen). Issue titles in this repo are `M<x>-T<y>: <summary>`
(e.g. `M2-T7: EditMode tests for content classifier + resolver guards`).

If the user asks for the **"next" issue** (e.g. `/issue next`, "do the next issue",
"next"), do **not** ask which one — resolve it automatically per section 1b.

If no marker is given and the user did **not** ask for "next", ask which issue.

## 1b. Resolve "next" = last completed + 1 (from git history)

"Next" means the first not-yet-done task after the last one that was actually completed.
Because merging a PR into `dev` does **not** auto-close its issue in this repo (issues are
closed manually), the open/closed state is **not** a reliable signal — use **git history on
`dev`** as the source of truth for what's done.

1. **Find the highest completed marker** by scanning `dev` commit **subjects only** — the marker
   of work actually done lives in the subject (`feat(m0-t1): …`) and in merge-commit subjects
   (`Merge pull request #61 from …/feat/m0-t2-…`). **Do NOT scan commit bodies (`%b`)** — bodies
   contain prose that references *other* markers as forward-looking notes (e.g. "…lands in M2-T7"),
   which would falsely register as completed:

   ```powershell
   $doneKey = 0; $doneLabel = "none"
   $log = git log dev --pretty="%s"   # subjects only — never %b
   foreach ($m in [regex]::Matches(($log -join "`n"), '(?i)M(\d+)-T(\d+)')) {
     $k = [int]$m.Groups[1].Value * 1000 + [int]$m.Groups[2].Value
     if ($k -gt $doneKey) { $doneKey = $k; $doneLabel = "M$($m.Groups[1].Value)-T$($m.Groups[2].Value)" }
   }
   # $doneKey = milestone*1000 + task of the most recently completed issue
   ```

2. **Pick the smallest OPEN issue after it.** List open issues, extract each marker, and choose
   the lowest `M*1000+T` that is greater than `$doneKey`. This naturally handles rolling into the
   next milestone when a milestone's tasks are exhausted, and skips any gaps:

   ```powershell
   $gh = "$env:ProgramFiles\GitHub CLI\gh.exe"
   $cand = & $gh issue list --state open --limit 200 --json number,title,labels |
     ConvertFrom-Json | ForEach-Object {
       if ($_.title -match '(?i)M(\d+)-T(\d+)') {
         $_ | Add-Member NoteProperty Key ([int]$Matches[1]*1000 + [int]$Matches[2]) -PassThru
       }
     } | Where-Object { $_.Key -gt $doneKey } | Sort-Object Key | Select-Object -First 1
   ```

3. If **no** open issue is greater than `$doneKey`, report that there's nothing left after the
   last completed issue (all done, or the next one isn't filed/open yet) and stop.

4. If a candidate **is** found, **confirm it before doing any work.** Because the target was
   auto-resolved (not named by the user), call `AskUserQuestion` with the resolved issue so the
   user confirms with a button — do not branch or write anything until they do:

   - Question: `Work this issue next?`
   - Show the resolution in the question text: `Last completed: <doneLabel>. Next open issue:
     #<n> — M<x>-T<y>: <title>.`
   - Options:
     - `Yes — start M<x>-T<y>` → proceed to section 3.
     - `No — pick a different one` → ask the user which marker to work instead, then resolve
       that via section 2.

   Only after an affirmative confirmation, continue with the normal flow from section 3. (When
   the user named an explicit marker instead of "next," no confirmation is needed — skip this
   step and go straight to section 3.)

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
