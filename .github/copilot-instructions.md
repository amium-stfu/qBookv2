# Workspace Instructions

## General Guidelines

- When changing or extending the bundled Python helper API under `Host/Python/**` or `UiEditor/Templates/ui_python_client/**`, update the command documentation in `Host/Python/Integration/ui-python-client-commands.md` and `UiEditor/Templates/ui_python_client/COMMANDS.md` in the same change.
- Treat `Host/Python/Integration/ui-python-client-commands.md` as the source-of-truth documentation for predefined Python client commands.
- When changing Python bridge behavior, generated Python folder contents, template workflow, or Python interaction argument handling, update `Host/Python/Integration/python-system-overview.md` and `UiEditor/Templates/PYTHON_SYSTEM.md` in the same change.
- When working on Python templates, Python environments, or generated Python scripts, consult `Host/Python/Integration/python-system-overview.md` and `Host/Python/Integration/ui-python-client-commands.md` first.
- When changing widget code under `UiEditor/Widgets/**`, update the matching widget Markdown documentation under `AutomationExplorer/docs/widgets/` in the same change. Use one Markdown file per widget type and keep the file name aligned with the persisted widget `Type` value so the documentation can be loaded later inside the application.
- For widget selection, use shorter separate description files instead of full .md help files, as the complete Markdown help can appear overloaded.
- When changing widget code under `UiEditor/Widgets/**`, update the matching short selection description file under `AutomationExplorer/docs/widgets/descriptions/` in the same change. Use one Markdown file per widget type and keep the file name aligned with the persisted widget `Type` value so the picker can load the concise description later.
- When changing widget code under `UiEditor/Widgets/**`, update the matching detailed help file under `AutomationExplorer/docs/widgets/help/` in the same change. Use one help Markdown file per widget type and keep the file name aligned with the persisted widget `Type` using the pattern `<Type>.help.md` so the help content can later be loaded inside a help window.
- Code comments and user-facing dialog texts in the code should remain in English, while responses should be in German.
- Align new UI elements with the theme guidelines and ensure that new UI areas are designed to match the existing windows/dialogs of the project. Ensure that new icons are always colored according to the theme and do not use fixed colors.
- For computed formulas, ensure that variable and function buttons are user-friendly and easily accessible.
- When creating a plan, execute the planning without interruption. Only pause if a genuine user decision is required. After the plan is complete, explicitly ask for approval before implementation, e.g., 'Der Plan steht. Soll ich mit der Umsetzung beginnen? Ja/Nein'. If the user agrees or expects implementation, begin directly without pausing again.
- Prefer a minimally fragmented architecture: encapsulate and provide functions within their respective components rather than requiring additional separate helper components or intermediary signals. Prioritize clear, consistent architectural rules over mixed patterns for easier documentation later.
- Remove mixed target/property patterns in widgets: controls should register all required control items, including colors, for external access, and redundant methods should be removed.
- Bevorzugt bei Methodenaufrufen named Parameter statt rein positioneller Argumente, besonders bei mehreren Parametern oder ähnlichen Typen.

## Logging Instructions

- For the planned logger file splitting, ensure that a new file is rotated at the configured time, even if the current file is nearly empty.

## Code Maintenance

- Remove all instances of `FilteredSignals`, as it is deprecated legacy code. Use `EnhancedSignal` as the replacement.