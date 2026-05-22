ItemClient

Connects to an external or widget-owned MQTT item host with a generated readonly local client id, exposes remote retained and live MQTT `/read` values under `Attached To UI`, shows attach and publish actions in the matching list headers, publishes active local `Published Items` definitions to shared flat broker topics without retained `write` command state, and writes external live broker updates back only for active definitions marked `Writable=true`.
