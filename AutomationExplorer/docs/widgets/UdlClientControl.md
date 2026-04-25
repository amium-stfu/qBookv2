# UdlClientControl Widget

## Type

`UdlClientControl`

## Purpose

Connects to a UDL endpoint, shows connection state, and publishes runtime status information.

## Typical Use Cases

- Monitor UDL connectivity
- Attach runtime items to a project page
- Expose connection and item-count status to the registry

## Key Configuration

- Host and port
- Auto-connect behavior
- Debug logging
- Attached item paths and demo modules

## Runtime Notes

The widget publishes status items such as endpoint, connection, item count, message counter, and auto-connect state.

## Source

- `UiEditor/Widgets/UdlClient/`
- `UiEditor/Widgets/UdlClient/UdlClientControl.axaml.cs`

## Help

- Detailed help: `AutomationExplorer/docs/widgets/help/UdlClientControl.help.md`