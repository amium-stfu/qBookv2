# AmiumStudio: Aktueller Stand und nächste Schritte

Diese Datei ist als kurze Übergabe für den nächsten Chat gedacht.

## Aktueller Stand

- `AmiumStudio` existiert jetzt als eigenes Projekt unter `AmiumStudio/AmiumStudio.csproj`.
- Der neue Build läuft erfolgreich nach `AmiumStudio/bin/Debug/net9.0-windows/AmiumStudio.dll`.
- Die Lösung verweist bereits auf das neue Projekt.
- Das bisherige Wrapper-Projekt unter `UiEditor/UiEditor.csproj` wurde entfernt.

## Architekturstand

Die gewünschte Richtung ist:

- `Amium.Host.dll`
- `Amium.Editor.dll`
- `Amium.Roslyn.dll`

Anwendungsaufteilung:

- `AmiumStudio = Host + Editor + Roslyn`
- `UdlBook = Host + Editor`

## ViewModel-Stand

### Allgemeiner Editor

`UiEditor/ViewModels/MainWindowViewModel.cs` ist jetzt die allgemeinere Editor-Basis.

Dort soll nur allgemeine Editor-Funktionalität liegen, zum Beispiel:

- Pages
- Auswahl
- Canvas
- Theme
- EditMode
- Layout-Persistence

### Studio-spezifisch

`AmiumStudio/ViewModels/AmiumStudioMainWindowViewModel.cs` enthält jetzt die Studio-spezifische Logik, zum Beispiel:

- `BookProjectPath`
- `LoadBookCommand`
- `RebuildBookCommand`
- Loganzeige
- Meldungen
- Runtime-Reaktionen auf Load/Run/Destroy

## Wichtige praktische Hinweise

- Die eigentlichen Shell-Dateien liegen unter `AmiumStudio/`.
- Die Editor-Quellen liegen weiterhin physisch unter `UiEditor/` und werden vom Projekt `Editor/Amium.Editor.csproj` eingebunden.
- Die früher separat liegenden `Amium.EditorUi`-Bausteine wurden wieder in den Editor integriert; der alte Ordner `Shared/Avalonia` wurde entfernt.
- `Amium.Logging` liegt wieder unter `Host/Logging/` und wird direkt von `Amium.Host` gebaut.

## Noch offen

### 1. Demo-/Book-Referenzen weiter prüfen

Mindestens `dev/DemoBook/DemoBook.csproj` wurde bereits auf den neuen Output-Pfad umgestellt.

Es sollte noch geprüft werden, ob weitere Book-/Demo-Projekte harte Pfade auf den alten `UiEditor/bin/...`-Ort enthalten.

### 2. UdlBook noch nicht weiterziehen

`UdlBook` soll vorerst nicht weiter ausgebaut werden, bevor `AmiumStudio` vollständig sauber getrennt ist.

## Empfohlener Einstieg für den nächsten Chat

Der nächste Chat sollte am besten mit diesem Ziel starten:

> Verbleibende harte Referenzen und unnötige Altdateien prüfen, nachdem die Shell jetzt physisch unter `AmiumStudio/` konzentriert ist.

## Kurzfassung

- Neues echtes Projekt `AmiumStudio` steht.
- Build des neuen Projekts funktioniert.
- Studio-Logik ist in ein abgeleitetes ViewModel ausgelagert.
- Der alte Wrapper `UiEditor/UiEditor.csproj` ist entfernt.
- Die früher separat ausgelagerten UI-Bausteine liegen wieder direkt im Editor.
- Logging ist wieder Host-Infrastruktur unter `Host/Logging/` und kein eigenes Projekt mehr.
- `UdlBook` bleibt vorerst zurückgestellt.
