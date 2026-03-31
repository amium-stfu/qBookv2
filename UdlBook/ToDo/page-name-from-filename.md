# Aufgabe: Page-Name nicht mehr in YAML speichern, sondern aus Dateinamen ableiten

## Ziel

Das PageModel soll keinen redundanten `Name`-Eintrag mehr in der YAML-Datei enthalten.

Stattdessen gilt kuenftig:

- der technische Page-Name wird immer aus dem Dateinamen ohne `.yaml` abgeleitet
- die YAML-Datei enthaelt keinen `Name` mehr
- es gibt damit nur noch eine Quelle fuer die technische Identitaet einer Page

## Problemstellung

Aktuell existieren zwei moegliche Traeger fuer dieselbe Information:

- der Dateiname der YAML-Datei
- die Property `Name` innerhalb der YAML

Das ist problematisch, weil dadurch leicht Unklarheiten entstehen:

- Was ist fuehrend: Dateiname oder YAML-Property?
- Was passiert, wenn beides voneinander abweicht?
- Wie soll Rename technisch sauber behandelt werden?
- Welche Information wird beim Speichern geschrieben?
- Welche Information wird beim Laden vertraut?

Diese Doppelstruktur erzeugt unnoetige Komplexitaet und fuehrt spaeter sehr wahrscheinlich zu Fehlern oder Sonderfaellen.

## Empfehlung

Die technische Identitaet einer Page soll ausschliesslich aus dem Dateinamen kommen.

Konkret:

- `Name` wird aus dem YAML-Modell entfernt
- beim Laden wird `Path.GetFileNameWithoutExtension(...)` als Page-Name verwendet
- beim Speichern wird kein `Name` mehr in die YAML geschrieben
- der Dateiname ist die einzige technische Quelle fuer den Namen

## Begruendnung

Diese Variante ist sauberer, weil sie:

- nur eine Wahrheitsquelle verwendet
- Laden und Speichern vereinfacht
- Rename klar definiert
- Validierung reduziert
- Inkonsistenzen zwischen Dateiinhalt und Dateiname verhindert

## Fachliche Trennung

Es ist wichtig, zwei Dinge nicht zu vermischen:

- technischer Name bzw. Identitaet
- sichtbarer Anzeigename fuer Benutzer

Empfehlung:

- der Dateiname repraesentiert die technische Identitaet
- falls spaeter eine sichtbare Beschriftung benoetigt wird, soll diese als eigenes Feld eingefuehrt werden, zum Beispiel `Title` oder `DisplayName`

Nicht empfohlen:

- `Filename` und `Name` parallel fuer dasselbe Konzept

Empfohlen:

- `Filename` = technische Identitaet
- `Title` oder `DisplayName` = optionale Benutzeranzeige

## Zielverhalten

### Laden

Beim Laden einer Page-Datei gilt:

1. Dateipfad der YAML bestimmen
2. Dateinamen ohne Erweiterung lesen
3. diesen Wert als technischen Page-Namen setzen
4. YAML-Inhalt ohne `Name` deserialisieren

Beispiel:

- Datei: `Pages/Overview.yaml`
- resultierender Page-Name: `Overview`

### Speichern

Beim Speichern gilt:

- `Name` wird nicht in die YAML serialisiert
- der Dateiname bleibt die technische Identitaet
- wenn sich die technische Identitaet aendern soll, muss die Datei umbenannt werden

### Rename

Wenn eine Page technisch umbenannt werden soll, erfolgt das ueber den Dateinamen.

Beispiel:

- vorher: `Overview.yaml`
- nachher: `MachineOverview.yaml`
- technischer Page-Name nach Reload: `MachineOverview`

## Konkrete Regeln

### 1. Kein `Name` mehr in YAML

Die YAML-Struktur soll das Feld `Name` nicht mehr enthalten.

### 2. Name immer aus Dateinamen ableiten

Verwendung von:

```csharp
Path.GetFileNameWithoutExtension(filePath)
```

### 3. Dateiname ist technische Identitaet

Alle internen Referenzen, die sich auf die technische Page-Identitaet beziehen, muessen mit diesem abgeleiteten Namen arbeiten.

### 4. Sichtbarer Titel nur separat

Falls ein Benutzername angezeigt werden soll, dann als eigene Property mit anderer Bedeutung.

Beispiel:

```yaml
title: Maschinenuebersicht
```

Nicht als zweiter technischer Name.

## Umsetzungsaufgaben

### 1. YAML-Modell bereinigen

- `Name` aus dem serialisierten/deserialisierten Modell entfernen
- sicherstellen, dass bestehende Serialisierung `Name` nicht mehr schreibt

### 2. Loader anpassen

- technischen Namen aus dem Dateinamen ohne Erweiterung ableiten
- abgeleiteten Namen in das interne PageModel uebernehmen

### 3. Save-Pipeline anpassen

- kein `Name` mehr in die YAML ausgeben
- sicherstellen, dass Speichern nicht versucht, einen redundanten Namen zu persistieren

### 4. Rename-Handling definieren

- technische Umbenennung bedeutet Dateiumbenennung
- falls es UI fuer Rename gibt, muss diese den Dateinamen aendern und nicht ein YAML-Feld

### 5. Rueckwaertskompatibilitaet pruefen

- alte YAML-Dateien mit `Name` sollen beim Laden nach Moeglichkeit weiterhin toleriert werden
- der geladene `Name` aus YAML soll aber nicht mehr fuehrend sein
- optional kann beim naechsten Speichern das alte Feld automatisch verschwinden

### 6. Validierung ergaenzen

- leerer oder ungueltiger Dateiname
- ungueltige Zeichen fuer technische Namen
- doppelte Namen durch mehrere Dateien mit gleichem Basenamen

## Empfehlung fuer den Uebergang

Sinnvolle Einfuehrung:

1. Loader zuerst tolerant machen
2. `Name` aus YAML beim Lesen ignorieren oder nur noch fuer Altfaelle akzeptieren
3. Save-Pipeline auf neue Struktur umstellen
4. bestehende Dateien nach und nach automatisch oder manuell bereinigen

So wird der Umstieg robust, ohne alte Dateien sofort unbrauchbar zu machen.

## Nicht empfohlen

- weiterhin `Name` und Dateiname parallel zu pflegen
- beim Laden bei Abweichungen heuristisch zu raten, welcher Wert gemeint ist
- einen sichtbaren Titel wieder `Name` zu nennen, wenn der technische Name bereits aus dem Dateinamen kommt

## Klare Empfehlung

- `Name` aus der YAML entfernen
- technischen Page-Namen immer aus dem Dateinamen ableiten
- falls spaeter notwendig, einen separaten sichtbaren `Title` oder `DisplayName` einfuehren

## Beispiel

### Vorher

Datei:

```text
Pages/Overview.yaml
```

YAML:

```yaml
name: Overview
widgets:
  - ...
```

### Nachher

Datei:

```text
Pages/Overview.yaml
```

YAML:

```yaml
widgets:
  - ...
```

Resultat beim Laden:

- technischer Name = `Overview`

## Optional spaeter

Wenn die UI eine frei benennbare Beschriftung braucht:

```yaml
title: Maschinenuebersicht
widgets:
  - ...
```

Dann gilt:

- technischer Name = `Overview` aus dem Dateinamen
- sichtbarer Titel = `Maschinenuebersicht` aus YAML