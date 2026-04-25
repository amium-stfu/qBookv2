# CustomSignals Widget

## Type

`CustomSignals`

## Purpose

Provides a low-code way to define project-local signals without requiring Python.

## Typical Use Cases

- User input values
- Constant project values
- Simple computed values derived from registry targets

## Key Configuration

- Signal definitions stored in `Properties.CustomSignals`
- Signal mode such as input, constant, or computed
- Trigger mode for computed signals
- Formula variables and operations

## Runtime Notes

Persisted target paths use dot notation and are stored relative to the current folder when possible.

## Source

- `UiEditor/Widgets/CustomSignals/`
- `UiEditor/Widgets/CustomSignals/CustomSignalsControl.axaml.cs`
- `UiEditor/Widgets/CustomSignals/CustomSignalEditorDialogWindow.axaml`

## Help

- Detailed help: `AutomationExplorer/docs/widgets/help/CustomSignals.help.md`