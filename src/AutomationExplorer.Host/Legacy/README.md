# Host Legacy Archive

This folder contains legacy host files that are intentionally excluded from active compilation.

Reason:
- Keep active Host root focused on current runtime paths.
- Preserve historical code for reference during migration.

Notes:
- Do not add these files to `UiEditor.Host.csproj` compile items unless a migration task explicitly requires it.
- New host development should target the active files in the parent `Host` folder.
