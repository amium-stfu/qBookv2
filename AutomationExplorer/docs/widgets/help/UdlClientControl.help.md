# UdlClientControl Help

## Widget Type

`UdlClientControl`

## Overview

The UdlClientControl widget connects to a configured UDL endpoint, presents connection state, and publishes runtime status and attached item information.

## Properties

### UdlClientHost

Configured host name or address.

### UdlClientPort

Configured endpoint port.

### UdlClientAutoConnect

Controls whether the client should connect automatically.

### UdlClientDebugLogging

Enables additional diagnostic logging.

### UdlAttachedItemPaths

Stores configured attached runtime items.

### UdlDemoModuleDefinitions

Stores configured demo module definitions.

### SocketText / ConnectionStateText / AutoConnectText / ItemCountText

Presentation-facing status values shown by the control.

## Functions and Behavior

### Connect and disconnect

The widget manages the connection lifecycle against the configured endpoint.

### Publish status values

Connection and item-related status values are published into runtime paths.

### Synchronize attached items

The widget can keep attached item state aligned with runtime data.

### Monitor runtime state

Background monitoring updates visible status and connection state.

## Runtime Notes

Published runtime status items include endpoint, connection, item count, message counter, and auto-connect state.

## Suggested Help Window Metadata

- Summary file: `AutomationExplorer/docs/widgets/UdlClientControl.md`
- Help file: `AutomationExplorer/docs/widgets/help/UdlClientControl.help.md`

## Source

- `UiEditor/Widgets/UdlClient/UdlClientControl.axaml.cs`