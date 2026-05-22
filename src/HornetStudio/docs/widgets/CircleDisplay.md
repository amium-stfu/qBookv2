# CircleDisplay Widget

## Type

`CircleDisplay`

## Purpose

Renders a circular status display that can expose signal state and progress-related runtime values.

## Typical Use Cases

- Progress display
- Circular status indicator
- Compact runtime visualization in dashboards

## Key Configuration

- Signal color
- Run state
- Progress bar enablement
- Progress state and progress color
- Table-style cell layout settings

## Runtime Notes

The widget publishes display runtime values below `studio.<folder_name>.display_runtime.<widget_name>`.
Runtime child item segments use strict `snake_case`: `signal_color`, `signal_run`, `progress_bar`, `progress_state`, and `progress_bar_color`.

## Source

- `src/HornetStudio.Editor/Models/PageItemModel.cs`
- `src/HornetStudio.Editor/Widgets/CircleDisplay/`
- `src/HornetStudio.Editor/Widgets/FolderEditor/`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/CircleDisplay.help.md`
