# Python application for '{{CLIENT_NAME}}'
# Folder: {{FOLDER_NAME}}
# Script file: {{SCRIPT_FILE_NAME}}

from ui_python_client import PythonClient


def main() -> None:
    """Entry point for the Python application."""
    client = PythonClient(name="{{CLIENT_NAME}}")

    # TODO: register values and functions here using client.define_value / client.define_function
    # Example:
    #   value = client.define_value("ExampleValue", 0)
    #   client.log(f"Initial value: {value.value}")

    client.run()


if __name__ == "__main__":
    main()
