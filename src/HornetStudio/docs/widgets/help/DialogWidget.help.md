# DialogWidget Help

`DialogWidget` is a normal widget used as a dialog definition. It can be placed on any screen, including a screen that is used only to organize dialogs.

Interaction rules target the widget id:

- `OpenDialog(dialogWidgetId, origin = Screen, position = Center)`
- `CloseDialog(dialogWidgetId)`

The MVP overlay placement supports centered screen overlays. Additional stored placement values are preserved for later placement behavior.
