# CsvLoggerControl Widget

## Type

`CsvLoggerControl`

## Purpose

Logs selected signal values into CSV files and exposes logger runtime state.

## Typical Use Cases

- Simple signal recording
- Export for spreadsheet analysis
- Buffered runtime logging with file rotation options

## Key Configuration

- Output directory and filename
- Timestamp handling
- Record interval
- Signal path list
- Daily split time and max file size
- Persistence and flush settings

## Runtime Notes

The widget publishes canonical runtime items below `studio.<folder_name>.logger_runtime.<logger_name>`.
Child items use lowercase snake_case names: `record`, `output_path`, `is_recording`, `last_file`, and `status`.

## Source

- `src/Hornetstudio.Editor/Widgets/CsvLogger/`
- `src/Hornetstudio.Editor/Widgets/CsvLogger/EditorCsvLoggerControl.axaml.cs`

## Help

- Detailed help: `src/HornetStudio/docs/widgets/help/CsvLoggerControl.help.md`