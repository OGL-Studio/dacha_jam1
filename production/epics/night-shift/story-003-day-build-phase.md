# Story 003: День — строительство сети

> **Epic**: Ночная смена (MVP)
> **Status**: In Progress
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 45 min
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1 (постановка и соединение нод за кредиты) + 3 (апгрейд СЗИ)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] Днём доступна панель магазина: Сервер, Firewall, IDS, Honeypot — с ценами.
- [ ] Выбор ноды в магазине + клик по пустой клетке ставит ноду и списывает цену; клик по занятой клетке ничего не делает.
- [ ] Перетаскивание от ноды к ноде создаёт связь (цена по длине); ПКМ по связи удаляет её с частичным возвратом.
- [ ] Клик по СЗИ показывает кнопку апгрейда до ур.2; без денег кнопка неактивна.
- [ ] Кнопка «Начать смену» переводит в ночь; после отчёта смены наступает следующий день с сохранённой сетью.
- [ ] Недоступные действия подсвечены/неактивны; никакое действие не уводит кредиты в минус.

---

## Implementation Notes

- All rules come from the 001 core API; UI only calls it.

---

## Out of Scope

- 004: terminal; 006: letters between nights.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: UI
**Required evidence**:
- UI: `production/qa/evidence/day-build-phase-evidence.md` with retained screenshot — the look is NOT waived

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 001, 002
- Unlocks: 005, 006
