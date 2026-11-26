---
description: Commits, branches, PRs and hooks.
---

# Git workflow

- **Commit, push or open PRs only when the user asks.**
- **Conventional commits:** `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, `perf:`, `build:`, `ci:`. Subject in imperative mood, ≤ 72 chars; body explains **why**, not what.
- **Atomic commits:** one logical change per commit; tests go in the same commit as the code they cover. No unrelated changes bundled.
- **Branches:** `feature/…`, `fix/…`, `refactor/…`, `chore/…`. Never commit directly to `main`/`master` when the project uses PRs.
- **Never force-push to `main`/`master`.** On feature branches use `--force-with-lease`, never plain `--force`.
- **Never skip hooks** (`--no-verify`). If a hook fails, fix the cause.
- **Accept formatter output** from format hooks / `dotnet format`; don't hand-revert it.
- **Before a PR:** build, test, format check and a review of your own diff (see `00-core.md`). Keep PRs to one concern.
- **Never commit** secrets, `bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.*.json` with real credentials, or local tool settings (`.claude/settings.local.json`).
