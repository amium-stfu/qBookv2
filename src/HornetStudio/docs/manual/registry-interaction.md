# Registry Interaction

Registry interaction describes how data becomes visible to the application and how UI-facing systems should work with that state.

## Core Idea

Items do not become generally visible just because a producer created them.
They become visible and updateable for the rest of the application after they are submitted to the host registry path.

## Producer to Host Registry Flow

1. An internal or external producer creates or updates item data.
2. The producer submits that item state to the host registry.
3. The host registry becomes the shared path for visibility, lookup, and updates.
4. UI-facing systems observe or query the registered host state instead of maintaining a competing source of truth.

This keeps the host registry as the central path for shared item visibility.

## Visualization and Updates

If an item should be visualized, attached to widgets, or updated through UI-driven workflows, the relevant systems should work through the host registry path.

That means:

- visualization should use host-registered item state
- update paths should target host-registered items
- UI-specific lookup layers should reflect the host state instead of replacing it

## Function Registry Relationship

`FunctionRegistry` is not presented here as a child registry owned by items.
It is an independent registry for callable functionality.
Those functions may read from items, write to items, or coordinate workflows that involve items, but that does not make the function registry structurally owned by item instances.

## Handbook Guidance

When documenting workflows elsewhere in the manual:

- describe where producers submit state into the host registry
- describe UI behavior in terms of observing or updating host-registered state
- avoid implying that a UI-only registry is the primary source of truth
- avoid implying that `FunctionRegistry` belongs to item ownership

## Related Technical Sources

- `../data-flow-and-signals.md`
- `../../../HornetStudio.Host/Python/Integration/python-system-overview.md`
