# Signal Help

## Widget Type

`Signal`

## Overview

The Signal widget displays a bound runtime signal and supports typed editing, bool toggling, bit toggling, and interaction-rule-based actions.

## Properties

### TargetPath

Defines the signal target path to resolve.

### TargetParameterPath

Selects the displayed or editable parameter.

### TargetParameterFormat

Controls display formatting.

### Unit

Optional unit override for the displayed value.

### InteractionRules

Defines additional click-based behavior such as open editor, set value, toggle bool, or invoke Python functions.

### IsReadOnly

Blocks input actions when enabled.

## Functions and Behavior

### Open value dialog

The widget can open the shared value input editor for writable targets.

### Toggle bits

Bit-oriented parameter presentations can route user actions to bit toggling logic.

### Send bool input

Bool-oriented UI choices can send direct input values.

### Execute interactions

The widget can execute configured interaction rules for body and sub-control actions.

### Respect editor mode

The widget suppresses runtime interaction behavior when edit mode is active.

## Runtime Notes

Signal behavior builds on the shared target binding and parameter presentation infrastructure defined in the item model and parameter control.

## Suggested Help Window Metadata

- Summary file: `AutomationExplorer/docs/widgets/Signal.md`
- Help file: `AutomationExplorer/docs/widgets/help/Signal.help.md`

## Source

- `UiEditor/Widgets/Signal/EditorSignalControl.axaml.cs`
- `UiEditor/Widgets/Parameter/ParameterControl.axaml.cs`
- `UiEditor/Models/PageItemModel.cs`