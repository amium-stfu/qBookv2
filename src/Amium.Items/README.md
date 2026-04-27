# Amium.Items

`Amium.Items` provides a lightweight hierarchical item model with named parameters, child items, JSON serialization helpers, cloning helpers, and path normalization utilities.

## Included Types

- `Item` represents a node in the hierarchy and exposes `Name`, `Path`, `Value`, child items, and parameters.
- `Parameter` stores a named value together with its path and last update timestamp.
- `ItemDictionary` and `ParameterDictionary` provide keyed access to child items and parameters.
- `ItemExtension` contains JSON serialization, JSON deserialization, and cloning helpers.
- `ItemPathExtensions` provides recursive path rewriting through `Repath`.

## Typical Usage

```csharp
var root = new Item("Root");
root["Motor"].Params["Speed"].Value = 1200;
root["Motor"].Params["Enabled"].Value = true;

string json = root.ToJsonString();
Item clone = root.Clone();
clone.Repath("Plant.Line1.Root");
```

## Notes

- Item paths are normalized to dot-separated segments.
- Parameters raise `Changed` when their value changes.
- Items forward parameter changes through the `Item.Changed` event.
- JSON payloads include parameter name, value, last update timestamp, and runtime type.
