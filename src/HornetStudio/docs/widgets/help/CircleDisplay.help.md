# CircleDisplay Help

## Widget Type

`CircleDisplay`

## Overview

The CircleDisplay widget is a circular visualization control that can expose runtime values for signal state and progress-related presentation.

## Properties

### SignalColor

Defines the color used for the circular signal visualization.

### SignalRun

Represents the running state used by the display.

### ProgressBar

Enables the progress bar layer.

### ProgressState

Stores the current progress value.

### ProgressBarColor

Defines the progress bar color.

### TableRows / TableColumns

CircleDisplay uses table-like layout metadata for its internal visible cell logic.

### VisualRules

Defines Monitor-driven background overrides for the circular display fill.
Version 1 exposes only `DisplayBackColor` with `None` or `Blink` as the active effect.

## Functions and Behavior

### Publish display runtime values

The widget publishes signal color and progress-related values into runtime data.

### Refresh runtime values on property change

Changing the display-related properties triggers publication updates.

### Visibility rules for circular cells

The control can determine whether a conceptual cell lies inside the circular display area.

### Apply visual rules

The Action tab includes a `Visual` section for Monitor-backed `DisplayBackColor` changes without altering persisted theme defaults.

## Runtime Notes

CircleDisplay runtime values are published as child items so other runtime features can consume them.
The runtime root is `studio.<folder_name>.display_runtime.<widget_name>`.
Child item segments are `signal_color`, `signal_run`, `progress_bar`, `progress_state`, and `progress_bar_color`.

## Suggested Help Window Metadata

- Summary file: `src/HornetStudio/docs/widgets/CircleDisplay.md`
- Help file: `src/HornetStudio/docs/widgets/help/CircleDisplay.help.md`

## Source

- `src/HornetStudio.Editor/Models/PageItemModel.cs`
- `src/HornetStudio.Editor/Widgets/CircleDisplay/`
