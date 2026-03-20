# BookPage Architecture Notes

## Ziel

`BookPage` ist die gemeinsame Basisklasse fuer alle Runtime-Pages.

Sie soll verhindern, dass Anwender in jeder Page dieselben Infrastrukturteile erneut und potenziell fehlerhaft bauen muessen.

Insbesondere soll `BookPage` zentral bereitstellen:

- den Page-Kontext
- `Attach(...)` fuer UI-Items
- einen festen Lifecycle-Rahmen
- konsistente Initialisierung und Aufraeumlogik

## Wichtige Entscheidung

Eine konkrete Page soll nicht mehr selbst ihren `UiPageContext` verwalten.

Stattdessen:

- `BookPage` besitzt intern den `UiPageContext`
- konkrete Pages erben von `BookPage`
- `Attach(...)` steht direkt als geschuetzte Methode zur Verfuegung

Beispiel:

```csharp
public class qPage : BookPage
{
    public qPage() : base("Page1")
    {
    }

    protected override void OnInitialize()
    {
    }

    protected override void OnRun()
    {
        _demoCanBus ??= new DemoCanBus();
        _demoCanBusS1 ??= Attach(_demoCanBus.Items["S1"], "CanBus/S1");
    }

    protected override void OnDestroy()
    {
    }
}
```

## Warum das sinnvoll ist

Ohne Basisklasse muss jede Page dieselben Dinge erneut richtig machen:

- `UiPageContext` anlegen
- `PageName` korrekt setzen
- `Attach(...)` richtig aufrufen
- Cleanup nicht vergessen
- Lifecycle-Reihenfolge einhalten

Das fuehrt leicht zu:

- Boilerplate
- Copy-Paste
- inkonsistentem Verhalten zwischen Pages
- vergessenen Dispose- oder Destroy-Schritten

Mit `BookPage` liegt die Infrastruktur zentral an einer Stelle.

## Verantwortung von BookPage

`BookPage` ist nicht fuer die Fachlogik der Page da.

`BookPage` ist verantwortlich fuer:

- Page-Kontext
- Attach-API
- Runtime-Lifecycle-Rahmen
- Schutz gegen doppelte oder inkonsistente Aufrufe

Die konkrete Page ist verantwortlich fuer:

- fachliche Initialisierung
- Starten der Runtime-Logik
- Stoppen und Bereinigen eigener Ressourcen

## Lifecycle-Modell

`BookPage` stellt drei oeffentliche Einstiegspunkte bereit:

- `Initialize()`
- `Run()`
- `Destroy()`

Intern delegiert die Basisklasse an abstrakte Hooks:

- `OnInitialize()`
- `OnRun()`
- `OnDestroy()`

Damit ergibt sich ein klarer Rahmen:

1. `Initialize()` darf nur einmal wirksam werden
2. `Run()` sorgt bei Bedarf zuerst fuer `Initialize()`
3. `Run()` wird nicht mehrfach parallel erneut ausgefuehrt
4. `Destroy()` raeumt den Page-Kontext immer auf

## Ziel der abstrakten Basisstruktur

Die abstrakten Methoden zwingen jede konkrete Page dazu, ihren Lifecycle bewusst zu implementieren.

Damit wird verhindert, dass Anwender:

- `Initialize` vergessen
- `Destroy` leer lassen, obwohl Cleanup noetig ist
- eigene ad-hoc Lifecycle-Methoden verwenden
- den Infrastrukturrahmen umgehen

## Attach als Page-API

`Attach(...)` ist eine geschuetzte Methode der Basisklasse.

Beispiel:

```csharp
var s1 = Attach(canBus.Items["S1"], "CanBus/S1");
UiPublisher.Publish(s1);
```

Damit gilt:

- das Runtime-Objekt liefert das Source-`Item`
- `BookPage` haengt es in den Page-Kontext ein
- der finale Pfad entsteht im Page-Kontext
- `Publish(...)` bleibt explizit

## Warum Attach nicht in jeder Page lokal gebaut werden sollte

Wenn jede Page ihren eigenen Attach-Mechanismus erfindet, entstehen schnell Unterschiede bei:

- Path-Aufbau
- Cleanup
- Link-Verhalten
- Semantik von Source- und Attached-Items

`BookPage` macht `Attach(...)` zur offiziellen und einheitlichen API.

## Beziehung zu den anderen Architektur-Notizen

`BookPage` ergänzt die bereits definierten Regeln:

- `Item` bleibt das publizierbare UI-Objekt
- `Publish(...)` bleibt explizit
- Plugin- oder Schnittstellenobjekte verwalten ihre eigenen Runtime-Items
- `BookPage.Attach(...)` ist der Uebergang von Runtime-Item in den Page-Kontext

## Empfohlene Regeln fuer konkrete Pages

### 1. Immer von BookPage ableiten

Nicht:

```csharp
public class qPage
{
}
```

Besser:

```csharp
public class qPage : BookPage
{
}
```

### 2. Page-Name im Konstruktor festlegen

Beispiel:

```csharp
public qPage() : base("Page1")
{
}
```

### 3. Nur die Hooks implementieren

Also:

- `OnInitialize()`
- `OnRun()`
- `OnDestroy()`

Nicht die oeffentlichen Lifecycle-Methoden selbst neu definieren.

### 4. Attach fuer UI-Kontext verwenden

Beispiel:

```csharp
_attachedItem ??= Attach(canBus.Items["S1"], "CanBus/S1");
```

### 5. Publish bleibt explizit

Beispiel:

```csharp
UiPublisher.Publish(_attachedItem);
```

`Attach(...)` soll den Kontext setzen, aber nicht automatisch publizieren.

## Gute Zielvorstellung

```text
BookPage = gemeinsamer Runtime-Rahmen fuer alle Pages
UiPageContext = interne Infrastruktur der BookPage
Attach(...) = offizieller Weg fuer Page-gebundene UI-Items
OnInitialize / OnRun / OnDestroy = verpflichtende Lifecycle-Hooks
Publish(...) = explizit
```

## Kurzfassung

- `BookPage` kapselt den Page-Kontext
- `BookPage` bringt `Attach(...)` direkt mit
- konkrete Pages implementieren nur noch abstrakte Lifecycle-Hooks
- dadurch wird Boilerplate reduziert
- Lifecycle-Fehler werden schwieriger
- `Attach(...)` wird zum einheitlichen Standard fuer Page-gebundene UI-Items
