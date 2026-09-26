# Story 001: Ядро симуляции сети

> **Epic**: Ночная смена (MVP)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1 h
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP features 1–3 (карта сети, ночная симуляция, 3 СЗИ) — engine-independent rules

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] Сеть — граф на сетке 12×7: ровно один Шлюз (левый край) и одно Ядро (правый край) стоят с начала и не продаются; ноду можно поставить только в пустую клетку; повторная связь между теми же нодами запрещена.
- [ ] Покупка ноды / связи / апгрейда списывает цену из `GameData`; при нехватке кредитов операция отклоняется и баланс не меняется.
- [ ] Сервер приносит доход (кредиты/с) только пока онлайн и имеет путь и до Шлюза, и до Ядра.
- [ ] Пакет появляется на Шлюзе, движется по связям со скоростью своего типа и на каждой ноде пересчитывает кратчайший (по хопам) путь к цели; если пути нет — пакет рассеивается без урона.
- [ ] Firewall при прохождении пакета снимает с него `filter` HP (ур.1/ур.2 из `GameData`); пакет с HP ≤ 0 уничтожается. Скрытые пакеты firewall не видит, пока они не раскрыты.
- [ ] IDS раскрывает скрытые пакеты на соседних нодах и замедляет пакеты на соседних связях (множитель из `GameData`).
- [ ] Пакет, пришедший на ноду, соседнюю с Honeypot с оставшейся ёмкостью, уводится в Honeypot и уничтожается; ёмкость уменьшается на 1 (ур.1/ур.2 из `GameData`).
- [ ] Пакет, дошедший до Ядра, снимает свой урон с целостности Ядра (старт 100); при 0 симуляция сообщает поражение.
- [ ] Ночь длится `nightDuration` из данных ночи; по истечении симуляция сообщает конец ночи и отчёт (заработано, заблокировано, пропущено, урон).
- [ ] Симуляция детерминирована: одинаковый seed и одинаковые действия дают одинаковый результат.

---

## Implementation Notes

- `Assets/Scripts/Core/` namespace `NightShift.Core`; asmdef `NightShift.Core` with `noEngineReferences: true`.
- Validation project `tests/unit/core/` — core compiled as `netstandard2.1`, `LangVersion 9` (Unity's C# level), NUnit tests via `dotnet test`.
- Randomness only via injected seeded RNG; time only via `Tick(dt)`.
- Events (`OnPacketBlocked`, `OnCoreDamaged`, `OnNightEnded`, …) for the Unity layer to subscribe to.

---

## Out of Scope

- 002: rendering; 004: terminal commands (core exposes Isolate/Reveal/Patch API only); 005: attack-type behaviours beyond basic movement, night data for nights 2–5.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/core/simulation-core_test.cs` — advisory (waived at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: 002, 003, 004, 005
