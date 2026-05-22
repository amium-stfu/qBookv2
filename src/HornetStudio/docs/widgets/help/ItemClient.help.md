# ItemClient Help

`ItemClient`

Connects to an MQTT ItemBroker bus with `BrokerHost`, `BrokerPort`, `BrokerBaseTopic`, and the generated local MQTT client id stored in `ServerClientId`.

An empty `BrokerBaseTopic` is the default and means no MQTT topic prefix. Set a non-empty value only when the broker uses a shared prefix such as `hornet`.

`BrokerMode` is either `External` or `Own`. `External` keeps the previous behavior and connects to an already running broker endpoint. `Own` starts a local embedded MQTT item host on the configured endpoint and stops it again when the widget disconnects.

The local client id is generated for widgets, persisted with the layout, and shown as readonly `LocalMqttClientId` in the property dialog. Saved values that do not use the `hornet-studio-{shortGuid}` format are replaced with a generated local id when loaded.

Remote items are published under `runtime.item_broker.{WidgetName}.shared.{ItemPath}` and exposed as flat widget paths such as `{WidgetName}.{ItemPath}` through `BrokerAttachedItemPaths`. MQTT `/read` topics are reconstructed as item values and appear as attach options when a value is present. The widget body lists these rows under `Attached To UI`, places the `Attach` action and received item count in the section header, and hides the widget-name prefix in row text. The attach editor groups available paths by widget and item tree, while saved paths that are no longer live are shown as missing so they can be removed.

`PublishItems` selects local registry roots and stores structured local publish definitions in `BrokerPublishedItemPaths`. The widget body lists these roots under `Published Items`, places the `Publish` action in the section header, and hides the current `studio.<FolderName>` prefix in row text. New selections are inactive by default and default to `studio.<LocalPath>`, `OnChanged`, `1000` ms, and `Writable=false`.

Published entries use shared flat MQTT topics. For example, with an empty `BrokerBaseTopic`, `studio.default_layout.UdlClient1.m400.set` publishes `meta` JSON as `studio/default_layout/udl_client1/m400/set`, the current feedback value as `studio/default_layout/udl_client1/m400/set/read`, and retained state properties such as `unit` as `studio/default_layout/udl_client1/m400/set/unit`. The command-like `write` property is omitted from retained snapshots and is published only as a non-retained live command update when the local `write` property changes.

External write requests must publish to the `.../write` topic with `retain=false`. A non-retained write message is handled as a live request and can update the local target when the matching published definition has `Writable=true`. A retained message on `.../write` is only retained broker state, is reconstructed as a received property, and does not trigger write-back on receive or reconnect. Clear accidental retained `.../write` values by publishing an empty retained message to the same topic. Later non-retained write messages still work.

Use `Edit` in `Published Items` to configure the selected root and its subitems. The visible root row is only a grouping and navigation row; only individual rows with `Active=true` publish while connected.

Connect publishes all active entries once. Saving a root publishes retained snapshots only for active entries under that root. Retained snapshots omit `write` recursively so command values are not replayed as state on reconnect. `OnChanged` publishes exact active item changes, active subtree roots for descendant changes, and active descendants after ancestor snapshot/upsert refreshes. Ancestor value or property changes do not publish unrelated descendants. `Interval` publishes snapshots periodically for active interval entries.

Successful publish diagnostics are quiet unless the `HornetStudio.ItemClient.PublishDiagnostics` AppContext switch is enabled. Receive diagnostics summarize direct received roots, visible roots, and attach-option counts.

When an active definition has `Writable=true`, external broker updates on the exact configured broker path are written back to the local registry path. `Value` updates are allowed. Non-retained `write` updates write the local item's `write` property when available, then fall back to the resolved value target. Other property updates are checked by the central protected-property policy, so system metadata such as `Writable`, `WritePath`, `BrokerPath`, `LocalPath`, `Active`, `PublishMode`, and `PublishIntervalMs` cannot be changed through broker write-back. Recent local Host writes keep short-term priority over conflicting retained or echoed broker `read` and property state for the same effective target, so stale MQTT state does not immediately undo a local UI or Monitor write. Self-published live `write` echoes are consumed once so shared-topic command publishes do not rewrite and republish themselves. Later external equal `write` commands still notify as commands, and external non-retained `write` requests still work.
