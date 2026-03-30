# Page-Relative Layout References

## Goal

Layout files should only reference items that belong to their own page. Because of that, layout-internal references must be stored relative to the current page instead of including the page name or the `UdlBook` root.

## Reason

- Renaming a page must not break saved layout references.
- Moving or copying a layout file must keep references valid.
- The page context is already known when a layout is loaded, so it does not need to be duplicated in every stored path.

## Rules

1. References to items inside the same page are stored relative.
2. Global namespaces such as `Runtime/...`, `Logs/...`, and `Commands/...` remain absolute.
3. The active page provides the context root during load and resolution.

## Examples

- Preferred: `Motor/Set/Speed`
- Preferred: `Group1/ValveA/State`
- Avoid: `UdlBook/Page1/Motor/Set/Speed`
- Avoid: `Page1/Motor/Set/Speed`

## Impact

- Saved layouts become robust against page renames.
- Drag and drop or copying layout files becomes portable.
- Resolver logic must interpret relative paths against the current page.

## Open Implementation Note

The next strict step would be to move remaining page-local persistence and resolution fully from book-relative behavior to page-relative behavior wherever the current page is the intended root.