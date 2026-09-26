# Story 005: Пять ночей и нарастание «Out of Control»

> **Epic**: Ночная смена (MVP)
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 45 min
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP features 5–6 (5 ночей из данных, типы атак, чужие ноды, сбои инструментов)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] 5 ночей описаны данными (длительность 300–420 с, волны: тип, количество, интервал, цель); код ночей не содержит чисел баланса.
- [ ] Типы атак: скан (быстрый, слабый), брутфорс (крепкий), DDoS (много слабых в цель-Сервер, роняют его), червь (заражает Сервер; заражённый сам шлёт червей соседям до `patch`), аномалия (скрытая, может пройти по несуществующей связи между соседними клетками).
- [ ] Ночи 3–4: в сети сами появляются чужие ноды (заданы данными), связанные с существующими; их нельзя продать, только изолировать.
- [ ] Ночи 4–5: команды с вероятностью из данных срабатывают не на ту ноду; об этом пишется в лог.
- [ ] Ночь 5 заканчивается состоянием «сеть вне контроля»: остаётся доступна только команда `shutdown --all`, её ввод — победная концовка.
- [ ] Прохождение всех 5 ночей возможно: автотест «разумной» стартовой обороны на seed проходит ночи 1–2 без поражения.

---

## Implementation Notes

- Data in `Assets/Scripts/Core/Data/`; RNG seeded per night.

---

## Out of Scope

- 006: letters and screens.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/core/five-nights-escalation_test.cs` — advisory (waived at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 001, 004
- Unlocks: 006
