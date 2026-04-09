# ui_python_client Commands

This file is copied into each generated Python client folder.
It documents the predefined public commands of the bundled `ui_python_client` helper API.

Source of truth:
- `Host/Python/Integration/ui-python-client-commands.md`

Rule:
- whenever the helper API changes, update this file in the same change

## Import

```python
from ui_python_client import FunctionResult, PythonClient
```

## Predefined Commands

### `PythonClient(...)`
Creates the client bridge object.

### `client.function(name, description="", category="demo")`
Decorator to register a host-callable function.

### `client.on_init`
Decorator for code that runs after the host sends `init`.

### `client.on_stop`
Decorator for code that runs when the host sends `stop`.

### `client.on_heartbeat`
Decorator for code that runs when the host sends `heartbeat`.

### `client.set_ready_payload(payload)`
Sets the payload returned with the automatic `ready` message.

### `client.register_value(name, title=None, unit=None)`
Declares a value for the host registry.

### `client.update_value(name, value)`
Publishes a new runtime value.

### `client.log(level, message)`
Generic log function.

### `client.log_debug(message)`
Writes a debug log.

### `client.log_info(message)`
Writes an info log.

### `client.log_warning(message)`
Writes a warning log.

### `client.log_error(message)`
Writes an error log.

### `client.log_exception(exc, message=None)`
Writes a formatted exception log.

### `client.hello()`
Sends the `hello` handshake message manually.

### `client.ready(payload=None)`
Sends the `ready` message manually.

### `client.result(request_id, result)`
Sends a manual structured function result.

### `client.run()`
Starts the bridge loop and handles host messages.

## VS Code Support

Generated Python client folders also receive:

- `.vscode/settings.json` with Python analysis settings for local imports and auto-import completions
- `.vscode/extensions.json` recommending the VS Code extensions `ms-python.python` and `ms-python.vscode-pylance`

### `FunctionResult.ok(payload=None, message=None)`
Builds a success result.

### `FunctionResult.fail(message, payload=None)`
Builds a failure result.

## Minimal Example

```python
from ui_python_client import FunctionResult, PythonClient

client = PythonClient("LogTest", capabilities=["functions", "host_log"])


@client.on_init
def handle_init() -> None:
    client.log_info("initialized")


@client.function("ping", description="Connectivity test", category="demo")
def ping(args: dict[str, object]) -> FunctionResult:
    return FunctionResult.ok(message="pong", payload={"echo": args})


if __name__ == "__main__":
    raise SystemExit(client.run())
```