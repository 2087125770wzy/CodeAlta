# Prompt and Instruction Authoring

CodeAlta prompt resources live under `prompts/` roots and are selected by convention.

## Roots and precedence

Resources are discovered from:

1. Built-in `content/prompts/`.
2. User-global `~/.alta/prompts/`.
3. Project-local `<project>/.alta/prompts/`.

Later roots override earlier roots when they contain the same file id.

```text
prompts/
  system/
    default.system-prompt.md
    my-custom-system.system-prompt.md
  agents/
    default.prompt.md
    reviewer.prompt.md
```

## System prompts

System prompts define host-level behavior and use the suffix `.system-prompt.md`:

```markdown
---
description: Team default system prompt.
version: 1
---
You are CodeAlta, helping this team complete software tasks safely and
efficiently.
```

## Agent prompts

Agent prompts are selectable session profiles and use the suffix `.prompt.md`. The `name` frontmatter field is required for UI display. `description` is optional. `system` is optional and defaults to `default`.

```markdown
---
name: Team Reviewer
system: team-default
description: Review-oriented project prompt.
---
Review the requested changes for correctness, regressions, test coverage,
and actionable risks before summarizing.
```

The file name supplies the prompt id. For example, `team-reviewer.prompt.md` creates the prompt id `team-reviewer`. A global or project file with the same id overrides the lower-precedence prompt.

## Composition overrides

Prompt composition overrides live in agent prompt frontmatter. Omit these fields to use CodeAlta defaults; add them only when a workflow intentionally needs to include or exclude generated context sections.

```markdown
---
name: Minimal Reviewer
description: Review-oriented project prompt with smaller generated context.
system: team-default
skills: false
project_context: true
runtime_context: true
tool_guidance: false
---
Review the requested changes for correctness, regressions, test coverage,
and actionable risks before summarizing.
```

Supported composition fields are `skills`, `project_context`, `runtime_context`, and `tool_guidance`. Each value must be `true` or `false`.
