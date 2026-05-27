# HornetStudio Manual

This manual defines the shared Markdown source for future in-app help and a later PDF handout.

The chapters stay intentionally lightweight while the application help experience is still evolving.

## Chapter Order

1. [Introduction](index.md)
2. [Getting Started](getting-started.md)
3. [Core Concepts](concepts.md)
4. [Registry Interaction](registry-interaction.md)
5. [UI Editor](ui-editor.md)
6. [Widgets](widgets.md)
7. [Python Integration](python-integration.md)
8. [Troubleshooting](troubleshooting.md)
9. [Glossary](glossary.md)

## Source Roles

- `manual/` contains cross-topic handbook chapters.
- `../widgets/descriptions/` contains short selection text for widget pickers.
- `../widgets/help/` contains detailed widget help for later in-app help views.
- Technical integration documents remain their own source material and are referenced from the manual where useful.

## Intended Use

- In-app help can present the chapter pages directly or through a curated navigation.
- Future PDF export should use the same manual chapter set to avoid duplicated long-form content.
- Detailed widget information should stay in widget help pages and only be summarized here when cross-topic context is useful.
