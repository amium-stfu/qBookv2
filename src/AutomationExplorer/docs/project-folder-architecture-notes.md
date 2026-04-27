# ProjectFolderBase Architecture Notes

## Ziel

`ProjectFolderBase` ist die gemeinsame Basisklasse fuer alle Runtime-Folders.

Sie soll verhindern, dass Anwender in jeder Folder dieselben Infrastrukturteile erneut und potenziell fehlerhaft bauen muessen.

Insbesondere soll `ProjectFolderBase` zentral bereitstellen:

- den Folder-Kontext
- `Attach(...)` fuer UI-Items
- einen festen Lifecycle-Rahmen
- konsistente Initialisierung und Aufraeumlogik

## Wichtige Entscheidung

Eine konkrete Folder soll nicht mehr selbst ihren `UiFolderContext` verwalten.

Stattdessen:

- `ProjectFolderBase` besitzt intern den `UiFolderContext`
- konkrete Folders erben von `ProjectFolderBase`
- `Attach(...)` steht direkt als geschuetzte Methode zur Verfuegung

Beispiel:

```csharp
public class qFolder : ProjectFolderBase
{
    public qFolder() : base("Page1")
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

Ohne Basisklasse muss jede Folder dieselben Dinge erneut richtig machen:

- `UiFolderContext` anlegen
- `PageName` korrekt setzen
- `Attach(...)` richtig aufrufen
- Cleanup nicht vergessen
- Lifecycle-Reihenfolge einhalten

Das fuehrt leicht zu:

- Boilerplate
- Copy-Paste
- inkonsistentem Verhalten zwischen Folders
- vergessenen Dispose- oder Destroy-Schritten

Mit `ProjectFolderBase` liegt die Infrastruktur zentral an einer Stelle.

## Verantwortung von ProjectFolderBase

`ProjectFolderBase` ist nicht fuer die Fachlogik der Folder da.

`ProjectFolderBase` ist verantwortlich fuer:

- Folder-Kontext
- Attach-API
- Runtime-Lifecycle-Rahmen
- Schutz gegen doppelte oder inkonsistente Aufrufe

Die konkrete Folder ist verantwortlich fuer:

- fachliche Initialisierung
- Starten der Runtime-Logik
- Stoppen und Bereinigen eigener Ressourcen

## Lifecycle-Modell

`ProjectFolderBase` stellt drei oeffentliche Einstiegspunkte bereit:

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
4. `Destroy()` raeumt den Folder-Kontext immer auf

## Ziel der abstrakten Basisstruktur

Die abstrakten Methoden zwingen jede konkrete Folder dazu, ihren Lifecycle bewusst zu implementieren.

Damit wird verhindert, dass Anwender:

- `Initialize` vergessen
- `Destroy` leer lassen, obwohl Cleanup noetig ist
- eigene ad-hoc Lifecycle-Methoden verwenden
- den Infrastrukturrahmen umgehen

## Attach als Folder-API

`Attach(...)` ist eine geschuetzte Methode der Basisklasse.

Beispiel:

```csharp
var s1 = Attach(canBus.Items["S1"], "CanBus/S1");
UiPublisher.Publish(s1);
```

Damit gilt:

- das Runtime-Objekt liefert das Source-`Item`
- `ProjectFolderBase` haengt es in den Folder-Kontext ein
- der finale Pfad entsteht im Folder-Kontext
- `Publish(...)` bleibt explizit

## Warum Attach nicht in jeder Folder lokal gebaut werden sollte

Wenn jede Folder ihren eigenen Attach-Mechanismus erfindet, entstehen schnell Unterschiede bei:

- Path-Aufbau
- Cleanup
- Link-Verhalten
- Semantik von Source- und Attached-Items

`ProjectFolderBase` macht `Attach(...)` zur offiziellen und einheitlichen API.

## Beziehung zu den anderen Architektur-Notizen

`ProjectFolderBase` ergÃ¤nzt die bereits definierten Regeln:

- `Item` bleibt das publizierbare UI-Objekt
- `Publish(...)` bleibt explizit
- Plugin- oder Schnittstellenobjekte verwalten ihre eigenen Runtime-Items
- `ProjectFolderBase.Attach(...)` ist der Uebergang von Runtime-Item in den Folder-Kontext

## Empfohlene Regeln fuer konkrete Folders

### 1. Immer von ProjectFolderBase ableiten

Nicht:

```csharp
public class qFolder
{
}
```

Besser:

```csharp
public class qFolder : ProjectFolderBase
{
}
```

### 2. Folder-Name im Konstruktor festlegen

Beispiel:

```csharp
public qFolder() : base("Page1")
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
ProjectFolderBase = gemeinsamer Runtime-Rahmen fuer alle Folders
UiFolderContext = interne Infrastruktur der ProjectFolderBase
Attach(...) = offizieller Weg fuer Folder-gebundene UI-Items
OnInitialize / OnRun / OnDestroy = verpflichtende Lifecycle-Hooks
Publish(...) = explizit
```

## Kurzfassung

- `ProjectFolderBase` kapselt den Folder-Kontext
- `ProjectFolderBase` bringt `Attach(...)` direkt mit
- konkrete Folders implementieren nur noch abstrakte Lifecycle-Hooks
- dadurch wird Boilerplate reduziert
- Lifecycle-Fehler werden schwieriger
- `Attach(...)` wird zum einheitlichen Standard fuer Folder-gebundene UI-Items

