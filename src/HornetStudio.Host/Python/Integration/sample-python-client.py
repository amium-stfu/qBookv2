import sys
import json
from typing import Any, Dict

BRIDGE_VERSION = "1.0"


def send(message: Dict[str, Any]) -> None:
    """Send a single JSON message as one line to stdout."""
    sys.stdout.write(json.dumps(message, separators=(",", ":")) + "\n")
    sys.stdout.flush()


def log(level: str, message: str) -> None:
    send(
        {
            "type": "log",
            "bridge_version": BRIDGE_VERSION,
            "payload": {"level": level, "message": message},
        }
    )


def send_hello() -> None:
    send(
        {
            "type": "hello",
            "bridge_version": BRIDGE_VERSION,
            "payload": {
                "client_name": "SamplePythonClient",
                "client_type": "demo",
                "capabilities": ["functions", "host_log"],
                "client_version": "0.1.0",
                "api_version": "1.0",
            },
        }
    )


def send_ready() -> None:
    send({"type": "ready", "bridge_version": BRIDGE_VERSION, "payload": None})


def define_functions() -> None:
    """Register a few demo functions on the host side."""

    send(
        {
            "type": "define_function",
            "bridge_version": BRIDGE_VERSION,
            "payload": {
                "name": "ping",
                "description": "Simple connectivity test.",
                "category": "demo",
            },
        }
    )

    send(
        {
            "type": "define_function",
            "bridge_version": BRIDGE_VERSION,
            "payload": {
                "name": "add",
                "description": "Add two numbers a + b.",
                "category": "demo",
            },
        }
    )


def handle_invoke(message: Dict[str, Any]) -> None:
    request_id = message.get("request_id")
    payload = message.get("payload") or {}
    function_name = (payload.get("function_name") or "").strip()
    args = payload.get("args") or {}

    success = False
    result_message = None
    result_payload: Any = None

    try:
        if function_name == "ping":
            result_payload = {"echo": args}
            result_message = "pong from PythonClient"
            success = True
        elif function_name == "add":
            a = float(args.get("a", 0.0))
            b = float(args.get("b", 0.0))
            result_payload = {"sum": a + b}
            success = True
        else:
            result_message = f"Unknown function '{function_name}'"
    except Exception as exc:  # noqa: BLE001
        success = False
        result_message = f"Exception during '{function_name}': {exc}"

    send(
        {
            "type": "result",
            "bridge_version": BRIDGE_VERSION,
            "request_id": request_id,
            "payload": {
                "success": success,
                "message": result_message,
                "payload": result_payload,
            },
        }
    )


def main() -> int:
    send_hello()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            message = json.loads(line)
        except json.JSONDecodeError as exc:  # noqa: PERF203
            log("error", f"Failed to decode message: {exc}: {line!r}")
            continue

        msg_type = message.get("type")

        if msg_type == "init":
            log("info", "init received from host")
            define_functions()
            send_ready()
        elif msg_type == "invoke":
            handle_invoke(message)
        elif msg_type == "stop":
            log("info", "stop requested by host; shutting down")
            break
        elif msg_type == "heartbeat":
            # optional, just acknowledge via log for now
            log("debug", "heartbeat received")
        else:
            log("debug", f"Unhandled message type: {msg_type}")

    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
