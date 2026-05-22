# ControllerWidget Help

## Widget Type

`ControllerWidget`

## Overview

The ControllerWidget stores PID controller definitions on the folder item and synchronizes them with host runtimes. Each PID controller reads a source value from the configured source path, reads its setpoint from an owned runtime `set` item value, computes a PID output, and writes the scaled result to the configured output target while running.

## Properties

### ControllerDefinitions

Stores the configured controller definitions as the widget persistence payload.

### Name / Path / FolderName

Changes to identity or folder context can rebuild the runtime path for controller instances.

### EffectiveBodyBackground / EffectiveBodyBorder / EffectiveBodyForeground / EffectiveMutedForeground

Theme values used by controller rows.

## PID Fields

The PID editor separates adjacent fields with consistent spacing and exposes tooltips on path, tuning, range, and interval inputs.

### Name

Unique controller name within the widget. The normalized name is used in the runtime path.

### Source

Path for the process value read by the controller.

### Set

The PID setpoint is not selected through a picker. Each controller publishes its own runtime item at `studio.<folder>.controller_widget.<controller_name>.set`. Writing a numeric value to that item value updates the setpoint used by the PID loop.

### Output

Target path that receives the scaled and clamped controller output.

### Ks / Tu / Tg

Tuning inputs used to derive the runtime PID parameters.

### D filter tau ms

Derivative filter time constant in milliseconds.

### Compute interval ms

Timer interval for PID evaluation.

### Output interval ms

Minimum interval between output target writes.

### Set min / Set max

Input range used to normalize source and setpoint values.

### Out min / Out max

Output range used to scale the normalized PID result.

## Runtime Behavior

### Published Path

The runtime root is:

`studio.<folder>.controller_widget.<controller_name>`

### Run Control

The runtime publishes `run` as a direct runtime value. Writing `true` to that item value requests a running state; writing `false` stops evaluation and resets integral and derivative state.

### Setpoint Control

The runtime publishes `set` as a direct runtime value. Writing a numeric value changes the PID setpoint owned by that controller runtime. Invalid nonnumeric writes are rejected at evaluation time with a waiting state and alert.

### State and Alerts

The runtime publishes `state` and `alert` values. Invalid or missing numeric source values, invalid owned setpoint values, and invalid PID parameters do not crash the runtime; it reports a waiting or invalid state with an alert message.

### Output Writes

While running, the runtime writes the computed output to the configured output path through the host registry update API.

## Source

- `src/HornetStudio.Editor/Widgets/Controller/ControllerControl.axaml.cs`
- `src/HornetStudio.Editor/Widgets/Controller/ControllerEditorDialogWindow.axaml.cs`
- `src/HornetStudio.Host/PidControllerRuntime.cs`
