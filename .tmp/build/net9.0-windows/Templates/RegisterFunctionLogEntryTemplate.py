# Template: Register Function -> Log Entry
# Purpose:
#   Example PythonClient template that registers one function on the host.
#   Calling that function creates a log entry and returns a structured result.
# Usage:
#   Invoke the function 'write_log_entry' from the host/button integration.

from ui_python_client import FunctionResult, PythonClient

client = PythonClient("{{CLIENT_NAME}}", capabilities=["functions", "host_log"])


@client.function(
    "write_log_entry",
    description="Writes a demo log entry from {{CLIENT_NAME}}.",
    category="demo",
)
def write_log_entry(args: dict[str, object]) -> FunctionResult:
    custom_text = str(args.get("message") or "Demo log entry").strip()
    client.log_info("{{CLIENT_NAME}} -> " + custom_text)
    return FunctionResult.ok(
        payload={"message": custom_text},
        message="Log entry written",
    )


if __name__ == "__main__":
    raise SystemExit(client.run())
