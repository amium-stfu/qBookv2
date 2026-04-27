UdlClientControl

Connection widget for UDL communication with an inline module list and module-scoped helper exposure editing.

- Manages host, port, and connection state
- Lists runtime or persisted modules directly in the widget
- Opens exposure editing per module with an `Edit` button and can remove persisted module helper configuration with `Delete`
- Can publish bitmask channels as bool helper items directly on the affected runtime channels; current runtime scope is focused on `Publish Bits` and the stored bit count
- Updates published helper bit values without rebuilding the attached UdlClient mirror on every bit click
- Shows module-scoped exposure editing in grouped `Main`, `Bitmask`, `Settings`, and `Adjust` areas, with the bitmask area as the active first-step workflow
- Suggests default counts for common bitmask channels and can route `Read` helper writes to `Set.Request`

Best for:
Projects that need direct UDL connectivity and module-level helper exposure management in the editor UI.