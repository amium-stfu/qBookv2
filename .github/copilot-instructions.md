# Workspace Instructions

## General Guidelines

- When changing or extending the bundled Python helper API under `Host/Python/**` or `UiEditor/Templates/ui_python_client/**`, update the command documentation in `Host/Python/Integration/ui-python-client-commands.md` and `UiEditor/Templates/ui_python_client/COMMANDS.md` in the same change.
- Treat `Host/Python/Integration/ui-python-client-commands.md` as the source-of-truth documentation for predefined Python client commands.
- When changing Python bridge behavior, generated Python folder contents, template workflow, or Python interaction argument handling, update `Host/Python/Integration/python-system-overview.md` and `UiEditor/Templates/PYTHON_SYSTEM.md` in the same change.
- When working on Python templates, Python environments, or generated Python scripts, consult `Host/Python/Integration/python-system-overview.md` and `Host/Python/Integration/ui-python-client-commands.md` first.
- Code comments and user-facing dialog texts in the code should remain in English, while responses should be in German.
- Align new UI elements with the theme guidelines.

## Code Maintenance
- Remove all instances of `FilteredSignals`, as it is deprecated legacy code. Use `EnhancedSignal` as the replacement.