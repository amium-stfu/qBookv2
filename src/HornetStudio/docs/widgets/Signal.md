# Signal Widget

## Type

`Signal`

## Purpose

Displays a bound signal value and can open editors or execute interactions against its target.

## Typical Use Cases

- Show live signal values
- Toggle bool or bit-based values
- Open value editors for runtime input

## Key Configuration

- Target path
- Target parameter and format
- Unit and caption
- Interaction rules
- Header, body, and footer settings

## Runtime Notes

The signal widget uses parameter visualization and can open typed value editors or send direct input to the configured target.

## Source

- `src/HornetStudio.Editor/Widgets/Signal/`
- `src/HornetStudio.Editor/Widgets/Signal/EditorSignalControl.axaml.cs`
- `src/HornetStudio.Editor/Widgets/Parameter/ParameterControl.axaml.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/Signal.help.md`