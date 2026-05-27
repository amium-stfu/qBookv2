# ControllerWidget

## Type

`ControllerWidget`

## Purpose

Manages PID controller definitions and publishes each controller as runtime items.

## Typical Use Cases

- PID loop configuration inside a folder
- Owned runtime setpoint control through the direct value of each controller `set` item
- Runtime start and stop control through the direct value of each controller `run` item
- Scaled and clamped output writes to a configured target path
- Compact controller list rows that keep type, name, and edit actions directly visible

## Key Configuration

- Controller name
- Source process value path
- Output target path
- PID tuning values `Ks`, `Tu`, `Tg`, and `DFilterTauMs`
- Setpoint and output ranges
- Compute and output intervals
- Editor spacing and field tooltips help distinguish adjacent PID parameters

## Runtime Notes

Each configured PID controller publishes a runtime root below:

`studio.<folder>.controller_widget.<controller_name>`

The runtime exposes `run`, `source`, `set`, `out`, `state`, `alert`, and `parameters` children. `Source` stays bound to the configured readable picker path and `Out` stays bound to the configured writable picker path. `Set` is owned by the controller runtime itself at `studio.<folder>.controller_widget.<controller_name>.set` and stores the controller-owned setpoint directly in its item value. Writing `true` to the direct value of `run` starts evaluation and writing `false` stops it.

## Source

- `src/HornetStudio.Editor/Widgets/Controller/`
- `src/HornetStudio.Host/PidControllerRuntime.cs`
- `src/HornetStudio.Host/ControllerRuntimeManager.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/ControllerWidget.help.md`
