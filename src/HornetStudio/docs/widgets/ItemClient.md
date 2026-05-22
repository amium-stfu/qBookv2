# ItemClient

`ItemClient` connects to an MQTT ItemBroker bus and exposes attached remote MQTT items under `studio.<FolderName>.{WidgetName}.{ItemPath}`. The internal MQTT shared root marker and transport marker are not shown in the visible path, so a shared broker item such as `Edm1.Pressure` appears as `ItemClient1.Edm1.Pressure`.

`BrokerMode` controls where the bus comes from. `External` connects to the configured `BrokerHost` and `BrokerPort` without starting anything locally. `Own` starts a local embedded MQTT item host on the configured endpoint, then connects the widget client to that local endpoint.

Widgets use a generated local MQTT client id such as `hornet-studio-1a2b3c4d`. The id is stored in the persisted `ServerClientId` property and displayed as readonly `LocalMqttClientId` in the property dialog so it cannot accidentally be replaced with a remote client id.

The widget body is split by direction. `Attached To UI` lists remote broker items that are exposed into Hornetstudio and shows the `Attach` action plus the received item count in the section header. Rows hide the widget-name prefix for readability, so `item_client_1.edm1.pressure` is displayed as `edm1.pressure` while the saved technical path stays unchanged. `Published Items` lists local registry roots that may publish from HornetStudio to the broker and shows the `Publish` action in the section header. Rows hide the current `studio.<FolderName>` prefix for readability, so `studio.main.enhanced_signals.filtered_1` is displayed as `enhanced_signals.filtered_1` while the saved local path stays unchanged.

The `AttachToUi` editor displays remote paths as a compact item tree and keeps the selected broker item identities in `BrokerAttachedItemPaths`. Received MQTT `/read` topics are reconstructed as item values and are attachable when a value is present. Live received items are registered directly below the widget branch.

The `PublishItems` editor selects local registry roots and stores structured local publish definitions in `BrokerPublishedItemPaths`. New selections appear in `Published Items` but are inactive by default. The body row is only a grouping and navigation row for that local root; the actual publish units are the individual active definitions configured through `Edit`. Use the row `Edit` action to enable `Active` for the root or any subitem, choose `PublishMode`, set `PublishIntervalMs`, and store the future-facing `Writable` flag.

`Status.AttachOptions` is an internal discovery branch used by the attach dialog and is hidden from normal item-tree/runtime data display. `Published Items` are managed through the Item client publish UI and are not duplicated as normal received item-tree entries.

Active local entries are published one-way to the MQTT ItemBroker when the widget is connected. New definitions default to broker path `studio.<LocalPath>`, `Active` `false`, `PublishMode` `OnChanged`, `PublishIntervalMs` `1000`, and `Writable` `false`.

Published values use flat shared MQTT topics. An empty `BrokerBaseTopic` is the default and means no MQTT topic prefix: the broker path `studio.default_layout.UdlClient1.m400.set` publishes `meta` JSON to `studio/default_layout/udl_client1/m400/set`, its readback value to `studio/default_layout/udl_client1/m400/set/read`, and state properties such as `unit` to `studio/default_layout/udl_client1/m400/set/unit`. The command-like `write` property is omitted from retained snapshots and is published only as a non-retained live command update when the local `write` property changes. With `BrokerBaseTopic=hornet`, the same topics are prefixed with `hornet/`. The topic does not contain the MQTT client id, so every client reads and writes the same shared item topic in MQTT Explorer.

External write requests must publish to the `.../write` topic with `retain=false`. A non-retained publish such as `studio/default_layout/udl_client1/m400/set/write = 23` is treated as a live write request and can be written back to the configured local item when the matching published definition has `Writable=true`. A retained publish on the same `.../write` topic is treated only as retained broker state and is reconstructed as a received property; it does not trigger local write-back on receive or reconnect. If a `.../write` topic was accidentally published retained, clear that retained broker value explicitly, for example by publishing an empty retained message to the same topic. Later non-retained `.../write` messages still work even while an old retained value is visible in MQTT Explorer.

Snapshots are scoped to active definitions. Connect publishes all active entries once. Saving a `Published Items` root publishes retained snapshots only for active definitions under that saved root. `Interval` publishes retained snapshots only for active interval entries. Retained snapshots omit `write` recursively so command values are not replayed as state on reconnect. `OnChanged` publishes the exact active entry that changed, an active subtree root when one of its descendants changes, or an active descendant after an ancestor snapshot/upsert refreshes it. Ancestor value and property updates do not publish unrelated active descendants.

Successful high-frequency publish updates are quiet by default. Enable the `HornetStudio.ItemClient.PublishDiagnostics` AppContext switch only when detailed successful publish traces are needed. Receive diagnostics stay compact and compare direct received roots, visible compatibility roots, and attach-option counts.

`PublishMode` supports `OnChanged` for registry change publishing and `Interval` for periodic snapshot publishing. `Writable=false` is the safe default. When a connected active definition has `Writable=true`, external broker updates on its exact flat `BrokerPath` topic are written back to the configured `LocalPath`. `Value` updates write the local item value. Non-retained `write` updates write the local item's `write` property when that property exists, then fall back to the resolved value target. Property updates use the central `HostRegistryPropertyPolicy`, so protected system metadata such as `Writable`, `WritePath`, `BrokerPath`, `LocalPath`, `Active`, `PublishMode`, and `PublishIntervalMs` is blocked. Recent local Host writes keep short-term priority over conflicting retained or echoed broker `read` and property state for the same effective target, so stale MQTT state does not immediately revert a local UI or Monitor write. Self-published live `write` echoes from the shared MQTT topic are consumed once before write-back so they do not republish themselves; later external equal `write` commands still notify as commands, and external non-retained `write` requests remain allowed. Inactive or non-writable definitions are ignored for write-back and do not publish. Definitions are not removed from retained broker state automatically.

## Properties

- `BrokerHost`
- `BrokerPort`
- `BrokerBaseTopic` (empty means no MQTT topic prefix)
- `ServerClientId` (generated local MQTT client id, shown as readonly `LocalMqttClientId`)
- `BrokerMode` (`External` or `Own`)
- `BrokerAutoConnect`
- `BrokerAttachedItemPaths`
- `BrokerPublishedItemPaths` (JSON definitions configured through `PublishItems` and `Published Items`: `LocalRootPath`, `LocalPath`, `BrokerPath`, `Active`, `PublishMode`, `PublishIntervalMs`, `Writable`)
