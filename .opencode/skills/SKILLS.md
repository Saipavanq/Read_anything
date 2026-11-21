# Installed engineering skills

These Agent Skills were downloaded from public GitHub repositories on 2026-11-21 and installed here so OpenCode can load them on demand. Selection was based on GitHub star count (a proxy for popularity, not a security review) and relevance to building and maintaining this Python desktop application.

## Sources

| Repository | Stars at selection | Licence | Skills taken |
|---|---:|---|---|
| [mattpocock/skills](https://github.com/mattpocock/skills) | ~328,900 | MIT | 3 |
| [obra/superpowers](https://github.com/obra/superpowers) | ~322,800 | MIT | 8 |
| [anthropics/skills](https://github.com/anthropics/skills) | ~198,100 | No repo-level licence; per-skill terms vary | 1 |
| [addyosmani/agent-skills](https://github.com/addyosmani/agent-skills) | ~119,000 | MIT | 5 |

The repositories were cloned with `git clone --depth 1` and the selected skill directories were copied verbatim into this folder. Star counts came from the GitHub API via public leaderboards on 2026-11-21.

## Installed skills

### Process and workflow (superpowers)
- **using-superpowers** — establishes when and how to invoke skills.
- **brainstorming** — understand what is actually wanted before creative work.
- **writing-plans** — turn a design into bite-sized, testable tasks.
- **executing-plans** — implement a plan task by task with a durable ledger.
- **test-driven-development** — red/green/refactor; no production code without a failing test.
- **systematic-debugging** — find root causes before proposing fixes.
- **verification-before-completion** — run the commands and read the output before claiming success.
- **requesting-code-review** — review work against requirements before calling it done.

### Engineering quality (Matt Pocock)
- **codebase-design** — deep-module vocabulary for designing interfaces.
- **code-review** — review changes against standards and spec.
- **diagnosing-bugs** — diagnosis loop for hard bugs and regressions.

### Production quality (Addy Osmani)
- **code-simplification** — reduce complexity without changing behaviour.
- **security-and-hardening** — harden input handling and integrations.
- **observability-and-instrumentation** — make production behaviour diagnosable.
- **documentation-and-adrs** — record decisions and detect documentation drift.
- **frontend-ui-engineering** — production-quality, accessible interfaces.

### Authoring (Anthropic)
- **skill-creator** — create, improve and evaluate skills.

## Notes

- A skill can bundle scripts that an agent may run. These were inspected at installation; no unexpected executable hooks were found in the selected set.
- Licences are attributed above. `anthropics/skills` has no repository-level licence file; check the individual skill directory before redistributing that skill.
- To update, re-clone the source repositories and replace only the directories listed here.
