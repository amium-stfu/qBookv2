# Offline-KI für UI‑Layout‑Generierung (Szenario A)

## Ziel
Eine kleine, vollständig **offline laufende KI** integrieren, die anhand von Text‑Prompts automatisch **UI‑Layouts** in einem definierten Format (z. B. JSON, YAML oder einer DSL) erzeugt.

Die KI muss nicht allgemein intelligent sein – sie soll nur ein strukturiertes Format zuverlässig generieren.

---

## Anforderungen & Eigenschaften

- Modell läuft **komplett offline**, ohne Cloud-Abhängigkeiten.
- Generiert **strukturierten Text** (UI‑Definitionen für Layouts).
- Erkennt und reproduziert ein **klar definiertes Format**.
- Anpassung der KI durch **LoRA‑Feintuning** mit überschaubarem Aufwand.
- Trainingsumfang minimal: **100–300 Beispiele** reichen.

---

## Empfohlene Modellbasis (kleine LLMs)

Geeignet für Offline‑Anwendung:

- **LLaMA 3 – 1B oder 3B**
- **Phi‑3 Mini (1.3B)**
- **Mistral Tiny (1.6B)**

Diese Modelle:

- haben sehr geringe Speicheranforderungen (2–4 GB RAM),
- laufen auf CPU und sogar auf älteren Laptops,
- eignen sich hervorragend für strukturierten Output.

Zum lokalen Betrieb:

- **Ollama**
- **llama.cpp**
- **GGUF‑Modelle (4‑Bit)**

---

## Vorgehensweise (Workflow)

### 1. UI‑Format definieren
Ein strukturiertes Format wählen, das leicht zu erzeugen ist, z. B.:

- YAML
- JSON
- eigene DSL
- Python‑ähnliche Konfig

Wichtig: klare Syntax, deterministische Struktur.

### 2. Trainingsdaten vorbereiten
Je Beispiel:

- **Prompt** (Beschreibung des Layouts)
- **Zieldatei** (Layout in gewünschtem Format)

Beispiel: