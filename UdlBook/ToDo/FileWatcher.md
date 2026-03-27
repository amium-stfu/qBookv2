Ziel:
UdlBook soll statt einer einzelnen „Book-Datei“ ein Verzeichnis überwachen. Jede Datei in diesem Verzeichnis repräsentiert genau eine Page. UdlBook bleibt bewusst primitiv: keine zusätzliche Config, keine Pipes.

Rahmenbedingungen / Philosophie:

UdlBook ist ein sehr einfacher Host zum Anzeigen/Testen von UDL-Pages.
Keine zentrale Book-Config, keine Pipes, keine komplexen Pipelines.
Konvention statt Konfiguration:
Ein Verzeichnis entspricht einem „Buch“.
Jede Datei in diesem Verzeichnis entspricht genau einer Page.
Reihenfolge wird in der jeweiligen Page-Datei (Layout) über ein Feld wie „PageIndex“ festgelegt.
Funktionale Anforderungen:

Startverhalten
UdlBook startet mit einem konfigurierten/ausgewählten Verzeichnis (z.B. per Commandline-Arg oder UI-Auswahl).
Beim Start:
Verzeichnis vollständig scannen.
Für jede gefundene Datei:
Datei als Page laden (Layout + Code, so wie bisher eine einzelne Book-Datei verarbeitet wurde).
Entsprechende Host-Objekte / Page-Instanzen erzeugen.
Einen zugehörigen UdlClient (falls nötig) verbinden.
Page in das UI-Layout einfügen.
Reihenfolge der Pages ergibt sich aus dem PageIndex (oder einer vergleichbaren Property in der Layout-Struktur der Datei).
FileWatcher
Direkt nach dem initialen Laden wird ein FileSystemWatcher auf das Buch-Verzeichnis gesetzt.
Beobachtete Events:
Created
Changed
Deleted
Anforderungen je Event:
Created:
Prüfen, ob Datei bereits bekannt ist.
Wenn unbekannt:
Datei als neue Page laden.
Neues Page-Objekt + UdlClient anlegen.
In bestehendes Layout eingliedern (Position durch PageIndex).
Changed:
Bereits bekannte Datei:
Page neu laden/kompilieren.
Layout aktualisieren (z.B. falls sich PageIndex oder Titel ändert).
UdlClient und relevante Zustände möglichst erhalten oder sauber neu initialisieren.
Deleted:
Zu dieser Datei gehörige Page im Hostmodell finden.
Zugehörigen UdlClient sauber disconnecten.
Alle Ressourcen freigeben.
Page aus dem Layout/der UI entfernen.
Datenstrukturen / Mapping
Es soll ein zentrales Mapping geben (z.B. Dictionary mit Key = voller Dateipfad oder Dateiname), das für jede Datei den zugehörigen Page-/Context/UdlClient hält.
Über dieses Mapping müssen Change- und Delete-Events schnell die richtigen Objekte finden können.
Die UI-Reihenfolge orientiert sich an PageIndex (oder ähnlicher Eigenschaft) in der Page-Definition:
Falls zwei Pages denselben Index haben, ist ein deterministisches Fallback-Verhalten sinnvoll (z.B. dann Dateiname als Tie-Breaker).
Threading / UI-Sicherheit
FileSystemWatcher-Events laufen nicht zwingend auf dem UI-Thread.
Alle UI-Updates (Pages hinzufügen/entfernen, Layout aktualisieren) müssen über den UI-Thread/Dispatcher laufen.
Optional: leichte Entprellung (Debounce) für Save-Stürme, damit mehrfaches Change-Ereignis beim Speichern einer Datei nicht zu aggressiven Reload-Schleifen führt.
Fehlertoleranz
Wenn eine Page-Datei nicht korrekt geladen/geparst/kompiliert werden kann:
Restliche Pages dürfen nicht beeinträchtigt werden.
Für die betroffene Page ist ein Fehlerzustand zulässig (z.B. Error-View statt vollständiger Page).
Log-Ausgabe sollte den Fehler klar benennen (Datei, Ursache).
Nicht-Ziele (explizit nicht umsetzen):

Keine zentrale Book.json mit globaler Konfiguration.
Keine Pipes, keine komplexe Build-/Processing-Pipeline.
Kein umfangreiches Meta- oder TOC-Management – Reihenfolge kommt ausschließlich aus der Page-Definition (PageIndex o.Ä.).
Kein persistentes Speichern von globalem Buchzustand außer der Konvention „Verzeichnis + Dateien“.
Erwartetes Ergebnis:

Eine Implementierung in UdlBook, die:
Beim Start ein Verzeichnis als Buch lädt.
Live auf hinzugefügte, geänderte und gelöschte Dateien reagiert.
Zu jeder Datei eine Page + UdlClient verwaltet.
Bei Löschung sauber aufräumt (UdlClient disconnect, UI-Layout bereinigen).
Ohne zusätzliche Konfigurationsdateien auskommt und nur auf Konventionen (Verzeichnis + PageIndex) basiert.