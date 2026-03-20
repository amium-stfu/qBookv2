# UI Path Architecture Notes

## Ziel

Der zentrale Punkt ist nicht, dass `Book` und `Page` unbedingt selbst `Item` sein muessen.

Der eigentliche Zweck ist:

- `Path` soll automatisch und korrekt erzeugt werden
- `Path` soll nicht manuell als freier String gepflegt werden
- die `Path`-Syntax soll stabil bleiben, weil sie spaeter direkt fuer MQTT-Topics verwendet werden kann
- der User soll weiterhin explizit entscheiden, was in die UI publiziert wird, damit kein unnoetiger Overhead entsteht

## Wichtige Entscheidung

Nicht alles muss ein `Item` werden.

Stattdessen:

- `Book` und `Page` liefern den Hierarchie-Kontext
- `Item` bleibt das publizierbare UI-Objekt
- `Path` wird aus der Hierarchie abgeleitet
- `Publish(...)` passiert nur explizit

## Gewuenschtes Modell

Beispielhierarchie:

```text
Testbook
Testbook/Page1
Testbook/Page1/Sinus
Testbook/Page1/Timer
Testbook/Page1/Logs/Page1Demo
```

Oder technisch formuliert:

- `Book.Name` ist das Root-Segment
- `Page.Name` haengt unter `Book`
- `Item.Name` haengt unter `Page` oder unter einem Parent-`Item`
- `Path` entsteht automatisch aus Parent + Name

## Warum das wichtig ist

Die `Path`-Syntax ist nicht zufaellig.

Sie soll spaeter direkt als MQTT-kompatible Adressstruktur nutzbar sein, z.B.:

```text
Testbook/Page1/Sinus
Testbook/Page1/Task
Testbook/Page1/Logs/Page1Demo
```

Wenn Pfade von Hand geschrieben werden:

- entstehen Tippfehler
- entstehen Inkonsistenzen
- brechen Referenzen leichter
- ist MQTT-Topic-Generierung unzuverlaessig

Wenn Pfade automatisch erzeugt werden:

- bleibt die Struktur deterministisch
- UI und Transport koennen dieselbe Adresslogik nutzen
- Refactorings werden einfacher

## Konsequenz fuer Book und Page

`Book` und `Page` muessen nicht zu `Item` gezwungen werden.

Das ist optional und aktuell nicht der eigentliche Bedarf.

Wichtiger ist:

- `Book` ist Root-Kontext
- `Page` ist Child-Kontext
- beide helfen beim automatischen Aufbau des finalen `Path`

## Konsequenz fuer Publishing

Publishing soll **nicht automatisch** beim Erzeugen von Objekten passieren.

Das war explizit so gewuenscht, damit nur wirklich benoetigte Dinge in die UI gelangen.

Deshalb:

- Objekte koennen einen korrekten `Path` besitzen
- publiziert wird aber nur ueber explizite APIs wie `Publish(...)`

## Aktuelle Richtung fuer ProcessLog

Die neue Richtung ist:

- `ProcessLog` wird fuer die UI als `Item` unter einem stabilen Pfad publiziert
- Beispiel: `Logs/Host`, `Logs/Page1Demo`
- `EditorLogControl` loest den Log nicht mehr primaer aus einer Sonderregistry auf, sondern ueber ein `Item` in `HostRegistries.Data`

Das ist ein Schritt in Richtung eines einheitlicheren UI-Modells.

## Empfohlene naechste Architektur

Nicht:

- `Book` muss ein `Item` sein
- `Page` muss ein `Item` sein

Sondern:

1. zentrale Path-Erzeugung
2. `Book` als Root-Kontext
3. `Page` als Child-Kontext
4. `Item` als publizierbares UI-Objekt
5. explizites `Publish(...)`

## Empfohlene Regeln

### 1. Path nicht frei von aussen setzen

`Path` sollte nicht beliebig manuell zusammengebaut werden.

Besser:

- readonly
- intern gesetzt
- oder nur ueber klar definierte Builder/Attach-Methoden

### 2. Path aus Hierarchie ableiten

Beispielregel:

```text
Path = Parent.Path + "/" + Name
```

Falls kein Parent vorhanden ist:

```text
Path = Name
```

### 3. Book und Page geben Kontext

Beispiel:

- `Book.Name = Testbook`
- `Page.Name = Page1`
- `Item.Name = Sinus`

Dann wird automatisch:

```text
Testbook/Page1/Sinus
```

### 4. Publish bleibt explizit

Beispiel:

```csharp
page.Attach(item);
UiPublisher.Publish(item);
```

Oder spaeter:

```csharp
page.PublishItem(item);
```

Aber nur als expliziter Aufruf, nicht implizit beim Erzeugen.

## Sinnvolle API-Richtung

### Variante A: zentraler Path-Builder

```csharp
UiPath.ForPage(book.Name, page.Name)
UiPath.ForItem(book.Name, page.Name, item.Name)
UiPath.ForChild(parent.Path, child.Name)
```

### Variante B: Kontext ueber Parent/Attach

```csharp
page.Attach(item);
page.AttachProcessLog(log, "Page1Demo");
```

Dann berechnet `Attach(...)` intern den finalen Pfad.

## Empfehlung fuer Page

Wenn `Page` Lifecycle hat wie:

- `Initialize()`
- `Run()`
- `Destroy()`

dann ist eine spezialisierte Page-Klasse sinnvoll.

Aber:

- nicht primaer, damit `Page` ein `Item` wird
- sondern weil `Page` Verhalten/Lifecycle besitzt

Das Path-Thema ist davon getrennt.

## Kurzfassung

- `Book` und `Page` muessen nicht zwingend `Item` sein
- entscheidend ist die automatische, kanonische `Path`-Erzeugung
- `Book.Name` ist Root
- `Page.Name` dockt darunter an
- alle UI-`Item`s docken daran an
- `Publish(...)` bleibt explizit
- die Path-Struktur soll MQTT-kompatibel und stabil sein

## Gute Zielvorstellung

```text
Book = Root-Kontext
Page = Sub-Kontext
Item = publizierbares UI-Objekt
Path = automatisch aus der Hierarchie erzeugt
Publish = explizit, nicht automatisch
```
