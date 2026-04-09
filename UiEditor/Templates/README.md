# Python Templates

This folder contains reusable Python templates for the PythonClient widgets.

The generated Scripts folder also receives a bundled support package named `ui_python_client`.
Use that package from your scripts instead of hand-building JSON dictionaries.
It also receives a command reference file for the predefined helper API.
For better VS Code autocompletion, the generated Scripts folder also receives local `.vscode` settings and Python extension recommendations.

## Workflow

Use the `Template` button in the widget properties next to `Python Script`.
The selected template file is copied into the current folder's `Scripts` directory.
The copied file name is based on the widget name.
The helper package `ui_python_client` is copied alongside the script automatically.

## Supported Placeholders

- `{{CLIENT_NAME}}`: display name of the Python client/widget
- `{{WIDGET_NAME}}`: widget name
- `{{SCRIPT_FILE_NAME}}`: created script file name
- `{{FOLDER_NAME}}`: current folder/page name

## Templates

### PythonClientDemo.py
General bridge demo with handshake, registered functions and structured results.
Use this as the default starting point for a full PythonClient.

### LogsTemplate.py
Shows how to send log messages through the bridge.
Includes a sample function `write_demo_logs`.

### RegisterRawValuesRandom3Template.py
Shows how to declare three values with `define_value` and update them continuously with `value_update`.
Includes `start_loop` and `stop_loop`.

### RegisterFunctionLogEntryTemplate.py
Shows how to register a host-invokable function that writes a log entry.
Includes `write_log_entry`.

## Notes

- Value templates publish data via `define_value` and `value_update`.
- The host maps these messages into the registry under paths like `PythonClients/<ClientName>/<ValueName>`.
- You can target these registry paths from signal widgets after the PythonClient is running.
- The helper package exposes typed functions like `PythonClient`, `log_info`, `register_value`, `update_value` and `@client.function(...)` for better VS Code autocompletion and debugging.
- Additional convenience hooks are available via `@client.on_init`, `@client.on_stop`, `@client.on_heartbeat`, `client.set_ready_payload(...)` and `client.log_exception(...)`.
- The copied file `ui_python_client/COMMANDS.md` documents the predefined public commands that generated Python clients can use.
