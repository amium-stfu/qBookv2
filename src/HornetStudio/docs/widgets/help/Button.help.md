# Button Help

## Widget Type

`Button`

## Overview

The Button widget provides an interactive action surface with optional text, icon, command execution, and interaction rule handling.
It can also react visually to Monitor rule states through widget-level visual rules.

## Properties

### ButtonText

Defines the text shown on the button body when text display is enabled.

### ButtonIcon

Defines the icon path used by the button.

### ButtonOnlyIcon

Controls whether only the icon is shown without button text.

### ButtonCommand

Defines a host command or legacy script command executed by the button.

### ButtonIconAlign

Controls horizontal icon placement.

### ButtonTextAlign

Controls text alignment inside the button.

### ButtonBodyBackground

Optional override for the button background.

### ButtonBodyForegroundColor

Optional override for button foreground text and icon color handling.

### ButtonIconColor

Optional explicit icon tint.

### UseThemeColor

Controls whether icon tint should follow the effective theme color.

### VisualRules

Defines Monitor-driven background overrides for the visible button fill.
Version 1 exposes only `ButtonBackColor` with `None` or `Blink` as the active effect.

## Functions and Behavior

### Execute button command

The widget can execute a configured host command or legacy Python script command.

### Execute interaction rules

Mouse release events can trigger configured interaction rules instead of or in addition to command behavior.
Dialog-oriented rules can use `OpenDialog(dialogWidgetId, origin = Screen, position = Center)` and `CloseDialog(dialogWidgetId)` with a `DialogWidget` id to control the internal overlay host.

### Respect edit mode

In editor mode, runtime interaction behavior is suppressed unless the interaction mode allows it.

### Apply visual rules

The Action tab includes a `Visual` section for Monitor-backed `ButtonBackColor` changes without altering persisted theme defaults.

## Runtime Notes

The widget separates editor interaction from runtime activation and integrates with the common interaction rule pipeline.

## Suggested Help Window Metadata

- Summary file: `src/HornetStudio/docs/widgets/Button.md`
- Help file: `src/HornetStudio/docs/widgets/help/Button.help.md`

## Source

- `src/Hornetstudio.Editor/Widgets/Button/EditorButtonControl.axaml.cs`
- `src/Hornetstudio.Editor/Models/PageItemModel.cs`
