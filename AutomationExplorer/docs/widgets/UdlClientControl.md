# UdlClientControl Widget

## Type

`UdlClientControl`

## Purpose

Connects to a UDL endpoint, shows connection state, lists discovered or persisted modules inline, and can publish UdlClient-owned helper items for selected module channels.

## Typical Use Cases

- Monitor UDL connectivity
- Review modules directly inside the widget and edit one module at a time
- Attach runtime items to a project page
- Expose connection and item-count status to the registry
- Publish bit helper items for selected bitmask channels directly from the UdlClient

## Key Configuration

- Host and port
- Auto-connect behavior
- Debug logging
- Attached item paths and demo modules
- Optional module exposure definitions for bitmask-oriented helper items; in the current first step only `Publish Bits` and the explicit bit count are runtime-active
- Per-module actions through the inline module list `Edit` and `Delete` buttons

## Runtime Notes

The widget body shows a module list similar to EnhancedSignals. Each row can open a module-scoped exposure editor or remove the persisted helper configuration for that module, while socket and runtime status stay in the widget footer.

When a single module is edited from that list, the exposure dialog is organized into `Main`, `Bitmask`, `Settings`, and `Adjust` sections. `Main` currently shows the module identity, `Bitmask` is the active area for grouped helper toggles such as `Read / Set` and `Command / State`, and `Settings` plus `Adjust` are prepared as follow-up areas for later source parameterization. The `Publish Bits` switch stays visible even without format editing, and the amount of helper items is controlled directly through the stored `Count` value.

Common bitmask channels such as `Read`, `Set`, `Command`, `State`, and `Alert` receive a suggested default count of `4` when no explicit count is stored yet. The module-level option `Read Input route to Set.Request` redirects writes from published `Read` helper bits to the module `Set` request target when that target exists.

The widget publishes status items such as endpoint, connection, item count, message counter, and auto-connect state. For configured module/channel exposures it adds `Bits.Bit0...BitN` helper items directly to the matching runtime channel, so attached UdlClient paths expose those bool helper items naturally inside the project tree.

Runtime bit value updates on those published helper items are kept separate from structural exposure changes. Toggling a helper bit updates the mirrored value without republishing the full attached UdlClient subtree on every click.

When helper bits write back into a numeric runtime channel, the UdlClient preserves the target channel value type so request-oriented channels that use floating-point values keep their original runtime type.

The exposure dialog may already show additional fields for future source parameterization, but in the current first step the active runtime behavior is intentionally limited to publishing bitmask helpers and deriving their bit count.

## Source

- `UiEditor/Widgets/UdlClient/`
- `UiEditor/Widgets/UdlClient/UdlClientControl.axaml.cs`

## Help

- Detailed help: `AutomationExplorer/docs/widgets/help/UdlClientControl.help.md`