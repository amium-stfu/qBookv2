# Workflow Widget Help

## Widget Type

`WorkflowWidget`

## Overview

The Workflow widget looks for YAML workflow definitions below the current folder-local `Scripts/Workflows/` directory and renders a compact list of discovered files. Each row shows the workflow display name, source file, and a ready or invalid state, and now also exposes `Edit` and `Delete` actions.

## Properties

### Name / Path / FolderName

These identity values belong to the widget itself inside `Folder.yaml`. They do not contain the workflow steps.

### Workflow File Location

Workflow definitions are stored as separate YAML files below:

`Scripts/Workflows/<workflow-name>.yaml`

The widget resolves that location relative to the folder that contains `Folder.yaml`.

## Widget Actions

### Add

- Opens the workflow editor dialog
- Validates the workflow display name and file name before save
- Creates one YAML file only after the dialog is saved successfully

### Edit

- Loads the selected workflow file into the workflow editor dialog
- Saves changes back to the same YAML file
- Supports editing nested `IfThenElse` and `While` steps including condition variables and branch/body step lists

### Delete

- Opens a confirmation dialog before removing the selected YAML file
- Refreshes only the current workflow list after deletion

## YAML Schema

### Root fields

- `name`: required display name of the workflow
- `steps`: required ordered step list

### Supported step types

#### Log

- `type: Log`
- `targetLog`: required explicit log target
- `level`: optional `Debug`, `Info`, `Warning`, `Error`, or `Fatal`
- `text`: required log message

#### SetValue

- `type: SetValue`
- `target`: required target item path
- `value`: optional literal scalar value written to the target
- `valueFrom`: optional source item path; when configured, the current value of that item is read at runtime and written to the target instead of `value`
- When both `value` and `valueFrom` are absent, an empty string is written

#### Delay

- `type: Delay`
- `milliseconds`: required non-negative integer

#### IfThenElse

- `type: IfThenElse`
- `condition`: required boolean expression
- `variables`: optional list of step-local condition variables with `name` and `sourcePath`
- `then`: required step sequence
- `else`: optional step sequence

#### While

- `type: While`
- `condition`: required boolean expression
- `variables`: optional list of step-local condition variables with `name` and `sourcePath`
- `steps`: required loop body sequence
- The loop body must contain at least one positive `Delay` step as a guard against busy loops

#### StopFunction

- `type: StopFunction`
- Ends the current workflow execution with the final state `Done`

## Editor Scope

- The editor can add, remove, and reorder `Log`, `SetValue`, `Delay`, `IfThenElse`, `While`, and `StopFunction` steps.
- The editor uses target browsing for `SetValue.target` and source item browsing for `SetValue.valueFrom`.
- The editor uses discovered process log targets for `Log.targetLog`.
- `IfThenElse` and `While` use the shared boolean condition editor with variable rows, source picking, token buttons, and live validation.
- Each `IfThenElse` branch can contain nested declarative steps.
- Each `While` body can contain nested declarative steps.
- New `While` rows start with a default `Delay` of `100` ms. The required positive delay may be moved within the loop body, but validation rejects a `While` body without any positive `Delay` guard.

## Current Widget Behavior

- Discovers `.yaml` and `.yml` files below `Scripts/Workflows`
- Parses and validates each file using the workflow YAML codec
- Shows `Idle` for valid files
- Shows `Invalid` plus the first validation error for invalid files
- Opens a dense workflow editor dialog for `Add` and `Edit`
- Saves new workflows only after dialog validation succeeds
- Deletes selected workflow files only after confirmation
- Updates the widget footer with the number of discovered workflow files
- Refreshes discovery manually through the widget `Refresh` button

## Execution Foundation

The repository already contains a workflow executor with sequential step handling, cancellation, condition branching, loops, and clear `Done`, `Failed`, and `Canceled` outcomes. `While` conditions are evaluated before every iteration, each loop body must contain a positive `Delay` guard, and `StopFunction` exits the active workflow as `Done` even from nested control-flow blocks. The current widget surface does not yet expose start or stop actions, so execution remains an internal foundation for the next implementation step.

## Example YAML

```yaml
name: startup_sequence
steps:
  - type: Log
    targetLog: Logs.process
    level: Info
    text: Starting sequence
  - type: SetValue
    target: studio.main.pump.enable
    value: true
  - type: IfThenElse
    condition: "{Temperature} > 20"
    variables:
      - name: Temperature
        sourcePath: custom_signals_1.temperature
    then:
      - type: Delay
        milliseconds: 500
      - type: Log
        targetLog: Logs.audit
        text: Warm start completed
    else:
      - type: Log
        targetLog: Logs.audit
        text: Warm start skipped
  - type: While
    condition: "{Enabled} == true"
    variables:
      - name: Enabled
        sourcePath: custom_signals_1.enabled
    steps:
      - type: Delay
        milliseconds: 100
      - type: StopFunction
```

## Help Notes for Users

- Keep workflow files small and declarative.
- Use step-local `variables` on `IfThenElse` when the condition should read runtime sources directly.
- Use step-local `variables` on `While` when the loop condition should read runtime sources directly.
- Use explicit `targetLog` values for every `Log` step.
- Keep one positive `Delay` step inside every `While` body to avoid hot loops.
- Keep conditions boolean and focused.
- Store workflow content only in YAML files under `Scripts/Workflows`, not inside `Folder.yaml`.

## Suggested Help Window Metadata

- Summary file: `src/HornetStudio/docs/widgets/WorkflowWidget.md`
- Help file: `src/HornetStudio/docs/widgets/help/WorkflowWidget.help.md`

## Source

- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowControl.axaml`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowControl.axaml.cs`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowDefinitionCodec.cs`
- `src/HornetStudio.Editor/Widgets/Workflow/WorkflowExecutor.cs`