# Python bridge client demo for '{{CLIENT_NAME}}'
# Uses the bundled ui_python_client package so VS Code can offer
# autocompletion, jump-to-definition and debugger-friendly code.

from ui_python_client import FunctionResult, PythonClient

client = PythonClient("{{CLIENT_NAME}}", capabilities=["functions", "host_log"])
client.set_ready_payload("{{CLIENT_NAME}} is ready!")


@client.on_init
def handle_init() -> None:
    client.log_info("init received from host")


@client.on_heartbeat
def handle_heartbeat() -> None:
    client.log_debug("heartbeat received")


@client.function("ping", description="Simple connectivity test.", category="demo")
def ping(args: dict[str, object]) -> FunctionResult:
    return FunctionResult.ok(
        payload={"echo": args},
        message="pong from PythonClient",
    )


@client.function("add", description="Add two numbers a + b.", category="demo")
def add(args: dict[str, object]) -> dict[str, float]:
    a = float(args.get("a", 0.0))
    b = float(args.get("b", 0.0))
    return {"sum": a + b}


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(client.run())
