# Implementation Plan: Adaptive Orchestration (Deferred)

Status: **Deferred until after the project-first MVP**

This document remains a follow-up plan, not an active implementation entry point.

It depends on the MVP described in:

- `doc/specs/implementation_plan.md`
- `doc/specs/codealta_adaptive_orchestration_architecture.md`
- `doc/specs/filesystem_metadata_catalog_spec.md`

## 1. Why it is deferred

Adaptive orchestration is valuable, but it should not drive the first product slice.

The MVP first needs:

- automatic project discovery
- durable global/project threads
- clear host-owned orchestration
- reliable restoration

Only after those pieces are solid should CodeAlta add:

- proactive suggestions
- learned habits
- background work proposals
- richer cross-project continuation logic

## 2. Deferred focus areas

When resumed, this plan should focus on:

- project and thread recency tracking
- durable focus memory
- proactive continuation suggestions
- background review or validation suggestions
- learned tags and classifications
- smarter cross-project coordination from global threads

## 3. Rule for now

Do not let adaptive behavior complicate the project-first MVP.
