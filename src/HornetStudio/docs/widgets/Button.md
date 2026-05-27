# Button Widget

## Type

`Button`

## Purpose

Displays an interactive button that can execute configured actions, host commands, and interaction rules.

## Typical Use Cases

- Trigger commands
- Open dialogs or follow-up actions
- Provide a themed action button with text or icon

## Key Configuration

- Caption and footer text
- Button text and icon
- Button command
- Interaction rules including `RunFunction` for runnable registry functions, `OpenDialog(dialogWidgetId, origin = Screen, position = Center)`, and `CloseDialog(dialogWidgetId)` for `DialogWidget` overlays
- Theme-aware colors

## Runtime Notes

The button respects editor/runtime interaction mode and executes configured click interactions only in runtime usage. `RunFunction` resolves the selected stable function reference through the central folder-local `FunctionRegistry`, so runnable declarative YAML and registered Python functions can run even when no `Functions` widget is placed on the page. The picker shows friendly labels such as `YAML / new_workflow` and `Python / application_explorer_1 / raw / start_loop`, while the persisted value remains a stable registry reference such as `yaml:new_workflow` or a Python registry reference. Legacy `declarative:<name>` values remain resolvable. The optional `Argument` value is forwarded to Python functions with the existing JSON-or-plain-text payload behavior and is currently ignored by declarative functions.

## Source

- `src/Hornetstudio.Editor/Widgets/Button/`
- `src/Hornetstudio.Editor/Widgets/Button/EditorButtonControl.axaml.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/Button.help.md`
