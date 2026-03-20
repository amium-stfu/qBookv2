# Interface Architecture Notes

## Ziel

Zukuenftige Schnittstellen-Plugins wie `CanBus`, `Modbus`, `Mqtt` oder aehnliche Komponenten sollen eigene Runtime-Objekte und eigene interne Sammlungen besitzen.

Dabei soll weiterhin gelten:

- fuer die UI gibt es genau einen zentralen Publishing-Weg
- nicht jedes Runtime-Objekt wird automatisch in die UI publiziert
- UI-Objekte bleiben `Item`s
- Schnittstellenobjekte und UI-Objekte werden sauber getrennt

## Wichtige Entscheidung

Eine Schnittstelleninstanz ist fachlich **nicht automatisch selbst ein `Item`**.

Stattdessen:

- die Schnittstelleninstanz verwaltet ihre eigenen Domain-Objekte
- diese Domain-Objekte koennen `Item`s erzeugen oder direkt als `Item`s gehalten werden
- in die UI gelangen sie nur ueber explizites `UiPublisher.Publish(...)`

## Gewuenschtes Modell

Beispiel:

```csharp
var canBus = new CanBus(...);
var myS1 = canBus.Items["S1"];

// fachliche Nutzung
...

// explizit in die UI publizieren
UiPublisher.Publish(myS1);
```

Das bedeutet:

- `CanBus` hat eine eigene kleine Registry oder Item-Sammlung
- `Page` oder andere Runtime-Komponenten holen sich daraus gezielt ein `Item`
- die zentrale UI-Registry bleibt weiterhin `HostRegistries.Data`

## Warum das wichtig ist

Wenn jede Schnittstelle direkt dieselbe globale Struktur verwaltet, entstehen spaeter schnell Probleme:

- mehrere Instanzen eines Plugins lassen sich schwer trennen
- Namen wie `S1` kollidieren zwischen verschiedenen Pages oder Bussen
- Runtime-Verantwortung und UI-Verantwortung vermischen sich
- die UI muss plugin-spezifische Sonderfaelle kennen

Mit klarer Trennung bleibt die Architektur stabil:

- Schnittstellen-Plugin = Runtime und Fachlogik
- `Item` = publizierbares UI-Objekt
- `UiPublisher.Publish(...)` = bewusste Sichtbarmachung in der UI

## Konsequenz fuer Registries

Es gibt zwei verschiedene Ebenen.

### 1. Plugin-interne Registry

Jede Schnittstelleninstanz darf intern ihre eigene kleine Registry besitzen.

Beispiel:

```csharp
public sealed class CanBus
{
    public ItemRegistry Items { get; } = new();
}
```

Diese Registry ist nicht die globale UI-Registry.

Sie dient z.B. fuer:

- Signale
- Kanaele
- Topics
- Handles
- Device-Zustaende
- zur Schnittstelle gehoerende Runtime-Objekte

### 2. Zentrale UI-Registry

Fuer die UI bleibt der Zielpunkt zentral:

```csharp
HostRegistries.Data
```

Ein Plugin publiziert nur explizit dorthin:

```csharp
UiPublisher.Publish(item);
```

Dadurch bleibt das bestehende UI-Modell einheitlich.

## Keine eigene UI-Registry pro Plugin

Jedes Schnittstellen-Plugin sollte **keine eigene separate UI-Registry** aufbauen, wenn dieselben Objekte spaeter im Editor, in Bindings oder in Controls verwendet werden sollen.

Der Grund:

- die UI muss sonst mehrere Registry-Quellen kennen
- Target-Resolution wird komplizierter
- Diagnostics und Lookup werden fragmentiert
- das einfache Modell "`TargetPath` zeigt in eine zentrale `DataRegistry`" geht verloren

Deshalb:

- interne Runtime-Registry pro Plugin: ja
- getrennte UI-Registry pro Plugin: nein

## Vererbung oder Komposition

Schnittstellen sollten im Regelfall **nicht von `Item` ableiten**.

Besser ist:

- Schnittstellenobjekt hat `Item`s
- Schnittstellenobjekt erzeugt `Item`s
- Schnittstellenobjekt publiziert `Item`s

Also:

```csharp
public sealed class CanBus
{
    public ItemRegistry Items { get; } = new();
}
```

und nicht:

```csharp
public sealed class CanBus : Item
{
}
```

## Warum keine Ableitung von `Item`

`CanBus` und `Item` haben unterschiedliche Verantwortung.

`CanBus` ist eher:

- Verbindung
- Runtime-Lifecycle
- Kommunikationslogik
- Device- oder Signalverwaltung

`Item` ist eher:

- UI-gebundene Datenrepraesentation
- publizierbarer Knoten
- stabil adressierbares Objekt fuer die UI

Wenn beides ueber Vererbung zusammengezogen wird, vermischen sich Fachlogik und UI-Modell.

## Sinnvolle Ausnahme

Eine Schnittstelleninstanz kann zusaetzlich ein eigenes Root-`Item` besitzen.

Beispiel:

```csharp
public sealed class CanBus
{
    public Item RootItem { get; }
    public ItemRegistry Items { get; } = new();
}
```

Dann koennen z.B. folgende Pfade entstehen:

```text
Testbook/Page1/CanBus
Testbook/Page1/CanBus/S1
Testbook/Page1/CanBus/S2
```

Das heisst:

- `CanBus` ist nicht selbst das `Item`
- `CanBus` besitzt aber ein Root-`Item` fuer Status, Name oder ConnectionState

## Konsequenz fuer Instanzen

Die Registry sollte an der Plugin-Instanz haengen und nicht global statisch sein.

Besser:

```csharp
var canBus = new CanBus(...);
var myS1 = canBus.Items["S1"];
```

Schlechter:

```csharp
var myS1 = CanBus.Items["S1"];
```

Eine statische globale Sammlung wird spaeter problematisch, wenn:

- mehrere `CanBus`-Instanzen existieren
- mehrere Pages dieselbe Schnittstelle nutzen
- mehrere Verbindungen parallel laufen

## Konsequenz fuer Path

Die Item-Pfade muessen eindeutig aus Kontext und Name entstehen.

Nicht gut:

```text
S1
```

Besser:

```text
Testbook/Page1/CanBus/S1
Testbook/Page2/CanBus/S1
```

Dadurch bleiben mehrere Instanzen trennbar.

## Empfohlene Regeln

### 1. Schnittstelleninstanzen besitzen eigene Runtime-Sammlungen

Beispiel:

- `canBus.Items`
- `mqtt.Topics`
- `modbus.Registers`

### 2. UI bleibt explizit

Objekte werden nicht automatisch beim Erzeugen in die UI publiziert.

Beispiel:

```csharp
var myS1 = canBus.Items["S1"];
UiPublisher.Publish(myS1);
```

### 3. Keine Pflicht zur Vererbung von `Item`

Ein Plugin-Objekt ist nicht automatisch selbst ein UI-Knoten.

### 4. Eindeutige Pfade pro Instanz

Pfadsegmente sollten den fachlichen Kontext enthalten:

- Book
- Page
- Plugin-Instanz oder Plugin-Typ
- Signal- oder Item-Name

### 5. Ein zentraler UI-Zielpunkt

Alle publizierten UI-Objekte landen in derselben Data-Registry.

## Zielbild

```text
Schnittstellen-Plugin = Runtime/Fachlogik
Plugin-Registry = interne Instanz-Sammlung
Item = publizierbares UI-Objekt
UiPublisher.Publish(...) = expliziter Uebergang in die UI
HostRegistries.Data = zentrale UI-Registry
```

## Kurzfassung

- jede Schnittstelleninstanz darf ihre eigene kleine Registry haben
- diese Registry ist nicht die globale UI-Registry
- fuer die UI bleibt `HostRegistries.Data` der zentrale Zielpunkt
- Schnittstellen sollten meist nicht von `Item` ableiten
- besser ist Komposition: Schnittstelle hat oder erzeugt `Item`s
- publiziert wird nur explizit ueber `UiPublisher.Publish(...)`
