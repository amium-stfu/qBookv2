# Aufgabe: Widget-Referenzen von Name/Path auf stabile Ids umstellen

## Ziel

Widget-Referenzen sollen kuenftig nicht mehr technisch am editierbaren `Name` oder dem daraus abgeleiteten `Path` haengen.

Stattdessen gilt:

- jedes Widget behaelt seine stabile `Id` als technische Identitaet
- YAML bleibt fuer Menschen lesbar
- lesbare Pfade wie `PythonClients/Raw/raw_c` bleiben erhalten
- die eigentliche Aufloesung erfolgt aber ueber stabile Referenzfelder auf Basis der Widget-Id
- Rename eines Widgets darf bestehende Referenzen nicht mehr kaputt machen

## Problemstellung

Aktuell werden viele Referenzen ueber lesbare Pfade gespeichert.

Beispiel:

```yaml
Properties:
  Uri: 'PythonClients/Raw/raw_c'
```

Das ist gut lesbar, aber technisch instabil, weil `Raw` vom Widget-Namen kommt.

Wenn sich `Widget.Name` aendert:

- wird der technische Pfad anders
- bestehende YAML-Referenzen zeigen ins Leere
- Picker und DropDowns muessen umbenannte Ziele erneut finden
- InteractionRules, Signal-Targets und Chart-Serien koennen brechen

Die eigentliche Ursache ist:

- `Name` ist heute gleichzeitig Anzeige und technische Identitaet
- `Path` wird als lesbarer Schluessel missbraucht

## Zielbild

Es soll kuenftig zwei Ebenen geben:

- lesbare Persistenzwerte fuer Mensch, Diff und Debugging
- stabile technische Referenzen fuer die Aufloesung zur Laufzeit und im Editor

Das bedeutet:

- `Uri` bleibt als lesbarer Anzeige- und Persistenzwert erhalten
- zusaetzlich werden stabile Referenzfelder gespeichert
- beim Laden und Aufloesen wird zuerst ueber Id gearbeitet
- `Uri` wird aus der aktuellen Aufloesung automatisch erzeugt oder aktualisiert

## Empfehlung fuer Signal-Targets

Fuer Signal-Widgets und aehnliche Datenreferenzen wird folgende Struktur empfohlen:

```yaml
Properties:
  Unit: 'V'
  Uri: 'PythonClients/Raw/raw_c'
  SourceWidgetId: '16a3196adee44f7683132f47c4bc2a2a'
  SourceValueName: 'raw_c'
  Parameter: 'Value'
  Format: ''
  IsReadOnly: false
  RefreshRateMs: '1000'
```

Bedeutung:

- `Uri`: lesbarer generierter Pfad
- `SourceWidgetId`: stabile technische Referenz auf das erzeugende Widget
- `SourceValueName`: der konkrete Value-Kanal innerhalb dieses Widgets

## Empfehlung fuer InteractionRules

Fuer InteractionRules mit Widget-Zielen soll dieselbe Trennung gelten.

Beispiel fuer ApplicationExplorer-Funktionen:

```yaml
InteractionRules:
  -
    Event: 'BodyLeftClick'
    Action: 'InvokePythonFunction'
    TargetPath: 'ApplicationExplorer:Log'
    TargetWidgetId: '16a3196adee44f7683132f47c4bc2a2a'
    TargetMember: 'Log'
    FunctionName: 'write_host_log'
    Argument: 'Hallo'
```

Dabei gilt:

- `TargetPath` bleibt lesbar
- `TargetWidgetId` ist die technische Referenz
- `TargetMember` beschreibt den Unterpunkt, z.B. Env-Name oder Value-Name

Die genaue Feldbenennung kann noch vereinheitlicht werden, aber das Muster sollte gleich bleiben.

## Generierungsregel fuer Uri und TargetPath

`Uri` oder andere lesbare Target-Strings sollen nicht mehr die einzige Wahrheit sein.

Stattdessen:

### Beim Speichern

- den aktuellen Zielpfad aus der aufgeloesten Referenz generieren
- lesbaren String in YAML schreiben
- gleichzeitig stabile Referenzfelder mitschreiben

### Beim Laden

- zuerst ueber die stabilen Referenzfelder aufloesen
- falls diese fehlen oder nicht aufloesbar sind, auf den lesbaren Pfad als Fallback gehen

### Beim Rename

- keine technischen Referenzen umschreiben muessen
- nur lesbare Strings beim naechsten Speichern neu generieren

## Vorteile dieses Modells

- Rename-sicher
- YAML bleibt lesbar
- Rueckwaertskompatibel einfuehrbar
- Debugging bleibt moeglich
- Editor-UI kann weiterhin mit lesbaren Namen arbeiten

## Auswirkungen auf Picker und DropDown Menus

Die Umstellung betrifft nicht nur das Speichern, sondern auch alle Auswahldialoge.

Aktuell zeigen Picker und DropDowns meist einen lesbaren Zielpfad an und speichern denselben Wert direkt.

Kuenftig muessen sie mit einer zweigeteilten Struktur umgehen:

- Anzeige: lesbarer Name oder Pfad
- gespeicherter technischer Kern: Widget-Id plus Member

### Anforderungen an Picker

Picker muessen fuer jede auswählbare Option mindestens folgende Informationen intern fuehren:

- `DisplayText`
- `WidgetId`
- `MemberName` oder `TargetMember`
- `GeneratedPath`

Beispiel fuer eine interne Picker-Option:

```text
DisplayText: Raw -> raw_c
WidgetId: 16a3196adee44f7683132f47c4bc2a2a
MemberName: raw_c
GeneratedPath: PythonClients/Raw/raw_c
```

### Anforderungen an DropDown Menus

DropDowns sollen weiterhin lesbar bleiben.

Sie sollen:

- den lesbaren Pfad oder Anzeigenamen darstellen
- intern aber die stabile Referenz des gewaehlten Elements kennen
- bei Umbenennung nach Reload automatisch weiterhin das richtige Ziel selektieren koennen

### Anforderungen an bestehende Editor-Felder

Alle Editor-Felder, die bisher nur einen String speichern, muessen geprueft werden, insbesondere:

- `Uri`
- `TargetPath`
- `ChartSeriesDefinitions`
- `InteractionRules`
- PythonClient-Targets
- ApplicationExplorer-Targets

Je nach Feld braucht es:

- zusaetzliche Persistenzfelder
- interne ViewModel-Optionen mit `Display + StableRef`
- neue Mapping-Helper fuer Laden, Anzeigen und Speichern

## Betroffene Bereiche im System

### 1. YAML-Loader

Loader muessen neue Referenzfelder lesen koennen und alte YAML-Dateien weiterhin tolerieren.

### 2. Save-Pipeline

Speichern muss lesbare Pfade neu generieren und stabile Referenzfelder mitschreiben.

### 3. ViewModels und EditorDialog-Felder

Die Editor-Logik darf nicht mehr nur String-Targets kennen.

Sie braucht interne Referenzmodelle oder mindestens Hilfsstrukturen fuer:

- Anzeige
- Auswahl
- Aufloesung
- Serialisierung

### 4. Target-Helper

Es braucht zentrale Helper, die zwischen diesen Welten uebersetzen:

- Widget-Id + Member -> lesbarer Pfad
- lesbarer Pfad -> moegliche Rueckwaertskompatibilitaet
- gespeicherte Referenz -> Runtime-Ziel

### 5. Runtime-Aufloesung

Zur Laufzeit soll immer die stabile Referenz bevorzugt werden.

Wenn nur alte Daten vorliegen, kann der lesbare Pfad als Fallback dienen.

## Empfohlene technische Struktur

Statt in vielen Stellen rohe Strings direkt weiterzureichen, sollte es ein kleines internes Referenzmodell geben.

Beispiel:

```csharp
WidgetReference
    WidgetId
    MemberName
    DisplayPath
```

Optional spaeter spezialisiert:

- `SignalSourceReference`
- `InteractionTargetReference`
- `PythonValueReference`

Wichtig ist weniger der konkrete Klassenname als die klare Trennung von:

- technischer Identitaet
- lesbarer Anzeige
- generiertem Pfad

## Rueckwaertskompatibilitaet

Die Umstellung sollte tolerant eingefuehrt werden.

### Alte Dateien

Alte YAML-Dateien enthalten nur:

- `Uri`
- `TargetPath`
- Strings in `ChartSeriesDefinitions`

Diese muessen weiterhin lesbar bleiben.

### Uebergangsregel

Beim Laden gilt:

1. wenn neue stabile Referenzfelder vorhanden sind, diese verwenden
2. sonst alten Pfad lesen und versuchen, daraus das Ziel wie bisher aufloesen
3. beim naechsten Speichern die neue Struktur schreiben

So koennen bestehende Projekte ohne harte Migration weiterlaufen.

## Konkrete Umsetzungsaufgaben

### 1. Referenzschema definieren

- Benennung der neuen YAML-Felder festlegen
- fuer Signal-, Chart- und Interaction-Ziele vereinheitlichen, wo sinnvoll

### 2. Zentrale Referenz-Helper einfuehren

- Generierung lesbarer Pfade aus Id + Member
- Aufloesung ueber Id
- Fallback ueber Alt-Pfad

### 3. Save-Pipeline erweitern

- `Uri` weiter schreiben
- stabile Referenzfelder zusaetzlich schreiben
- `TargetPath` analog behandeln

### 4. Loader erweitern

- neue Felder lesen
- alte Dateien ohne neue Felder weiterhin akzeptieren

### 5. Picker und DropDown Menus umstellen

- Optionsmodell um technische Referenzen erweitern
- Anzeige getrennt von gespeicherten Referenzen behandeln
- Selektion auch nach Rename ueber Id wiederfinden

### 6. InteractionRules anpassen

- PythonClient- und PythonEnv-Ziele auf stabile Referenzen erweitern
- lesbare Anzeige behalten

### 7. Chart- und Serienreferenzen anpassen

- nicht mehr nur `PythonClients/Raw/raw_a|Y1`
- zusaetzlich stabile Quellen speichern oder intern ableiten

### 8. Rename-Verhalten definieren

- Rename soll keine technischen Referenzen mehr brechen
- nach Rename sollen generierte lesbare Pfade beim naechsten Speichern aktualisiert werden

### 9. Testszenarien definieren

- Widget umbenennen
- YAML speichern und neu laden
- Copy/Paste von Widgets
- Duplizieren von Widgets mit neuer Id
- InteractionRules nach Rename
- Signal- und Chart-Targets nach Rename

## Besondere Faelle

### Copy/Paste und Duplizieren

Wenn Widgets kopiert oder dupliziert werden, erhalten sie in der Regel neue Ids.

Dabei muss klar definiert sein:

- welche internen Referenzen innerhalb des kopierten Blocks auf die neuen Ids umgebogen werden
- welche externen Referenzen bewusst auf das urspruengliche Ziel zeigen sollen

Das darf nicht dem Zufall ueberlassen werden.

### Nicht aufloesbare Ziele

Wenn eine Id nicht mehr gefunden wird:

- lesbaren Pfad weiter anzeigen
- Problem im Editor sichtbar machen
- optional Neuverknuepfung ueber Picker ermoeglichen

## Nicht empfohlen

- weiterhin `Path` oder `Name` als eigentliche technische Identitaet zu verwenden
- nur GUIDs ohne lesbaren Pfad in YAML zu speichern
- Picker und DropDowns nur auf Basis von Anzeige-Strings zu betreiben
- Rename per globalem String-Ersetzen zu loesen

## Klare Empfehlung

- technische Referenzen auf Basis der stabilen Widget-Id einfuehren
- lesbare Pfade in YAML behalten
- lesbare Pfade automatisch generieren
- Picker und DropDown Menus auf ein internes Modell mit `Display + StableRef` umstellen
- alte YAML-Dateien uebergangsweise weiter unterstuetzen

## Beispiel fuer das Zielmodell

### Vorher

```yaml
Properties:
  Uri: 'PythonClients/Raw/raw_c'
```

### Nachher

```yaml
Properties:
  Uri: 'PythonClients/Raw/raw_c'
  SourceWidgetId: '16a3196adee44f7683132f47c4bc2a2a'
  SourceValueName: 'raw_c'
```

Die Id ist technisch fuehrend.

`Uri` bleibt lesbar und wird aus der aktuellen Referenz generiert oder aktualisiert.