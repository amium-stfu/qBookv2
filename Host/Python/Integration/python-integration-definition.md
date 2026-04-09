# Python Integration Working Draft

Status: Draft
Phase: Definition only
Implementation: Blocked until explicit approval

## Purpose

This document defines the target architecture for a host-controlled Python integration layer.
We use it as the working contract until implementation is explicitly approved.

## Core Goal

Python may:
- build interfaces
- deliver raw values
- provide functions
- provide process logic and workflows

The host remains responsible for:
- lifecycle
- startup
- stop
- hard stop
- cleanup on project switch
- visualization
- invocation
- control
- routing
- logging
- timeout handling
- state reporting

## Architectural Direction

Working name:
- PythonIntegration
- possible runtime component name later: PythonClient

Principle:
- Python is an integration and logic layer.
- The host is the owner of runtime control.
- Python must never own UI, runtime shutdown, or project lifecycle.

## Separation of Responsibility

### Python side

Allowed responsibilities:
- connect to external systems
- open custom interfaces
- implement protocol adapters
- read and transform raw data
- expose callable functions
- implement internal workflows
- publish values and states through the host bridge

Not allowed as final system responsibility:
- direct UI rendering
- direct editor manipulation
- owning process lifecycle
- deciding when the runtime survives project switch
- bypassing host stop and hard-stop rules

### Host side

Required responsibilities:
- create and supervise the Python runtime
- define startup and shutdown policy
- soft stop and hard stop handling
- kill whole process tree if required
- attach runtime to project/runtime scope
- collect logs and errors
- define callable surface between UI and Python
- expose values, commands, and states to the rest of the system
- manage visualization and operator interaction

## First Design Assumptions

1. Python should run out-of-process.
2. The host should remain the lifecycle owner.
3. Python integration should be runtime-scope aware.
4. Project switch must stop all Python runtimes cleanly or forcefully.
5. The bridge between host and Python should be explicit and versionable.

## Environment and Folder Model

Preferred direction:
- One host-side PythonManager per project/runtime scope.
- The PythonManager supervises one or more Python environments (Envs).
- Each Env is represented by a dedicated folder under a common Python root.

Conceptual layout (example):

```text
<ProjectFolder>/Python/
    lib/                       # shared helper libraries (e.g. ui_python_client)
    pdfCreator/                # Env 1
        core/                    # Env-specific core logic
        scripts/                 # entry scripts / workflows
        config/                  # configuration for this Env
    ModbusClient/              # Env 2
        core/
        scripts/
        config/
```

Principles:
- Shared, reusable code (such as the PythonClient SDK and common helpers) lives under the shared `lib` root and is imported by all Envs.
- Each Env folder contains only Env-specific scripts, configuration, and structure.
- Envs are copyable as self-contained mini-projects (folder-based portability), while still depending on the shared library surface.

Runtime ownership:
- The PythonManager creates and supervises one Python process per Env.
- Every Env process still follows the same handshake, lifecycle, and bridge rules defined in this document.
- Project switch or runtime stop must dispose all Env processes owned by the PythonManager.

## Candidate Feature Set

### Data publishing

Python should be able to:
- define values
- define groups of values
- update values
- publish status text
- publish health state
- publish diagnostics

### Functions

Python should be able to:
- expose callable functions
- accept parameters
- return results
- optionally support long-running operations with progress/status

## Function Registry Model

Preferred direction:
- Python may register host-callable functions.
- The host may bind these functions to buttons, rules, events, or other host-controlled triggers.
- Invocation authority remains fully with the host.

This is broader and more precise than a pure EventRegistry.

Preferred concepts:
- FunctionRegistry for callable Python methods
- host-side event bindings that decide when functions are invoked

### Responsibility split

Python side:
- implements functions
- registers callable function names and metadata

Host side:
- decides visibility and availability of functions
- binds functions to buttons, rules, events, or workflows
- invokes functions
- applies timeout, cancellation, logging, and error handling

### Candidate function registration concepts

Possible concepts:
- `define_function(name, description=None)`
- `register_function(name, handler, description=None)`
- `invoke(function_name, args)`

### Result contract for host-invoked functions

Preferred direction:
- Host-invoked Python functions should return a structured result type.
- A plain `bool` may be acceptable for an early prototype, but it is not the preferred long-term contract.

Why a structured result is preferred:
- `bool` only signals success or failure
- `bool` does not explain the reason for failure
- `bool` cannot carry payload data
- `bool` does not model timeout, cancellation, partial success, or richer execution status

### Completion model

The host should detect completion by call termination:
- normal return means the function finished
- exception means technical failure
- timeout or cancellation remain host-owned lifecycle outcomes

The returned result should describe the execution outcome, not merely the fact that the call ended.

### Preferred result shape

Possible concepts:
- `success: bool`
- `message: str | None`
- `payload: object | None`
- optional later: `code`, `severity`, `details`

Possible conceptual model:
- `Result(success=True, message=None, payload=None)`
- `Result(success=False, message="Device not connected", payload=None)`

### Minimum compatibility path

Possible staged approach:
- phase 1: host may accept `bool`
- phase 2: preferred contract becomes structured result objects
- phase 3: generated Python stubs describe the supported result type explicitly

### Interface adapters

Examples:
- custom Modbus
- serial devices
- TCP or UDP protocols
- HTTP-based devices
- vendor-specific SDK wrappers
- file-based integrations

## Logging Model

Preferred direction:
- Every PythonClient gets its own ProcessLog automatically.
- Python scripts must be able to write to both the central host log and the client-local log.

### Logging responsibilities

Host log should contain:
- runtime lifecycle events
- startup and shutdown events
- soft stop and hard stop escalation
- timeout events
- bridge errors
- handshake errors
- scope cleanup events
- critical warnings and errors from Python clients

Client ProcessLog should contain:
- client-local diagnostics
- protocol details
- adapter-specific debug output
- workflow messages from the Python script
- operational status information of the specific client

### Logging access from Python

Python scripts must be able to reach:
- Host/Log
- ClientLog

Possible concepts:
- `ctx.host_log.debug(message)`
- `ctx.host_log.info(message)`
- `ctx.host_log.warn(message)`
- `ctx.host_log.error(message)`
- `ctx.client_log.debug(message)`
- `ctx.client_log.info(message)`
- `ctx.client_log.warn(message)`
- `ctx.client_log.error(message)`

Alternative shorthand concepts may be supported later, but the separation between host log and client log must remain explicit.

### Logging rules

Preferred rules:
- Host-owned runtime messages are always written by the host into the host log.
- PythonClient-specific script messages are written into the client ProcessLog.
- Warning and error messages from the Python side may additionally be mirrored into the host log.

### Rationale

This model keeps:
- a central operational view in the host
- a local diagnostic view per PythonClient
- clean separation between lifecycle logging and adapter-specific logging

## Candidate Host Bridge API

This is only a draft and not yet approved.

## Preferred Transport Contract

Preferred direction:
- Host and Python communicate directly through the Python process streams.
- Preferred transport is `stdin` and `stdout`.
- Preferred message format is line-delimited JSON.
- `stderr` is reserved for diagnostics and auxiliary error output.

This does not mean file-based exchange through JSON files on disk.

### Preferred channel mapping

- `stdin`: Host -> Python
- `stdout`: Python -> Host
- `stderr`: diagnostics, warnings, trace output

### Preferred message format

Preferred format:
- one JSON object per line
- newline-delimited JSON

Why this is preferred:
- simple to implement
- easy to debug
- no extra port management
- tightly coupled to the supervised process lifecycle
- good fit for out-of-process Python runtime control

### Not preferred as primary runtime transport

Not preferred for the first implementation:
- file-based JSON exchange
- polling files on disk for commands or results

Reason:
- worse synchronization behavior
- worse stop/cancel behavior
- more cleanup complexity
- more file locking and race condition risks

### Transport message expectations

Every transport message should be able to carry at least:
- a message type
- an optional request id
- an optional bridge version
- a payload object

### Candidate message kinds

Possible concepts:
- `hello`
- `init`
- `define_value`
- `define_function`
- `value_update`
- `log`
- `invoke`
- `result`
- `error`
- `heartbeat`

### Working recommendation

Current best recommendation:
- start with `stdin/stdout`
- use line-delimited JSON messages
- keep the transport contract versioned
- add stricter handshake and timeout rules on top of this transport later

## Handshake And Versioning Model

Preferred direction:
- Every PythonClient must complete a defined startup handshake before productive runtime communication begins.
- Host and Python must both declare the bridge version they speak.
- The host decides whether the PythonClient is compatible and allowed to continue.

### Purpose of the handshake

The handshake exists to define at startup:
- who the PythonClient is
- which bridge version it supports
- which capabilities it exposes
- whether the host accepts this runtime instance
- when productive communication may begin

Without a defined handshake, later bridge evolution becomes fragile and difficult to validate.

### Preferred startup sequence

1. Host starts the Python process.
2. Python sends `hello`.
3. Host validates bridge version and capabilities.
4. Host sends `init` if accepted.
5. Python initializes adapters and internal state.
6. Python sends `ready`.
7. Only after `ready` may productive messages such as value definitions, logs, updates, and function declarations be treated as valid runtime traffic.

### Preferred handshake messages

Possible concepts:
- `hello`
- `init`
- `ready`
- `reject`
- `error`

### Candidate hello payload

Possible concepts:
- `bridge_version`
- `client_name`
- `client_type`
- `capabilities`
- optional later: `client_version`, `api_version`

### Candidate init payload

Possible concepts:
- `bridge_version`
- `session_id`
- `configuration`
- `allowed_capabilities`
- `timeouts`
- `runtime_scope_metadata`

### Versioning rules

Preferred minimum versioning model:
- `bridge_version` is mandatory

Possible later additions:
- `api_version`
- `client_version`

Preferred compatibility rule:
- the host validates bridge compatibility before accepting the runtime
- incompatible versions must lead to a clean reject path

Exact compatibility policy is still open, but a likely direction is:
- major version mismatch = reject
- compatible minor version range = accept

### Capability negotiation

The handshake should also define which features are available.

Possible capabilities:
- values
- functions
- host_log
- client_log
- dynamic_definitions
- progress_reporting
- cancellation

This allows the host to adapt behavior without assuming every PythonClient supports every feature.

### Failure handling

Preferred behavior:
- if no valid `hello` arrives within the startup timeout, startup fails
- if version validation fails, the host rejects the runtime
- if initialization fails before `ready`, the host treats the PythonClient as a failed startup
- failed startup remains a host-controlled lifecycle outcome and may escalate to hard termination if needed

### Working recommendation

Current best recommendation:
- define handshake as a mandatory startup phase
- require `bridge_version`
- require `hello`, `init`, and `ready`
- treat compatibility and acceptance as host-owned decisions

## Communication Pattern

Preferred direction:
- Communication should be host-led.
- Python may declare and publish.
- The host remains the runtime controller, invocation owner, and lifecycle owner.
- Allowed message types should depend on the current runtime state.

This means communication is not fully free-form.
Both sides may only send the messages that are valid for the current phase.

### Core communication rules

Preferred rules:
- Host-owned invocation: only the host starts function calls.
- Python-owned publishing: Python publishes values, status, logs, and declared capabilities.
- Request/response correlation: every answer to a host call should carry a request id.

### Preferred runtime states

Possible concepts:
- `Created`
- `Handshake`
- `Initializing`
- `Declaring`
- `Running`
- `Stopping`
- `Stopped`
- `Faulted`

### Start phase

Before `ready`, only startup messages are valid.

Python may send:
- `hello`
- `error`
- `ready`

Host may send:
- `init`
- `reject`
- `stop`

Rule:
- messages such as value updates or function results must not be treated as valid productive runtime traffic before `ready`

### Declaration phase

After `ready`, Python may declare what it provides.

Python may send:
- `define_value`
- `define_function`
- `define_status_channel`
- `define_group`

Host may send:
- `stop`
- optional acknowledgements if later needed

Rule:
- Python describes capabilities and runtime structure in this phase
- the host adopts these definitions for registry, UI, and bindings

### Running phase

After declaration, normal runtime communication begins.

Python may send:
- `value_update`
- `status_update`
- `log`
- `progress`
- `result`
- `error`
- optional later: controlled event messages if explicitly supported

Host may send:
- `invoke`
- `cancel`
- `ping`
- `stop`

Rule:
- Python does not directly control UI actions
- the host invokes registered functions and manages the surrounding behavior

### Stop phase

When the host requests shutdown, a defined stop sequence begins.

Host may send:
- `stop`

Python may send:
- `stopping`
- final `log`
- `stopped`

Rule:
- if Python does not finish in time, the host escalates to hard stop

### Fault phase

Errors may happen in every phase, but the host remains responsible for final state handling.

Python may send:
- `error`

Host decides whether to:
- log only
- move runtime to `Faulted`
- begin stop
- escalate to hard stop

### Candidate direction map

Host -> Python:
- `init`
- `invoke`
- `cancel`
- `stop`
- `ping`

Python -> Host:
- `hello`
- `ready`
- `define_value`
- `define_function`
- `define_status_channel`
- `value_update`
- `status_update`
- `progress`
- `result`
- `log`
- `error`
- `stopped`
- `pong`

### Working recommendation

Current best recommendation:
- use a state-based communication model
- keep invocation host-led
- keep publishing Python-led
- validate incoming message types against the current runtime state

## Robustness Rules

Preferred direction:
- The bridge should follow a small set of explicit robustness rules.
- These rules should protect the host from stale messages, hanging calls, oversized payloads, and ambiguous runtime state.

### Required message frame

Every runtime message should carry at least:
- `type`
- `bridge_version`
- optional `request_id`
- optional `session_id`
- `payload`

This ensures every message can be validated and interpreted consistently.

### Session ownership

Preferred rule:
- Every supervised Python runtime instance gets a unique `session_id` from the host.
- Messages from old sessions must never affect the current active runtime.

This protects against stale or delayed messages after stop, restart, or project switch.

### Request correlation

Preferred rule:
- Every host request that expects a response must include a `request_id`.
- Every Python response to that request must return the same `request_id`.

Typical examples:
- `invoke`
- `cancel`
- optional later: `ping`

### State validation

Preferred rule:
- Incoming message types must be validated against the current runtime state.

Examples:
- no `value_update` before `ready`
- no `result` without an open request
- no invalid declaration messages in states where declarations are closed

Invalid messages should be ignored or rejected and should be logged by the host.

### Timeouts

Preferred rule:
- Every critical lifecycle or request phase should have its own timeout.

Minimum timeout categories:
- process start to `hello`
- `hello` to `ready`
- `invoke` to `result`
- `stop` to `stopped`

When a timeout is exceeded:
- the host marks the correct fault state
- the host decides whether to retry, stop, or hard-kill

### Liveness / heartbeat

Preferred later rule:
- the bridge should support heartbeat or ping/pong liveness checks

Possible concepts:
- host sends `ping`, Python answers `pong`
- or Python emits periodic heartbeat messages

This helps detect:
- blocked runtimes
- broken streams
- dead processes that have not shut down cleanly

### Payload limits

Preferred rule:
- message size should be bounded
- log payloads should be bounded
- unusually large payloads should be rejected or truncated under host control

This prevents one PythonClient from blocking the bridge with oversized messages.

### Structured error format

Preferred rule:
- protocol and runtime errors should use a structured error message

Possible concepts:
- `code`
- `message`
- optional `details`
- optional `request_id`

This allows the host to distinguish technical bridge failures from business-level function failures.

### Idempotent stop behavior

Preferred rule:
- repeated `stop` handling must be safe
- multiple stop attempts must not corrupt runtime state

This is especially important when stop, timeout, and scope cleanup overlap.

### Host-owned escalation

Preferred rule:
- the host always remains the owner of escalation up to hard stop

If handshake, invoke, or shutdown fails:
- the host decides whether to wait, fault, stop, or kill the whole process tree

### Minimum robustness core

If the first implementation needs a compact mandatory subset, the preferred minimum is:
- every message carries `type` and `bridge_version`
- every runtime instance gets a `session_id`
- every host request with response uses `request_id`
- incoming messages are validated against the runtime state
- startup, invoke, and stop have explicit timeouts
- host owns final escalation to hard stop

## Preferred Python Developer Experience

Preferred direction:
- The host provides a stable Python integration API.
- The host additionally generates Python stub files for editor support.
- Runtime execution must not depend on a generated helper file.

Why:
- Autocompletion should be based on a stable typed contract.
- Runtime behavior and editor assistance should stay separated.
- Internal host registries should not be mirrored 1:1 as the runtime contract.

### Preferred solution

The preferred solution is:
- a small stable Python SDK surface exposed by the host
- plus generated `.pyi` stub files for autocomplete and typing

This is preferred over:
- generating one dynamic `.py` file that acts as the runtime contract for all scripts

### Proposed structure

Possible generated package layout:
- `amium_host/__init__.py`
- `amium_host/runtime.pyi`
- `amium_host/registries.pyi`
- `amium_host/types.pyi`

Purpose of the files:
- runtime API stays small and stable
- stubs describe values, functions, registries, and types for IntelliSense
- generated project-specific information can be refreshed without changing the runtime bridge design

### Registry exposure principle

The host should be able to expose registry content to Python, but not as a raw dump of internal structures.

Preferred rule:
- expose a Python-friendly, filtered, versionable view

Not preferred:
- exporting internal host registry implementation details directly into Python

### Scope of generated information

The generated Python stubs may include:
- visible registry roots
- available value paths
- callable host functions
- status channels
- command signatures
- basic value and return types where known

The generated Python stubs should not be treated as:
- the runtime transport layer
- the runtime state store
- the owner of host lifecycle behavior

### Design-time vs runtime

Design-time support:
- generated stubs provide autocomplete and type hints
- optional project-local previews can describe currently known targets

Runtime support:
- the host bridge remains the real source of truth
- Python interacts with the host through the runtime API, not through static generated code alone

### Working recommendation

Current best recommendation:
- build a stable host-owned Python API
- generate `.pyi` stubs for autocomplete
- optionally generate project-local helper modules for convenience
- do not make runtime execution depend on a generated dynamic registry file

### Runtime definition

Possible concepts:
- define_value(name, type=None, unit=None)
- define_group(name)
- define_function(name, description=None)
- register_function(name, handler, description=None)
- define_status_channel(name)

### Runtime updates

Possible concepts:
- set_value(name, value)
- set_values(dict)
- set_status(text, level=None)
- set_health(state)
- log(message, level=None)
- host_log(level, message)
- client_log(level, message)

### Host-driven invocation

Possible concepts:
- invoke(function_name, args)
- invoke_async(function_name, args)
- cancel(operation_id)
- function calls return a structured result object

## Lifecycle Model Draft

### Startup

Host:
- loads configuration
- creates supervised Python runtimes for all configured environments of the current scope
- performs handshake
- receives declared values and functions
- exposes them to registry and UI

Python:
- initializes adapters
- declares capabilities
- starts internal work loops only after handshake is complete

### Stop

Soft stop:
- host requests shutdown per Env or for all Envs
- each Python runtime gets a short cleanup window
- adapters close gracefully per Env

Hard stop:
- host terminates the process tree of the affected Env if the timeout is exceeded

### Project switch

Rules:
- old runtime scope must be disposed completely
- no Python runtime may survive into the next project scope
- stale callbacks must not update current runtime state

## Registry / UI Expectations

Open question:
- should values and functions be declared purely from Python at runtime,
  or should the editor support an optional static preview?

Current leaning:
- runtime declaration from Python should be supported
- optional editor-time preview may be added later
- generated Python stubs should support editor-time autocomplete for visible host values and functions
- registered Python functions should be visible to the host for binding to UI and runtime triggers

### Python Widget and Environment Management

Preferred direction:
- One generic Python widget per page/host context instead of one widget per environment.
- The widget is responsible for operator interaction, not for Python logic.
- The widget communicates with the PythonManager to start, stop, and monitor environments.

Recommended widget model:
- Header actions:
    - `Start all` / `Stop all` to control all configured environments for the current scope.
- Body list:
    - one row per environment (Env) configured for the project/runtime scope
    - shows Env name, status (running/stopped/faulted), and last message or health indicator
    - provides per-Env actions such as `Start`, `Stop`, `Restart`, and `Edit`.

Edit behavior:
- The `Edit` action for an Env asks the host to open the corresponding Env folder in the external editor.
- The host delegates this to a launcher component (for example `VscodeLauncher`), which opens the Env root folder.
- Exact editor choice and behavior remain host-owned; the widget itself does not know editor details.

Runtime isolation expectations:
- Each Env is backed by its own supervised task/process started by the PythonManager.
- Tasks must be isolated: one Env must not be able to keep another Env alive.
- Stop and hard-stop semantics apply per Env and for the aggregate "all Envs" operation.

## Safety / Stability Requirements

Required:
- strict host ownership of process lifetime
- timeout-based shutdown
- hard kill fallback
- protection against stale runtime callbacks
- clear runtime state model
- structured logs
- explicit error propagation

Optional later:
- restart policy
- resource limits
- heartbeat monitoring
- watchdog escalation

## Open Questions

1. Should Python define values dynamically at runtime, or should there be an optional manifest file?
2. Should functions be synchronous only, or also long-running with progress reporting?
3. Should the bridge be stdin/stdout JSON, sockets, named pipes, or another protocol?
4. Should Python be allowed to declare writable commands only, or also request subscriptions?
5. Should configuration live in the widget, in a separate Python integration asset, or both?

## Proposed Next Definition Steps

1. Agree on the runtime scope model.
2. Agree on the host/Python bridge protocol.
3. Define value/function declaration format.
4. Define stop and hard-stop contract.
5. Define how declared items appear in registry and UI.
6. Define minimal first implementation slice.

## Decision Log

### Confirmed

- Python may build interfaces, provide raw values, and provide functions/workflows.
- Host owns invocation, visualization, control, and lifecycle.
- We define the architecture first and do not implement until explicitly approved.
- Preferred developer experience is a stable host API plus generated `.pyi` stubs for autocomplete.
- Every PythonClient should receive its own ProcessLog automatically.
- Python scripts must be able to write to both Host/Log and ClientLog.
- Host-invoked Python functions should move toward a structured result type instead of a plain `bool`.
- Preferred transport is direct process communication via `stdin/stdout` with line-delimited JSON messages.
- Preferred startup model includes a mandatory handshake with explicit bridge version validation.
- Preferred communication pattern is state-based and host-led for invocation, while Python remains responsible for declarations and runtime publishing.
- Preferred robustness model includes session ids, request correlation, state validation, explicit timeouts, and host-owned escalation.
 - One host-owned PythonManager supervises multiple folder-based Python environments under a common Python root.
 - Each Python environment is represented by its own folder and Python process, while sharing a common PythonClient SDK and helper library surface.

### Pending

- final naming
- declaration protocol
- transport channel
- function model
- configuration model
