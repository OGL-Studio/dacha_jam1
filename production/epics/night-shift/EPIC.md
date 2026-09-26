# Epic: Ночная смена (MVP)

> **Tier**: minimal — implicit epic synthesized from `design/game-brief.md` (no GDDs / ADRs / manifest)
> **Status**: In Progress

**Goal**: Tower defense на схеме корпоративной сети: днём инженер ИБ строит топологию и расставляет СЗИ, ночью отбивает атаки, которые с каждой сменой всё меньше похожи на человеческие, пока сеть не начинает жить своей жизнью.

**Scope**: the 7 MVP features of `design/game-brief.md`.
**Ordering**: the brief's Build order — stories below are in that sequence.

## Architecture note (jam)
- Simulation core is plain C# (`Assets/Scripts/Core/`, namespace `NightShift.Core`, no `UnityEngine`), compiled by Unity and by a `netstandard2.1` / C# 9 project so it is testable with `dotnet test` outside the editor.
- Unity layer (`Assets/Scripts/Game/`) only renders and forwards input; scenes are built from code by a bootstrap so no hand-authored scene YAML is needed.
- All tuning values live in one data file (`Assets/Scripts/Core/Data/GameData.cs`); no gameplay numbers elsewhere.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Ядро симуляции сети | Logic | Ready | N/A (minimal) |
| 002 | Карта и играбельная ночь в Unity | Integration | Ready | N/A (minimal) |
| 003 | День — строительство сети | UI | Ready | N/A (minimal) |
| 004 | Терминал и команды | Logic | Ready | N/A (minimal) |
| 005 | Пять ночей и нарастание «Out of Control» | Logic | Ready | N/A (minimal) |
| 006 | Сюжет и экраны | UI | Ready | N/A (minimal) |
| 007 | 3D-комната с монитором | Visual/Feel | Ready | N/A (minimal) |
