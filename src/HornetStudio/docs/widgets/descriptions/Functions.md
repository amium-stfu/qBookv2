Functions

Central folder-local function catalog for declarative YAML entries with transition support for legacy `Scripts/Workflows` files and a read-only Python view from runtime metadata.

- Lists function entries in compact rows with consistent type badges, quiet state text, and row actions
- Adds, edits, and deletes declarative YAML functions from the widget UI
- Runs runnable entries and reflects matching declarative executions started from widget rows or Button Interaction Rules
- Validates YAML structure and marks invalid declarative entries
- Keeps function definitions outside `Folder.yaml`
- Supports declarative steps: `Log`, `SetValue`, `Delay`, `IfThenElse`, `While`, and `StopFunction`
- Edits declarative steps in compact inline rows, including nested `IfThenElse` and `While` lists, a dedicated condition dialog, and a required positive Delay guard inside every While body

Best for:
Folders that should expose one compact catalog of callable functions while keeping declarative YAML and future Python ownership separate.
