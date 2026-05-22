# LogControl Help

## Widget Type

`LogControl`

## Overview

The LogControl widget displays its own custom process log on a page and can be used for process-oriented runtime diagnostics.

## Properties

### Owned process log

Every LogControl creates a widget-owned process log.
The generated runtime path uses `studio.<folder>.logs.<widget_identity>` and is derived internally from the page/folder context and widget identity.
`TargetLog` and `AutoCreateLog` remain legacy load-only fields and are no longer part of the editable widget configuration.

### View

Controls in which page view the widget is active.

### Header / Footer / Body styling

Common visual shell properties for layout and theme integration.

## Functions and Behavior

### Display log stream

The widget always shows entries from its own generated process log.

### Integrate with page views

The widget can be shown or hidden depending on the active view.

### Process log support

The widget publishes writable level input items below the generated log path:

- `debug`
- `info`
- `warning`
- `error`
- `fatal`

Changing one of these item values writes the new string value to the generated process log at the matching level.

## Runtime Notes

LogControl is intended for in-page diagnostics and monitoring surfaces. It always shows its own local process log.

## Suggested Help Window Metadata

- Summary file: `src/HornetStudio/docs/widgets/LogControl.md`
- Help file: `src/HornetStudio/docs/widgets/help/LogControl.help.md`

## Source

- `src/Hornetstudio.Editor/Widgets/Log/EditorLogControl.axaml.cs`
- `src/Hornetstudio.Editor/Widgets/Log/EditorProcessLogControl.axaml.cs`
