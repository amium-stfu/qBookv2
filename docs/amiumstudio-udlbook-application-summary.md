# AmiumStudio und UdlBook: Zielbild

Diese Datei fasst den gewünschten Zielzustand der beiden Anwendungen für die weitere Arbeit zusammen.

## Grundidee

Es gibt zwei Anwendungen mit gemeinsamer technischer Basis:

- `AmiumStudio`
- `UdlBook`

Beide sollen auf denselben Editor- und Host-Bausteinen aufsetzen, aber unterschiedliche Aufgaben haben.

## Gemeinsame Architektur

Die geplante Trennung ist:

- `Amium.Host.dll`
  - Runtime, Registries, Publisher, Plugin-Anbindung, Host-Lebenszyklus
- `Amium.Editor.dll`
  - Canvas-Editor, Controls, ViewModels, Persistence, allgemeine Editor-UI
- `Amium.Roslyn.dll`
  - Book-Projekt laden, C# kompilieren, Assemblies bauen und laden

Darauf basieren die Anwendungen:

- `AmiumStudio.exe = Host + Editor + Roslyn`
- `UdlBook.exe = Host + Editor`

## AmiumStudio: Soll-Verhalten

`AmiumStudio` ist die vollständige Studio-Anwendung.

### Aufgaben

- Book-Projekte laden
- Book-Projekte per Roslyn bauen und ausführen
- Page-Definitionen und Canvas-Layouts bearbeiten
- Runtime-Zustand des geladenen Books anzeigen
- Host-Log und Diagnostik anzeigen

### Fachliche Rolle

`AmiumStudio` ist die Entwicklungs- und Integrationsumgebung.
Hier werden Books erstellt, bearbeitet, gebaut und getestet.

### Technische Anforderungen

- verwendet `Amium.Host`
- verwendet `Amium.Editor`
- verwendet `Amium.Roslyn`
- besitzt Studio-spezifische Shell-Logik für:
  - Book auswählen
  - Load/Rebuild auslösen
  - Runtime-Callbacks verarbeiten
  - Logfenster und Meldungen anzeigen

### ViewModel-Aufteilung

Der allgemeine Editor-Zustand soll nicht mit Studio-Logik vermischt sein.

- `MainWindowViewModel`
  - allgemeiner Editor-Zustand
  - Seiten, Auswahl, Canvas, Theme, EditMode, Layout-Persistence
  - keine Studio-spezifische Book- oder Build-Logik
- `AmiumStudioMainWindowViewModel`
  - Studio-spezifische Ableitung
  - `BookProjectPath`
  - `LoadBookCommand`
  - `RebuildBookCommand`
  - Meldungen, Logtext, Runtime-Reaktion

### Projektstruktur

`AmiumStudio` soll ein eigenes Projekt mit eigenem Ordner sein.

Gewünscht ist also:

- `AmiumStudio/AmiumStudio.csproj`
- Output unter `AmiumStudio/bin/...`

Nicht gewünscht ist, dass die eigentliche Studio-App nur logisch existiert, aber physisch weiter unter `UiEditor/` lebt.

## UdlBook: Soll-Verhalten

`UdlBook` ist nicht eine zweite Studio-Kopie und auch nicht eine eigene Shell mit separatem UI-Konzept.

Es soll eine leichtere Anwendung auf derselben Editor-Grundlage sein.

### Aufgaben

- dieselbe Canvas-basierte UI-Fläche wie im Editor nutzen
- dieselben Standard-Controls nutzen
- keine Roslyn-Build-Pipeline enthalten
- keine Book-Kompilierung enthalten
- keine allgemeine Code-/Projekt-Entwicklungsumgebung sein

### Datenquelle

`UdlBook` soll genau eine primäre Datenquelle haben:

- `UdlClient`

Aus dem `UdlClient` ausgewählte Items sollen in normale Controls übernommen bzw. an diese angebunden werden.

### Fachliche Rolle

`UdlBook` ist eine reduzierte Laufzeit-/Konfigurationsanwendung mit Editor-Oberfläche, aber ohne Studio-Entwicklungsfunktionen.

Anders formuliert:

- gleiche Editor-Mechanik
- deutlich weniger Shell- und Entwicklungsfunktionen
- Fokus auf UDL-Datenanbindung

### Technische Anforderungen

- verwendet `Amium.Host`
- verwendet `Amium.Editor`
- verwendet **nicht** `Amium.Roslyn`
- besitzt eine eigene kleine Shell für UDL-spezifische Workflows
- nutzt `UdlClient` als zentrale Quelle für auswählbare Datenpunkte

## Abgrenzung der beiden Anwendungen

### AmiumStudio

- vollständiges Authoring- und Runtime-Studio
- Build, Run, Rebuild, Diagnostics
- Book-zentriert
- Roslyn-basiert

### UdlBook

- reduzierte Anwendung
- kein C#-Build, kein Roslyn
- UDL-zentriert
- nutzt Editor-Controls und Canvas, aber nicht die volle Studio-Shell

## Was im letzten Stand wichtig war

Folgende Richtung wurde bereits als korrekt bestätigt:

- Host, Editor und Roslyn sind getrennte Assemblies
- `AmiumStudio` soll physisch ein eigenes Projekt sein
- Studio-spezifische Logik soll aus dem allgemeinen Editor-ViewModel herausgezogen werden
- `UdlBook` soll vorerst nicht weiter ausgebaut werden, bis `AmiumStudio` sauber getrennt ist

## Empfohlene Arbeitsreihenfolge

1. `AmiumStudio` sauber als eigene Anwendung abschließen
2. allgemeinen Editor-Zustand weiter von Studio-spezifischer Shell-Logik trennen
3. Altlasten unter `UiEditor/` bereinigen
4. danach `UdlBook` neu aufsetzen als `Host + Editor`-Anwendung ohne Roslyn

## Kurzfassung für den nächsten Chat

Wenn der nächste Chat direkt einsteigen soll, ist die Kurzform:

> `AmiumStudio` ist die volle Studio-App mit `Host + Editor + Roslyn`.
> `UdlBook` soll später eine Lite-App mit `Host + Editor` werden.
> Beide sollen denselben Canvas-Editor und dieselben Controls verwenden.
> Studio-spezifische Book-/Build-/Logik gehört nicht in das allgemeine Editor-ViewModel, sondern in eine Studio-spezifische Shell/Ableitung.
