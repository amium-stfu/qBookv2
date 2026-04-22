# Host value access demo for '{{CLIENT_NAME}}'
# Shows the projected amium_host API for reading and writing host registry values.

from amium_host import host
from ui_python_client import FunctionResult, PythonClient

client = PythonClient("{{CLIENT_NAME}}", capabilities=["functions", "host_log"])
client.set_ready_payload("{{CLIENT_NAME}} can access host values")


@client.on_init
def handle_init() -> None:
    aliases = host.values.keys()
    client.log_info(f"Visible host values: {len(aliases)}")
    if aliases:
        first_alias = aliases[0]
        first_value = host.values[first_alias]
        client.log_info(
            f"First host value: alias={first_alias} path={first_value.path} value={first_value.value}"
        )


@client.function("list_host_values", description="List visible host value aliases.", category="host")
def list_host_values() -> dict[str, object]:
    return {
        "aliases": host.values.keys(),
        "paths": host.values.paths(),
    }


@client.function("read_host_value", description="Read one host value by alias or path.", category="host")
def read_host_value(args: dict[str, object]) -> FunctionResult:
    key = str(args.get("key") or "").strip()
    if not key:
        return FunctionResult.fail("Missing key.")

    try:
        value = host.values[key]
    except KeyError:
        return FunctionResult.fail(f"Unknown host value '{key}'.")

    return FunctionResult.ok(
        payload={
            "alias": value.alias,
            "path": value.path,
            "value": value.value,
            "unit": value.unit,
            "is_writable": value.is_writable,
        }
    )


@client.function("write_host_value", description="Write one host value by alias or path.", category="host")
def write_host_value(args: dict[str, object]) -> FunctionResult:
    key = str(args.get("key") or "").strip()
    if not key:
        return FunctionResult.fail("Missing key.")

    try:
        target = host.values[key]
    except KeyError:
        return FunctionResult.fail(f"Unknown host value '{key}'.")

    target.value = args.get("value")
    return FunctionResult.ok(message=f"Updated {target.path}")


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(client.run())