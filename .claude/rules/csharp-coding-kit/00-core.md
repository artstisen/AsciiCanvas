---
description: Precedence, target-platform policy and working workflow for all .NET/C# work.
---

# .NET principles and workflow

## Precedence

1. Explicit instructions from the user in the conversation.
2. The project's own `CLAUDE.md`, `.editorconfig`, analyzers and **established patterns in the codebase**.
3. These rules.

- **DO** match existing conventions in brownfield code, even where these rules prefer something else. Mention the deviation once; never refactor unasked.
  Why: a consistent codebase beats a codebase that is half "modern" and half legacy.
- **DO** look at how the project already writes endpoints, handlers, tests and registrations before writing new ones.

## Target platform

- **DO** read `TargetFramework` / `LangVersion` (csproj, `Directory.Build.props`) before using version-specific APIs or syntax. No C# 14 features in a `net8.0` project.
- **DO** default new projects to the current LTS: **.NET 10 / C# 14** (`net10.0`). .NET 11 (Nov 2026) is STS — use only when asked.
- **DON'T** use .NET Framework-era APIs or patterns (`System.Web`, `WebClient`, `BinaryFormatter`, `Thread.Abort`, `AppDomain` tricks).
- **DO** keep `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`; fix nullable warnings instead of silencing them with `!`.

## Principles

- **Simplest thing that solves the actual problem.** No speculative abstractions, interfaces "for later", or generic frameworks. Three clear lines beat a premature abstraction.
- **Every non-obvious choice has a "why"** — in the PR/commit text or a short comment, not a wall of docs.
- **Ask before deciding things that are hard to undo** — architecture, public API shape, database schema, new third-party dependencies.

## Workflow

- **Plan first** for any task with 3+ steps or an architectural decision. If the implementation goes sideways, stop and re-plan instead of pushing through.
- **Bug fixes:** reproduce with a failing test when feasible, then fix, then keep the test.
- **Verify before claiming done** — evidence, not "it looks fine". Minimum bar: `dotnet build` with no new warnings and `dotnet test` green.
- **Challenge your own work** for non-trivial changes: "would a staff .NET engineer approve this?" Skip the ceremony for obvious one-liners.
- **Learn from corrections:** when the user corrects a pattern, propose adding it to the project's `CLAUDE.md` so it does not recur.

## Verification matrix

| Change | Run |
|---|---|
| Feature complete / before PR | build, test, format check, vulnerable-package scan, review own diff |
| Bug fix | build, test (incl. the new regression test) |
| Refactor | build, test, check for new analyzer warnings |
| Dependency update | build, test, vulnerable-package scan |
| Config / test-only | build, test |

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

When reviewing your own diff, look for: debug leftovers, commented-out code, stray `TODO`s, accidental file changes, secrets.

## Optional: Roslyn MCP

If the `cwm-roslyn-navigator` MCP server is available, prefer `find_symbol`, `find_references`, `find_implementations`, `get_public_api` and `get_project_graph` over reading whole files, and `get_diagnostics` / `detect_antipatterns` after edits. Why: focused results cost far fewer tokens than full-file reads.
