# Implement Computed formula editor

## Status
In Bearbeitung

## Steps
1. inspect CustomSignals editor flow
- read dialog window code-behind and model usage
- locate existing item picker integration
- Status: completed

2. design Computed data model
- extend custom signal definition for variables, formula, and trigger
- preserve compatibility with existing computed settings
- Status: in progress

3. implement Computed evaluation logic
- parse variables and formulas
- add trigger handling scaffolding
- keep simple editor-focused behavior

4. implement Computed editor view model
- manage variables, formula text, token insertion, and validation state

5. implement Computed editor UI
- add variable list, token buttons, formula area, trigger fields, and project-aligned styling

6. wire persistence and migration
- map old operation-based computed signals into the new model where feasible

7. validate Computed implementation
- check file errors
- run build

## Observations
- Removing Constant without explicit enum values renumbered Computed from 2 to 1, causing legacy-mode normalization to misclassify Computed as Input.

## Goal
Computed-Signale sollen Variablen, Formel und Trigger unterstützen und im Editor einfach bedienbar bleiben. Komplexe Berechnungen sollen weiterhin eher über Python gelöst werden.
