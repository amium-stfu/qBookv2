# LogControl Widget

## Type

`LogControl`

## Purpose

Displays an owned custom process log inside the page. Every LogControl owns its process log and exposes writable level input items.

## Typical Use Cases

- Custom process output display
- Runtime diagnostics on a dashboard page
- Widget-specific status and event history

## Key Configuration

- Caption and footer
- Theme-aware layout settings
- No public `TargetLog` or `AutoCreateLog` properties

## Runtime Notes

The widget always creates and shows its own `ProcessLog`.
The runtime path is generated internally from the page/folder context and widget identity as `studio.<folder>.logs.<widget_identity>`.

The runtime also publishes one writable input item per log level:

- `debug`
- `info`
- `warning`
- `error`
- `fatal`

Writing a string value to one of these items creates a log entry at the matching level. The widget always displays its own generated process log.

## Source

- `src/Hornetstudio.Editor/Widgets/Log/`
- `src/Hornetstudio.Editor/Widgets/Log/EditorLogControl.axaml.cs`
- `src/Hornetstudio.Editor/Widgets/Log/EditorProcessLogControl.axaml.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/LogControl.help.md`
