# Changelog

## Unreleased

## 2026.04.28.0110

- Generate first-start default layouts from the folder template.
- Create `Assets` and `Scripts` directories for first-start default layouts.

## 2026.04.28.0046

- Renamed the item and UDL client projects to `Amium.Item` and `Amium.UdlClient`.
- Renamed the solution, projects, namespaces, resource URIs, and documentation references to HornetStudio.
- Added numbered default widget names with validation for allowed characters.
- Set new widget text defaults to the generated widget name.
- Keep default widget text synchronized with the generated name after target changes.
- Moved editor dialog validation errors above the tab content.
- Start Windows camera devices lazily only when a camera widget subscribes to frames.
