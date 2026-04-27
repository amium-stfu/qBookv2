# Datenfluss und Signalschicht

Dieses Dokument beschreibt den Datenfluss im Host, wie Messwerte aktualisiert werden, wie Signale zugeordnet werden und wie RealtimeChart und CsvLogger darauf aufbauen.

## 1. Basis: DataRegistry und Item-Struktur

**Kernkomponenten**

- `Amium.Items.Item`
  - Baumstruktur von Messpunkten und Gruppen.
  - Wichtige Parameter (`Item.Params[...]`):
    - `Name` – Anzeigename des Items
    - `Path` – kanonischer Pfad des Items (z.B. `UdlBook.Page1.udl1.m310.Set.Request`)
    - `Value` – aktueller Wert (dynamic)
    - `Unit` – Einheit (z.B. `V`, `°C`, `rpm`)
    - `Format` – Formatstring für Darstellung/Logging (z.B. `0.###`)
- `Amium.Host.DataRegistry` (`IDataRegistry`)
  - Zentrale Registry, in der alle Root-`Item`s registriert werden.
  - Wichtige Methoden:
    - `UpsertSnapshot(string key, Item snapshot, bool pruneMissingMembers = false)`
    - `UpdateValue(string key, object? value, ulong? timestamp = null)`
    - `TryGet(string key, out Item? value)`
  - Events:
    - `ItemChanged` – fired bei Änderungen eines Items (Snapshot, Value, Parameter).
    - `RegistryChanged` – fired bei Hinzufügen/Entfernen von Roots.

**Datenfluss bei Messwertaktualisierung**

1. Ein Treiber/Adapter (z.B. UdlClient, CAN, Simulation) aktualisiert einen Wert im Host:
   - Entweder über `UpsertSnapshot(...)` (ganze Baumstruktur) oder über
   - `UpdateValue(key, value, timestamp)` für ein konkretes Item.
2. `DataRegistry` schreibt den Wert in das passende `Item`:
   - `Item.Value` bzw. `Item.Params["Value"].Value` wird gesetzt.
   - `LastUpdate` wird aktualisiert.
3. `DataRegistry` feuert `ItemChanged` mit `DataChangeKind.ValueUpdated`.
4. Alle Abonnenten (z.B. `SignalRegistry`) können darauf reagieren.

Damit gibt es genau **eine Wahrheit** für den aktuellen Messwert: das `Item` in der `DataRegistry`.

## 2. Signalschicht (ISignal, SignalDescriptor)

Um Widgets und Skripte von der internen Item-Struktur zu entkoppeln, gibt es eine gemeinsame Signalschicht in den Contracts.

**Typen in `src/AutomationExplorer.Contracts/Signals.cs`**

- `SignalDataType`
  - `Unknown`, `Boolean`, `Integer`, `Float`, `String`, `Object` – abgeleitet aus dem aktuellen Wert.
- `SignalDescriptor`
  - Metadaten eines Signals:
    - `Id` – eindeutige Signal-ID (aktuell: identisch zum `SourcePath`).
    - `Name` – Anzeigename des Signals.
    - `DataType` – abgeleiteter Datentyp.
    - `Unit` – Einheit (z.B. `V`, `°C`, …).
    - `Format` – Anzeige-/Logformat (z.B. `0.###`).
    - `SourcePath` – kanonischer Pfad zur Datenquelle im Host (Item-Pfad). Wichtiger Anker für Mapping.
    - `IsWritable` – ob der Wert schreibbar sein soll.
    - `Category` – z.B. `UdlModule`, `Camera`, … (aus `Item.Params["Kind"]`).
- `SignalValueChangedEventArgs`
  - Enthält `Descriptor`, `OldValue`, `NewValue`, `Timestamp`.
- `ISignal`
  - Abstraktion eines Signals:
    - `SignalDescriptor Descriptor { get; }`
    - `object? Value { get; set; }`
    - `event EventHandler<SignalValueChangedEventArgs>? ValueChanged`
- `ISignalRegistry`
  - Zentrale Sicht auf alle Signale:
    - `event EventHandler<SignalValueChangedEventArgs>? SignalChanged`
    - `IReadOnlyCollection<SignalDescriptor> GetAllDescriptors()`
    - `bool TryGetById(string id, out ISignal? signal)`
    - `bool TryGetBySourcePath(string sourcePath, out ISignal? signal)`

## 3. Host-Implementierung: SignalRegistry

Im Host gibt es eine konkrete Implementierung, die auf `DataRegistry` aufsetzt.

**SignalRegistry in `src/AutomationExplorer.Host/SignalRegistry.cs`**

- `SignalRegistry : ISignalRegistry`
  - Konstruktor: `new SignalRegistry(IDataRegistry dataRegistry)` – registriert sich bei `dataRegistry.ItemChanged`.
  - Interne Maps:
    - `_signalsBySourcePath : ConcurrentDictionary<string, DataRegistrySignal>`
    - `_signalsById : ConcurrentDictionary<string, DataRegistrySignal>`

- `DataRegistrySignal : ISignal`
  - Hält Referenz auf `IDataRegistry` und den `sourcePath`.
  - Liest initial den aktuellen Wert aus dem zugehörigen `Item`.
  - `Value`-Setter schreibt über `dataRegistry.UpdateValue(sourcePath, value, null)` zurück.
  - `OnSourceValueUpdated(object? newValue)` aktualisiert den Cache und feuert `ValueChanged`.

**Erzeugung von SignalDescriptor aus Item**

Bei `TryGetBySourcePath(sourcePath, out signal)` passiert:

1. `DataRegistry.TryGet(sourcePath, out Item? item)` – das Item wird über den Pfad geholt.
2. Aus dem Item werden Metadaten gelesen:
   - `name = item.Name ?? sourcePath`
   - `unit = item.Params["Unit"].Value?.ToString()` (falls vorhanden)
   - `format = item.Params["Format"].Value?.ToString()` (falls vorhanden)
   - `value = item.Params["Value"].Value`
3. Der Datentyp wird mit `InferDataType(value)` auf `SignalDataType` gemappt.
4. `SignalDescriptor` wird erzeugt mit:
   - `Id = sourcePath`
   - `SourcePath = sourcePath`
   - `Unit`, `Format`, `Category`, `IsWritable`.

**Eventfluss bei Wertänderung**

1. Treiber ruft `DataRegistry.UpdateValue(key, value, timestamp)` auf.
2. `DataRegistry` aktualisiert das `Item` und feuert `ItemChanged` mit `DataChangeKind.ValueUpdated`.
3. `SignalRegistry.OnDataRegistryItemChanged(...)` prüft, ob es ein `DataRegistrySignal` für `e.Key` gibt.
4. Falls ja:
   - Liest aktuellen Wert aus `e.Item.Params["Value"].Value` bzw. `e.Item.Value`.
   - Ruft `signal.OnSourceValueUpdated(currentValue)` → `ISignal.ValueChanged`.
   - Feuert zusätzlich `SignalRegistry.SignalChanged` mit demselben Descriptor.

## 4. Zugriff über HostRegistries

`HostRegistries` bündelt im Host zentrale Registries:

- `public static IDataRegistry Data { get; }` – bestehende Datenregistry.
- `public static ISignalRegistry Signals { get; }` – neue Signalschicht.

Initialisierung im static-Konstruktor:

- `Data = new DataRegistry();`
- `Signals = new SignalRegistry(Data);`

Damit haben alle Host- und UI-Komponenten einen einheitlichen Zugriffspunkt auf Daten und Signale.

## 5. Zuordnung von Signalen (Mapping)

### 5.1. Vom UI-TargetPath zum Item (bestehendes Verhalten)

UI-Controls (Signals, Buttons, Charts, CsvLogger, etc.) arbeiten mit `TargetPath`-Strings, die im Layout konfiguriert sind.

Beispiele:

- `UdlBook/Page1/udl1/m310/Set/Request`
- `this` (relativ zur aktuellen Seite/Folder)

Auflösung:

1. `TargetPathHelper.EnumerateResolutionCandidates(targetPath, pageName)` erzeugt Kandidatenpfade:
   - Berücksichtigt Projektroot-Präfixe (z.B. `UdlBook/...`).
   - Berücksichtigt Seiten-/Folderkontext.
2. Für jeden Kandidatenpfad:
   - `TryGetMatchingRegistryItem(candidatePath, out item)` versucht eine direkte Zuordnung zu einem Registry-Key.
   - Falls kein direkter Treffer:
     - Sucht einen passenden Root-Key in `HostRegistries.Data.GetAllKeys()`.
     - Wenn `candidatePath` ein Descendant dieses Keys ist, wird der relative Pfad aufgelöst.
3. Ergibt insgesamt ein `Item`, das den Wert und Metadaten enthält.

### 5.2. Vom Item zum Signal

Sobald ein `Item` aufgelöst ist, wird für signalbasierte Features ein `SourcePath` verwendet:

- `sourcePath = item.Path ?? targetPath;`
  - `item.Path` ist der kanonische Pfad aus den Item-Parametern.
  - Fallback: der ursprüngliche `TargetPath` aus der Konfiguration.

Dann wird versucht, ein Signal zu holen:

- `HostRegistries.Signals.TryGetBySourcePath(sourcePath, out var signal)`

Ergebnis:

- Wenn ein Signal existiert (oder neu erzeugt werden kann), steht eine einheitliche Sicht mit Descriptor (Unit, Format, SourcePath) und `Value`-Property zur Verfügung.
- Wenn kein Signal erzeugt werden kann, fällt der Code auf Item-basierte Pfade zurück (z.B. direkt `Item.Value`).

## 6. RealtimeChart – Zuordnung und Datenfluss

Der RealtimeChart arbeitet aktuell noch **Item-basiert**, nutzt aber dieselben TargetPath-/Auflösungsregeln wie andere Controls.

### 6.1. Konfiguration der Serien

- `FolderItemModel.ChartSeriesDefinitions` enthält pro Zeile eine Definition:
  - Format: `TargetPath|YAxis|Style`
  - Beispiel: `UdlBook/Page1/udl1/m310/Set/Request|Y1|Line`
- `ParseSeriesDefinitions(...)` erzeugt daraus `ChartSeriesConfiguration`:
  - `TargetPath` – normalisiert mit `TargetPathHelper.NormalizeConfiguredTargetPath`.
  - `PageName` – Seite, auf der der Chart liegt.
  - `AxisIndex` – 1..4 (Y1..Y4).
  - `ConnectStyle` – `Line`, `Step`, `StepVertical`.

### 6.2. Auflösung zum Item

Für jede Serie wird in `TryResolveSeriesItem(targetPath, pageName, out Item? item)` derselbe Mechanismus wie oben verwendet:

1. `TargetPathHelper.EnumerateResolutionCandidates(targetPath, pageName)` erzeugt Kandidatenpfade.
2. `TryGetMatchingRegistryItem(...)` sucht ein passendes Root-Item in `HostRegistries.Data`.
3. Wenn nötig, wird über relative Pfade der Kindknoten gesucht.

Ergebnis: Ein `Item`, dessen `Value` im Chart verwendet wird.

### 6.3. Abtastung und History

- `ChartRuntimeState` verwaltet die Samples:
  - Pro Serie: Liste von `(Timestamp, Value)`-Punkten.
  - `HistorySeconds` bestimmt, wie lange Daten vorgehalten werden.
- Timer-basiertes Sampling:
  - `_sampleTimer` ruft periodisch `SampleCurrentValues()` auf.
  - Für jede Serie:
    - `TryResolveSeriesItem(...)` → `Item`.
    - `TryResolveNumericValue(item, out double value)` – konvertiert `Item.Value` auf `double`.
    - Fügt `(now, value)` in die Timeserie ein.
  - Ältere Punkte werden über `TrimSeriesLocked(now)` verworfen.
- Die Visualisierung (`RenderPlot`) liest Snapshots dieser Punkte und stellt sie im ScottPlot dar.

> Hinweis: Der RealtimeChart verwendet aktuell noch direkt `Item` und `Item.Value`. Die neue Signalschicht ermöglicht es später, hier ebenfalls auf `ISignal` umzubauen, ohne die TargetPath-Logik zu ändern.

## 7. CsvLogger – Zuordnung und Datenfluss

Der CsvLogger wurde so erweitert, dass er bevorzugt über `ISignal` arbeitet und bei Bedarf auf das bisherige Item-Verhalten zurückfällt.

### 7.1. Konfiguration der zu loggenden Signale

- Im CsvLogger-Widget (`FolderItemModel`) steht `CsvSignalPaths` als Textfeld zur Verfügung.
- Parsing in `ParseCsvSignalSelection(FolderItemModel item)`:
  - Erlaubte Formate pro Zeile:
    - `Path`

## 8. EnhancedSignals – Erster Kalman-Versuch

Die Enhanced-Signal-Runtime unterstützt im ersten Wurf einen optionalen Kalman-Pfad, ohne die bestehenden Filterpfade `Raw`, `Average`, `EMA`, `WMA` und `EMAWMA` fachlich zu verändern.

### 8.1. Aktivierung und Grundidee

- Kalman wird über zusätzliche flache Konfigurationsfelder an einer `ExtendedSignalDefinition` aktiviert.
- Wenn `KalmanEnabled = true` ist, verwendet die Runtime für dieses Signal den Kalman-Pfad als alternative Stage-2-Berechnung.
- Die klassischen Filtermodi bleiben im Code unverändert und werden nur dann weiter genutzt, wenn Kalman deaktiviert ist.

### 8.2. Teach-Mode

- Der erste Teach-Wurf lernt nur die Messrausch-Varianz `R`.
- `Q` wird nach Abschluss der Teach-Session konservativ aus `R * KalmanTeachQFactor` abgeleitet.
- Wenn `KalmanTeachPauseOnDynamic = true` ist, pausiert der Teach-Mode bei erkannter Dynamik, damit Rampen nicht als Rauschen fehlinterpretiert werden.
- Wenn `KalmanTeachAutoApply = true` ist, werden die gelernten Werte einmalig am Ende der Teach-Session in die gespeicherte Enhanced-Signal-Definition zurückgeschrieben.

### 8.3. Runtime-Steuerung

Unter dem veröffentlichten Filtermodul wird ein `Kalman`-Zweig publiziert. Dort stehen Diagnosewerte wie Schätzwert, Gain, Innovation, Teach-Status und die aktuellen `R`-/`Q`-Werte zur Verfügung.

### 8.4. Adaptive Q

- Optional kann der Kalman-Pfad ein adaptives `Q` verwenden, ohne die bestehenden klassischen Filterpfade zu verändern.
- `KalmanDynamicQMin` ist der Ruhewert. Wenn keine relevante Dynamik erkannt wird, läuft der Kalman mit diesem `Q`.
- `KalmanDynamicQMax` ist der Zielwert bei starker Dynamik.
- Wenn `KalmanDynamicQEnabled = true` ist, verwendet der Kalman-Pfad seine eigene Dynamic-Analyse und berechnet daraus eine kontinuierliche Intensität zwischen Ruhe und voller Dynamik.
- Berechnung:
  - Kalman-Dynamic-Erkennung: Regressionsgerade über das Kalman-exklusive Analysefenster `KalmanDynamicDetectionWindowMs`
  - Relative Fensteränderung: Betrag der Regressionsänderung über das Fenster, normiert auf einen robusten Referenzbetrag aus dem Fenster
  - Normierungsmodus: `KalmanDynamicNormalizationMode` steuert den Nenner der Kalman-Winkelberechnung
  - `HybridReferenceFloor` ist der Default und entspricht dem aktuell bewährten Verhalten mit `KalmanDynamicReferenceFloor`
  - `PureResidual` normiert nur gegen das RMS-Residual der Regressionsgeraden und misst damit Trend relativ zum Rest-Rauschen
  - `AdaptiveResidualBlend` mischt Referenz-Floor und Residual mit `KalmanDynamicResidualWeight` von `0 .. 1`
  - Stabilisierung nahe `0`: Im Default-Modus `HybridReferenceFloor` wird der konfigurierbare Referenz-Floor `KalmanDynamicReferenceFloor` verwendet. Dadurch führt ein sehr kleiner Signalbetrag nicht mehr allein durch einen fast verschwindenden Nenner zu künstlich hohen Winkeln.
  - Dynamic-Winkel: `angle = atan(relativeChange) * 180 / PI`
  - Operativer Dynamic-Winkel: trendqualitätsgewichtete und geglättete Version des Rohwinkels, damit statisches Rauschen nahe `0°` bleibt und Trigger/Adaptive-Q nicht fast binär springen
  - Untere Grenze: konfigurierte Kalman-Winkelschwelle `KalmanDynamicAngleThresholdDeg`
  - Obere Grenze: konfigurierbares `KalmanDynamicAngleMaxDeg`; `45°` entsprechen ungefähr 100 % relativer Änderung über das Analysefenster
  - Intensität: Interpolation von `0 .. 1` zwischen Winkelschwelle und oberer Grenze, gewichtet mit der Trendqualität der Regression
  - Effektives `Q`: Interpolation von `KalmanDynamicQMin .. KalmanDynamicQMax`
- Mit `KalmanDynamicQHoldMs` läuft die adaptive Intensität weich aus, nachdem die untere Dynamikgrenze wieder unterschritten wurde. Das verhindert hartes Flattern an der Schaltschwelle.
- Der klassische Dynamic-Filter der normalen Stage-2-Filter bleibt davon getrennt und arbeitet weiter mit seinem eigenen slope-basierten Dynamic-Fenster und den klassischen Slope-Schwellen.
- Im `Dynamic`-Zweig stehen auswählbare Live-Werte wie `Active`, `AngleDeg`, `RawAngleDeg`, `RelativeChange`, `ReferenceValue` und `RemainingHoldMs` zur Verfügung.
- Diagnosewerte im `Kalman`-Zweig zeigen unter anderem `EffectiveProcessNoiseQ`, `AdaptiveQIntensity`, `DynamicAngleDeg`, `DynamicRawAngleDeg`, `DynamicSlopeRatio` (relative Änderung), `DynamicReferenceValue`, `DynamicMaxAngleDeg` und `DynamicTrendConfidence`.

### 8.5. Statistics

- Optional kann ein EnhancedSignal einen `Statistics`-Zweig publizieren.
- Die Statistikberechnung arbeitet auf der vorhandenen Sample-Historie der Runtime. `Min`, `Max` und `Average` verwenden das aktuell retained sample window.
- `Min` und `Max` publizieren jeweils den Wert selbst sowie ein SubItem `TimeStamp` als Unix-Zeitstempel in Millisekunden.
- `StdDev` verwendet ein eigenes konfigurierbares Zeitfenster `StdDevWindowMs`, damit die Rauschbewertung nicht an die Hauptfilterzeit gekoppelt bleiben muss.
- `Integral` wird zeitbasiert aus den echten Zeitdeltas zwischen akzeptierten Samples kumulativ seit Statistikstart bzw. `Statistics.Reset` berechnet und danach durch `IntegralDivisorMs` geteilt. Damit lassen sich z.B. Durchflusswerte in `l/min` ueber einen Divisor `60000` in ein Volumen in `l` umrechnen.
- Unter `Statistics.Params` stehen die aktiven Publish-Flags sowie `RetentionWindowMs`, `StdDevWindowMs` und `IntegralDivisorMs` fuer Diagnose und Nachvollziehbarkeit zur Verfuegung.
- `Statistics.Reset` ist ein Bool-Trigger. Ein Schreibzugriff mit `true` setzt nur die Statistik lokal zurueck, ohne die gemeinsame Sample-Historie der restlichen Filterpfade zu loeschen, und springt danach automatisch wieder auf `false`.

Der Teach-Mode wird ueber `Project.<Folder>.EnhancedSignals.<SignalName>.Kalman.Request` gesteuert:

- `StartTeach`
- `StopTeach`
- `ResetKalman`

Diese erste Version ist absichtlich minimal gehalten, damit sie testbar bleibt und keine Regressionsänderungen an den bestehenden klassischen Filterpfaden verursacht.
    - `Name|Path`
    - `Name|Path|Unit`
  - Rückgabe: Liste von Tupeln `(DisplayName, TargetPath, Unit)`.

### 7.2. Starten des CsvLoggers

In `StartCsvLogging(FolderItemModel item)` passiert pro Zeile:

1. `TargetPath` wird aus `CsvSignalPaths` gelesen.
2. `pageName` = aktuelle Seite.
3. Auflösung zum `Item`:
   - `TryResolveDataItem(targetPath, pageName, out var dataItem)`.
4. Bestimmen eines `sourcePath`:
   - `sourcePath = dataItem.Path ?? targetPath;`
5. Versuch, ein Signal zu verwenden:
   - `HostRegistries.Signals.TryGetBySourcePath(sourcePath, out var signal)`.

### 7.3. Logging via ISignal (bevorzugt)

- Wenn ein `ISignal` gefunden wurde:
  - `logger.AddSignal(signal, string.Empty, displayName, unit);`
- In `CsvLogger.AddSignal(ISignal signal, ...)`:
  - `name` = `signal.Descriptor.Name`.
  - `unit` = Override aus UI oder `signal.Descriptor.Unit`.
  - `format` = Override oder `signal.Descriptor.Format`.
  - `GetLogObject` liest immer `signal.Value` in Echtzeit.

Vorteile:

- Einheitliche Metadaten (Unit, Format, SourcePath) im CSV-Header.
- Gleiche Definition kann später von Skripten, RealtimeChart usw. wiederverwendet werden.

### 7.4. Fallback: Logging via Item

Falls kein `ISignal` gefunden wird (z.B. für spezielle/temporäre Items):

- `logger.AddItem(dataItem, string.Empty, displayName, unit);`
- Verhalten wie bisher:
  - `CsvLogger` liest `item.Params["Value"].Value` beim Sampling.
  - `Unit`/`Format` kommen aus `Item.Params`.

Damit ist das System kompatibel zu bestehenden Layouts, nutzt aber dort, wo verfügbar, die neue Signalschicht.
