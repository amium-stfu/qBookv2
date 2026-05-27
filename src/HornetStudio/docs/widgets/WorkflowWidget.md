# Workflow Widget

## Type

`WorkflowWidget`

## Purpose

Discovers folder-local workflow YAML files below `Scripts/Workflows/`, validates their schema, and lets users create, edit, and delete workflow files from one compact widget list inside the page.

## Typical Use Cases

- Show which workflows are available in one folder
- Create new workflow YAML files from the widget UI
- Edit flat workflow steps without opening the filesystem manually
- Delete obsolete workflow YAML files with confirmation
- Validate workflow YAML files directly from the widget surface
- Surface invalid workflow files with a clear error state
- Keep workflow content outside `Folder.yaml`

## Key Configuration

- Workflow files live below `Scripts/Workflows/` next to the current folder `Folder.yaml`
- The widget itself stays small and UI-focused inside `Folder.yaml`
- Supported workflow YAML root fields are `name` and `steps`
- Supported declarative step types are `Log`, `SetValue`, `Delay`, `IfThenElse`, `While`, and `StopFunction`
- Widget header actions are `Add` and `Refresh`
- Each discovered workflow row exposes `Edit` and `Delete`
- `Log` requires `targetLog`, optional `level`, and `text`
- `SetValue` requires `target` and either a literal `value` or a `valueFrom` source item path; when `valueFrom` is configured the current value of that item is read at runtime and written to the target
- `Delay` requires `milliseconds`
- `IfThenElse` requires `condition` and `then`; `else` is optional and `variables` can define step-local condition sources
- `While` requires `condition` and `steps`; `variables` are optional and each `While` body must contain at least one positive `Delay` step as a loop guard
- `StopFunction` ends the current workflow execution with `Done`
- The editor can add, reorder, and remove `Log`, `SetValue`, `Delay`, `IfThenElse`, `While`, and `StopFunction` steps
- `IfThenElse` and `While` use the shared boolean condition editor with variable rows, source picking, token buttons, and live validation

## Editor Notes

- `Add` opens a workflow editor dialog instead of creating an empty file immediately
- New workflow file names are validated as `snake_case` and saved as `.yaml`
- Editing saves back to the selected file
- Deleting removes only the selected workflow file below `Scripts/Workflows/`

## Runtime Notes

The current widget implementation discovers and validates workflow files, shows their ready or invalid state, and exposes nested declarative step editing including editable `IfThenElse` and `While` branches. `While` bodies require at least one positive `Delay` guard to avoid busy loops, and `StopFunction` completes the active workflow as `Done` even when triggered inside nested control-flow blocks. Workflow execution foundations are implemented in code, but full start and stop interactions are not yet exposed on the widget surface.

## Source

- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowControl.axaml`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowControl.axaml.cs`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowDefinitionCodec.cs`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowExecutor.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/WorkflowWidget.help.md`