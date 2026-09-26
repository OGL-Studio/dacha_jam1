# Story 004: Терминал и команды

> **Epic**: Ночная смена (MVP)
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 40 min
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4 (терминал: help, isolate, scan, patch; кулдауны)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] `help` выводит список команд с кратким описанием.
- [ ] `isolate <node>` на `isolateDuration` секунд отключает все связи ноды (пакеты перестраивают путь); повтор на той же ноде — до окончания кулдауна отказ.
- [ ] `scan` на `scanDuration` секунд раскрывает все скрытые пакеты.
- [ ] `patch <node>` лечит заражённую/упавшую ноду.
- [ ] Каждая команда имеет кулдаун из `GameData`; вызов в кулдауне пишет в лог оставшееся время и ничего не делает.
- [ ] Неизвестная команда, неверное имя ноды или лишние аргументы дают понятную ошибку, без исключений.
- [ ] Имена нод в терминале совпадают с подписями на карте (`srv-1`, `fw-2`, `core`, `gw`…); ввод регистронезависимый.
- [ ] Терминал в Unity: поле ввода с фокусом, история вывода, ↑ — предыдущая команда.

---

## Implementation Notes

- Parser + command effects live in Core (unit-tested); Unity terminal is a thin view.

---

## Out of Scope

- 005: glitched command execution.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/terminal/terminal-commands_test.cs` — advisory (waived at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 001, 002
- Unlocks: 005, 006
