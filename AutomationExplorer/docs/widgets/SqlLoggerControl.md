# SqlLoggerControl Widget

## Type

`SqlLoggerControl`

## Purpose

Logs configured signals into a SQLite-backed database file and exposes runtime logging status.

## Typical Use Cases

- Durable structured signal logging
- Queryable runtime history
- Local database-based recording

## Key Configuration

- Output path derived from logger directory and filename
- Record interval and signal paths
- Persistence behavior

## Runtime Notes

The SQL logger follows the same logger runtime pattern as the CSV logger, but stores data in a database file.

## Source

- `UiEditor/Widgets/SqlLogger/`
- `UiEditor/Widgets/SqlLogger/EditorSqlLoggerControl.axaml.cs`

## Help

- Detailed help: `AutomationExplorer/docs/widgets/help/SqlLoggerControl.help.md`