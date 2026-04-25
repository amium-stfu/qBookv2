# Item Widget

## Type

`Item`

## Purpose

Displays and optionally edits a bound target value with caption, body, and footer presentation.

## Typical Use Cases

- Show a runtime value
- Display units and formatting
- Open an input editor for writable targets

## Key Configuration

- Target path
- Target parameter path and format
- Unit and body caption
- Header, body, and footer styling
- Interaction rules

## Runtime Notes

The item widget resolves a registry target, formats its parameter view, and can write values back when the target is writable.

## Source

- `UiEditor/Models/PageItemModel.cs`
- `UiEditor/Widgets/Parameter/`
- `UiEditor/Widgets/ValueInput/`

## Help

- Detailed help: `AutomationExplorer/docs/widgets/help/Item.help.md`