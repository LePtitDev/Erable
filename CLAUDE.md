# CLAUDE.md — Erable

Erable is a structured, "IDE-style" wiki: extended Markdown pages, categories with inheritance, typed and validated properties, typed links, attachments. It runs as a **local desktop application** (Photino + in-process backend) and in multi-user **server mode** (ASP.NET Core + React SPA).

This file is for AI agents working on the project. It **takes precedence** over the agent's default habits. Everything that also applies to human contributors — reference documents, architecture invariants, conventions, definition of "done", commands — is in [CONTRIBUTING.md](CONTRIBUTING.md): read it first and follow it.

---

## 1. Sources of truth

The documents of `docs/` are **validated** (see the table in CONTRIBUTING.md). Never depart from them silently: any departure goes through a proposal to the maintainer (§4), then through an update of the document concerned once the decision is made.

Fonts of the design: **IBM Plex Sans** (UI), **IBM Plex Mono** (IDs, code), **Source Serif 4** (page body). Colors and spacing are design tokens in `src/web/src/shared/theme/tokens.css`.

## 2. Languages

Everything written to the repository is in **English** (code, comments, docs, commit messages), except the content of the reference project "Chroniques d'Aube", which is French. UI texts go through i18n only; `fr` is the reference locale. Conversations with the maintainer may be in French.

## 3. Git: reserved to the maintainer

**The agent performs no Git operation that changes the state of the repository** unless the maintainer explicitly asks for it. Forbidden by default: `commit`, `branch`, `checkout`/`switch`, `merge`, `rebase`, `reset`, `revert`, `stash`, `tag`, `push`, `pull`, `fetch`, `cherry-pick`, `add`, `rm`, `mv`, `restore`, `clean`, changes to `.git/` or to the Git configuration, creating a worktree.

Allowed (read-only): `git status`, `git diff`, `git log`, `git show`, `git blame`.

At the end of a task, the agent **proposes** a commit message (English, Conventional Commits, e.g. `feat(core): add ElementId parsing and formatting`) and the list of files concerned. The maintainer commits.

Files never to move or delete without agreement: `docs/`, `LICENSE`, this file.

## 4. Working on a task

Work comes from a GitHub issue or from the maintainer's request.

1. **Read** the request, then the sections of `docs/` it touches.
2. **Challenge** (§5): if something is wrong in the request or in the documents, say so **before** coding.
3. **Implement** in small increments, tests included (definition of "done" in CONTRIBUTING.md). For a bug, write the failing test first.
4. **Verify**: build without warnings, tests green, lint. Never declare "done" without running the tests; if a test fails or could not be run, say so explicitly.
5. **Report**: summary, files changed, verification commands, proposed commit message, possible improvement proposals.

A task too large for one session is split: propose the split to the maintainer first.

## 5. Critical thinking and proposals — expected, not optional

The agent is not a mere executor. It is expected to **challenge** technical or design choices that seem ill-suited, and to **propose** improvements.

**When**
- a choice in the documents or in the request is risky, expensive, outdated, or inconsistent with another document;
- a planned library is abandoned, has a clearly better equivalent, or raises a license issue;
- a business rule has an unhandled edge case;
- a testing requirement costs more than it protects at this stage;
- a better UX, a simplification or a reduction of complexity is possible.

**How**
- Raise it **before** implementing, in a structured and brief way:
  > **Point to challenge** — *finding* · *concrete impact* · *options (with cost)* · *recommendation*
- For a blocking or structural point: **stop and ask**. For a minor, local point: choose the most cautious option, move on, and mention it in the report.
- **Never** depart silently from a validated document. Once a decision is made, propose the update of the document (a new `Q<n>` decision in §A-13 if it is structural).
- Ideas outside the scope of the task are proposed to the maintainer (they may become issues); they are not implemented without agreement.
- Be factual: check (official documentation, version, quick test) rather than asserting from memory. Say "I don't know" when that is the case.
