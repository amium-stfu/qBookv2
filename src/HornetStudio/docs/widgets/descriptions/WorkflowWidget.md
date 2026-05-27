WorkflowWidget

Folder-local workflow list and editor entry point for YAML files under `Scripts/Workflows`.

- Discovers available workflow definitions automatically
- Adds, edits, and deletes workflow files from the widget UI
- Validates YAML structure and marks invalid files
- Keeps workflow content outside `Folder.yaml`
- Supports declarative steps: `Log`, `SetValue`, `Delay`, `IfThenElse`, `While`, and `StopFunction`
- Edits nested `IfThenElse` and `While` steps with step-local condition variables, branch/body lists, and a required positive Delay guard inside every While body

Best for:
Folders that should expose a simple curated list of declarative workflows without embedding the workflow content into the page layout.