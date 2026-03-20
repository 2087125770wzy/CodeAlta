# CodeAlta App Architecture Improvements v2

Status: Implemented
Audience: `src/CodeAlta/` implementers
Scope: the current post-refactor terminal app architecture in `src/CodeAlta/`
References:
- `report_codealta_app_architecture.md`
- `codealta_app_architecture_improvements.md`
- `codealta_app_architecture_plan.md`

Implementation notes:
- Implemented on 2026-03-20.
- `ChatSelectorCoordinator` and `ThreadTabStripCoordinator` were reevaluated after constructor cleanup and intentionally kept in `Presentation`.
- `CodeAltaShellBridge` was reevaluated and retained because it still provides a small adapter seam between `CodeAltaShellController` and the app host without pushing controller-facing interface members back onto `CodeAltaApp`.

## 1. Purpose

The previous refactoring already achieved its main goal: `CodeAltaApp` is no longer the pre-refactor God class, and the app now has explicit coordinators, presenters, view models, and a UI-dispatch contract.

This v2 document is not a call for another broad architecture rewrite.

It is a smaller follow-up iteration based on the current code:

- keep the coordinator and presenter structure
- keep the current terminal UI approach
- reduce the remaining wiring burden in `CodeAltaApp`
- improve type-level clarity around shared app state and cross-coordinator collaboration
- avoid "cleanup for cleanup's sake"

## 2. Assessment of the Current Report

The report is broadly pertinent. Its core diagnosis matches the current code.

### 2.1 What the report gets right

The following observations are accurate and should stay central:

- `CodeAltaApp` still acts as the composition root and also as the relay between many collaborators.
- Several coordinators are still callback-heavy, especially `ThreadCommandCoordinator`, `ShellWorkspaceCoordinator`, `ChatSelectorCoordinator`, and `ThreadTabStripCoordinator`.
- `IUiDispatcher` and `UiDispatch` living under `CodeAlta.App` create avoidable upward references from Presentation code.
- `OpenThreadState` is not a pure model. It combines model state, presenter state, and a tab view model.
- `SidebarCoordinator` is functionally a coordinator even though it sits under `Views`.

The report is also correct that these are mostly wiring-level problems, not evidence that the refactor failed.

### 2.2 Where the report overreaches

The report is less convincing in a few areas.

#### Namespace purity is not the main problem

The report emphasizes namespace dependency inversions such as `Presentation -> App` and `Presentation -> Views`. Some of those are real, but in this terminal UI they are not equally important.

`ThreadTimelinePresenter`, `ToolCallPresenter`, and parts of the workspace/tab coordination are intentionally close to concrete UI controls. That is acceptable here. Chasing a perfectly downward namespace graph would add churn without necessarily making the code easier to change.

#### Moving coordinators between namespaces is a low-priority cleanup

Moving `ChatSelectorCoordinator` and `ThreadTabStripCoordinator` from `Presentation` to `App` might make the folder layout look cleaner, but it does not solve the real problem. Their pain comes from constructor shape and state access, not primarily from namespace placement.

#### Replacing callbacks with many tiny interfaces would be easy to overdo

The report suggests several small interfaces. The direction is good, but a blanket "introduce interfaces everywhere" response would risk replacing one kind of ceremony with another.

For the current codebase, typed internal context objects or a few narrow facades are likely a better default. Interfaces should be added where they provide a real seam, not as a rule.

### 2.3 What the report underplays

The main remaining architectural issue is not just "too many callbacks." It is that `CodeAltaApp` is still the only place that can see enough of the app at once:

- current selection and open-thread state
- workspace control handles
- shell/workspace refresh actions
- tab-opening and tab-closing operations
- backend preference application helpers

Because that knowledge is centralized in `CodeAltaApp`, other coordinators can only access it through long lists of lambdas. The callback-heavy constructors are a symptom of that missing shared access layer.

There is also a more concrete cross-layer leak than the report emphasizes: some Presentation code depends directly on `Views` for shared UI helpers and shell-specific constants. That is a more actionable concern than namespace purity in the abstract, because it shows where shared terminal UI primitives are still living in the wrong home.

## 3. Recommended v2 Direction

The next iteration should focus on introducing a small typed collaboration layer between `CodeAltaApp` and the high-traffic coordinators.

This should be done without changing the overall architecture:

- `CodeAltaApp` remains the composition root and lifecycle owner
- coordinators remain the main workflow units
- presenters continue to own imperative timeline rendering
- view models stay lightweight

The target is not a new architecture. The target is a cleaner version of the current one.

## 4. Proposed Improvements

### 4.1 Introduce typed app contexts instead of raw callback lists

This is the highest-value change.

Today, the largest coordinators receive many individual `Func<>` and `Action<>` parameters. That makes dependencies hard to read and makes `CodeAltaApp` carry a large relay surface.

v2 should replace the raw callback lists with a small number of typed internal collaborators, for example:

- a selection/thread access context
- a workspace controls access context
- a shell/workspace actions context
- a backend preference access context where needed

These do not all need to be interfaces. Internal sealed context/facade types are acceptable if they make constructor signatures and call sites clearer.

The goal is to let code say "I need thread selection access" rather than "I need eight unrelated lambdas."

#### Concrete impact

This should be applied first to:

- `ThreadCommandCoordinator`
- `ShellWorkspaceCoordinator`
- `ChatSelectorCoordinator`
- `ThreadTabStripCoordinator`

If done well, this should also remove a large portion of the one-line relay methods in `CodeAltaApp`.

### 4.2 Make `CodeAltaApp` depend on typed collaborators, not on relay methods

`CodeAltaApp` should still wire the app together, but it should stop exposing dozens of tiny private methods whose only job is forwarding.

After the context extraction in 4.1:

- coordinator-to-coordinator collaboration should go through typed collaborators
- state reads should go through typed accessors
- workspace actions should be grouped behind explicit methods

This keeps `CodeAltaApp` as a composition root instead of a hidden integration API.

### 4.3 Move UI dispatch abstractions out of `CodeAlta.App`

This remains a good low-risk cleanup.

`IUiDispatcher`, `UiDispatch`, and `TerminalUiDispatcher` should live in a shared namespace such as:

- `CodeAlta.Threading`, or
- `CodeAlta.Infrastructure.Threading`

This reduces needless upward references from Presentation code and better matches the actual role of these types.

### 4.4 Reclassify `OpenThreadState`

`OpenThreadState` currently mixes:

- the thread descriptor
- session state
- presenter ownership
- tab view-model state

That is probably acceptable as an aggregate, but it is misleading in `CodeAlta.Models`.

v2 should either:

- move it into an app-owned state namespace, or
- rename it to better signal that it is an open-thread UI aggregate rather than a pure domain model

This is a clarity improvement, not a behavior change.

### 4.5 Apply small namespace cleanup only where it helps ownership

Only two namespace moves look worthwhile in this iteration:

- move `SidebarCoordinator` out of `Views`
- move threading abstractions out of `App`

In addition, shared UI helpers or constants that are used by both `Views` and `Presentation` should be extracted out of `Views` when touched. That keeps the fix targeted at real coupling points instead of broad namespace churn.

Other moves should be deferred unless they fall out naturally from the context extraction work.

## 5. Explicit Non-Goals for v2

v2 should not attempt the following:

- another large refactor of the terminal UI structure
- a push toward strict MVVM purity
- a dependency injection container
- mass file shuffling just to improve namespace diagrams
- splitting large formatters unless they become an active maintenance problem
- removing `CodeAltaShellBridge` unless the other changes make it obviously redundant

## 6. Suggested Implementation Shape

The cleanest path is to add a small `App/Context` or `App/State` area and migrate the highest-pain coordinators one by one.

An example direction:

1. Add typed access objects that wrap current app-owned state and controls.
2. Update one coordinator at a time to use those objects.
3. Delete the now-unused relay methods from `CodeAltaApp`.
4. Move threading abstractions.
5. Reclassify `OpenThreadState`.

This keeps the refactor incremental and avoids destabilizing the working shell.

## 7. Success Criteria

The v2 iteration should be considered successful if it achieves most of the following:

- `CodeAltaApp` still reads as a composition root and lifecycle shell
- the largest coordinator constructors become materially shorter and clearer
- cross-coordinator dependencies become named and typed
- the number of one-line relay methods in `CodeAltaApp` drops significantly
- Presentation code no longer references threading abstractions through `CodeAlta.App`
- `OpenThreadState` no longer looks like a pure model type in the wrong namespace
- existing architecture guardrails and controller tests remain valid or are updated in a clearly better direction

## 8. Recommended Priority

Priority order for the next iteration:

1. Typed app contexts for the callback-heavy coordinators
2. Remove corresponding `CodeAltaApp` relay methods
3. Move threading abstractions out of `App`
4. Reclassify `OpenThreadState`
5. Minor namespace cleanup only where it improves ownership clarity

This keeps the next step focused on the highest-payoff architectural debt without reopening the whole app design.
