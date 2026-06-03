# Agent prompt discoverability

- Status: Completed
- Plan file: `.alta/plans/2026-06-03-agent-prompt-discoverability.md`
- Created: 2026-06-03
- Task: Add compact prompt/mode discoverability to composed instructions and make built-in/custom prompt descriptions decision-useful.
- Git: `.alta/` is not ignored in this checkout; commit this plan with the related implementation work, but preserve unrelated `.alta/` state.

## Objective
- Add a compact generated section to composed developer instructions that lists effective agent prompt profiles, marks the current prompt, and tells agents how to switch or use a one-shot prompt id.
- Improve built-in `default`/`plan` prompt descriptions and document that custom prompt descriptions are surfaced in model context.
- Non-goals: rework prompt selection propagation already fixed in the previous commits; add provider-specific prompt/skill behavior; change queued-prompt switching behavior beyond regression coverage; expose prompt bodies, file paths, hashes, or large prompt metadata in model context.

## Context and evidence
- `SystemPromptBuilder.Build` currently composes agent prompt, runtime context, tool guidance, available/active skills, and project context in `src/CodeAlta.Orchestration/Runtime/SystemPrompts/SystemPromptBuilder.cs`.
- `SystemPromptBuildRequest` already carries session, project roots, user/global roots, selected prompt name, and prompt part options needed to discover effective prompts.
- `AgentPromptCatalog.ListEffectivePrompts` exists in `src/CodeAlta.Orchestration/Runtime/SystemPrompts/AgentPromptCatalog.cs` and applies built-in < user-global < project precedence while removing shadowed prompts.
- Existing tests cover prompt catalog precedence and selected prompt body/system selection in `src/CodeAlta.Orchestration.Tests/SystemPromptInfrastructureTests.cs`.
- Existing built-in prompt content tests live in `src/CodeAlta.Tests/BuiltInPromptContentTests.cs`.
- Current built-in descriptions are terse: `default.prompt.md` says normal project session; `plan.prompt.md` says read-only planning workflow.
- Docs describe prompt roots/selection in `doc/runtime.md`, `site/docs/prompts.md`, and `site/docs/workspace.md`; they do not yet warn that agent prompt descriptions are shown to the model.
- Current git state before this plan only had unrelated untracked `.alta/`; `.gitignore` has no `.alta` ignore rule.
- Prompt-switching fixes now exist in recent commits: `2e132087` for direct prompt switching, `f0ec205c` for queued prompt drains after `set_agent`, and `f3089a77` for uniform provider skill handling.

## Assumptions and open decisions
- Assumption: prompt propagation for UI prompt bar, `alta session set_agent --prompt-id ...`, and queued drains after prompt switching is fixed by recent commits; the builder should still run the existing focused regression tests to guard against regressions.
- Decision: add discoverability as a generated developer-instruction part controlled by the existing `ToolGuidance` prompt part option, because the section is operational live-tool guidance and avoids expanding template schema.
- Decision: list effective prompts only; do not list shadowed prompts in model context.
- Decision: show only compact safe fields: prompt id, display name, source label, system prompt id, current marker, and one-line description. Do not include body, path, content hash, or shadowed-by path.
- Decision: render prompt profiles as a compact multiline Markdown list, with each profile on its own bullet and source/system/description on nested list lines for scanability.
- Open decision if implementation evidence suggests severe context bloat: whether to cap very large prompt lists and summarize omitted count. Prefer no cap unless tests/evidence show a problem.

## Design notes
- In `SystemPromptBuilder.Build`, after resolving the final agent prompt/template fallback, build a prompt guidance string using `AgentPromptCatalog.ListEffectivePrompts` with the same content locator and roots used by prompt composition.
- Add the prompt guidance near existing tool guidance, likely as generated manifest part key `prompt.discovery`, id `agent_prompts`, title `Agent Prompts`, and an order immediately after `tool.guidance` and before skills.
- Create a private helper such as `BuildAgentPromptGuidance(SystemPromptBuildRequest request, string? projectRoot, string currentPromptName)`.
- Construct `AgentPromptCatalogQuery` from the existing request/root data: user profile root, user CodeAlta root, project root, and trusted project prompt resources when a project root is present.
- Format the section for scanability, for example: "Agent prompt profiles available for this session" followed by a compact multiline Markdown list where each profile is a top-level bullet (with the current marker on the selected profile) and source/system/description are nested list items, plus switch commands `alta session set_agent --prompt-id <id>` and `alta session send <session-id> --prompt-id <id> --stdin`.
- Normalize descriptions to a single trimmed line and escape/control troublesome Markdown characters enough to avoid malformed guidance; keep descriptions concise rather than perfectly preserving frontmatter formatting.
- If prompt discovery fails unexpectedly, avoid breaking prompt composition unless existing root validation would already fail; prefer diagnostics/warnings only if there is a clear existing pattern.
- Update built-in prompt frontmatter descriptions only, not the bodies, unless tests require small wording alignment.
- Update docs to say custom agent prompt descriptions should be concise and decision-useful because they are included in the generated agent prompt discoverability section.

## Risks and challenges
- Discoverability may increase prompt tokens if many custom prompts exist; compact formatting and one-line descriptions mitigate this.
- The final current prompt can fall back to `default` if the requested prompt is missing; the current marker must use `bundle.Manifest.Template.InstructionName`/post-fallback template value, not the raw requested id.
- The section must not expose paths/hashes/custom prompt bodies, which may be sensitive.
- Source precedence tests need temporary built-in/global/project roots that mirror `FileSystemPromptContentLocator` layout.
- If `ToolGuidance` is disabled by a prompt template, prompt-switch guidance will also be disabled by design; document this if surprising.

## Implementation checklist
- [x] In `SystemPromptBuilder.Build`, add a generated `Agent Prompts` developer part after tool guidance and before skills, gated by `template.PartOptions.ToolGuidance` and non-empty prompt discovery output.
- [x] Add private helper(s) in `SystemPromptBuilder` to query `AgentPromptCatalog.ListEffectivePrompts` with the same built-in/global/project roots and format compact multiline Markdown prompt profile lists.
- [x] Ensure the current marker uses the final selected/fallback agent prompt id after `ResolveResource` fallback logic.
- [x] Keep the generated section free of prompt bodies, source paths, hashes, and shadowed prompt details.
- [x] Update `src/CodeAlta.Orchestration/content/prompts/agents/default.prompt.md` description to communicate normal implementation/build mode and plan-file execution.
- [x] Update `src/CodeAlta.Orchestration/content/prompts/agents/plan.prompt.md` description to communicate read-only planning, `.alta/plans/` output, and Default handoff.
- [x] Update prompt docs in `doc/runtime.md` and `site/docs/prompts.md` to explain that custom prompt descriptions should be concise because they appear in composed model context.
- [x] If local docs mention the prompt selector/agent prompts elsewhere (`site/docs/workspace.md`), add a short cross-reference only if needed; avoid broad doc churn.
- [x] Update or add `SystemPromptInfrastructureTests` covering generated prompt discoverability with built-in/global/project prompts, shadowing precedence, current marker, and absence of bodies/paths/hashes.
- [x] Update `BuiltInPromptContentTests` assertions for the improved built-in descriptions.
- [x] Re-run existing prompt propagation regression tests from `AltaLiveToolTests` to confirm this work does not regress `session send --prompt-id`, `session set_agent`, or queued prompt drains after agent switching.
- [x] Self-review the diff for context bloat, sensitive metadata exposure, provider-specific branching, and docs/test alignment.

## Verification checklist
- [x] `dotnet test CodeAlta.Orchestration.Tests\CodeAlta.Orchestration.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SystemPromptInfrastructureTests"`
- [x] `dotnet test CodeAlta.Tests\CodeAlta.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BuiltInPromptContentTests|FullyQualifiedName~AltaLiveToolTests.SessionSend_PromptIdSelectsAgentPromptForThatSend|FullyQualifiedName~AltaLiveToolTests.SessionSetAgent_RefreshesActiveSessionPromptForSubsequentSends|FullyQualifiedName~AltaLiveToolTests.SessionSetAgent_InjectsAgentPromptAndAvailableSkillsForCodexSession|FullyQualifiedName~AltaLiveToolTests.SessionSend_QueuedPromptAfterSetAgentRefreshesInstructionsBeforeDrain"`
- [x] `dotnet test CodeAlta.Orchestration.Tests\CodeAlta.Orchestration.Tests.csproj -c Release --no-restore`
- [x] `dotnet test CodeAlta.Tests\CodeAlta.Tests.csproj -c Release --no-restore`
- [x] `dotnet test -c Release --no-restore` from `src` if time permits or if touched areas broaden beyond the focused projects.
- [x] `git diff --check`
- [x] `lunet build` from `site`
- [x] Manual/context review: inspect a composed `DeveloperInstructions` sample and confirm it contains compact prompt metadata/current marker/switch commands but no prompt bodies/paths/hashes.

## Handoff notes
- Start from the saved plan in this file; if approved, switch to Default/build mode before editing source/docs/tests.
- Keep unrelated untracked `.alta/` content out of staging unless it is this plan file or explicitly requested.
- This plan intentionally focuses on remaining discoverability/description work; do not revisit provider-specific skill handling or already-fixed prompt propagation except through regression tests.
