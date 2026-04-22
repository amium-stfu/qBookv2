# Template: Logs
# Purpose:
#   Minimal PythonClient template that shows how to write log messages to the host/client bridge.
# Usage:
#   Copy this template into a widget-specific script via the Properties dialog.
# Placeholders:
#   {{CLIENT_NAME}}     -> widget/client display name
#   {{WIDGET_NAME}}     -> widget name
#   {{SCRIPT_FILE_NAME}} -> target script file name
#   {{FOLDER_NAME}}     -> current folder name

from ui_python_client import PythonClient

client = PythonClient("{{CLIENT_NAME}}", capabilities=["functions", "host_log"])


@client.on_init
def handle_init() -> None:
    client.log_info("Template 'Logs' initialized")


@client.function("write_demo_logs", description="Writes demo log messages.", category="logs")
def write_demo_logs() -> str:
    client.log_info("{{CLIENT_NAME}}: info log from write_demo_logs")
    client.log_warning("{{CLIENT_NAME}}: warning log from write_demo_logs")
    client.log_error("{{CLIENT_NAME}}: error log from write_demo_logs")
    return "Demo logs written"


def get_log_message(args: dict[str, object]) -> str:
    message = str(args.get("value") or "").strip()
    return message or "Host log entry from {{CLIENT_NAME}}"


@client.function(
    "write_host_log",
    description="Writes the passed interaction text to the host log.",
    category="logs",
)
def write_host_log(args: dict[str, object]) -> str:
    message = get_log_message(args)
    client.log_info("{{CLIENT_NAME}} -> " + message)
    return "Host log entry written"


if __name__ == "__main__":
    raise SystemExit(client.run())
