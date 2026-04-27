---
applyTo: "**/*.py"
description: "Use when working on Python templates, generated Python scripts, or Python environments in this workspace. Automatically consult the local Python system docs and command reference."
---

When working on Python files in this workspace:

- Read `Host/Python/Integration/python-system-overview.md` for the runtime and folder structure.
- Read `Host/Python/Integration/ui-python-client-commands.md` for the supported helper API.
- If the Python file lives in a generated script or environment folder, also consult nearby `PYTHON_SYSTEM.md` and `ui_python_client/COMMANDS.md` when present.
- Keep template-style Python files intentionally simple.
- Prefer reading `args.get("value")` for simple `InteractionRules` text arguments unless a richer JSON payload is explicitly needed.