# Story 002: Карта и играбельная ночь в Unity

> **Epic**: Ночная смена (MVP)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Integration
> **Estimate**: 45 min
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2 (ночная симуляция) — first playable night

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] Открытие сцены `Main` (или любой пустой сцены) поднимает игру через bootstrap из кода: камера, карта, HUD — без ручной сборки сцены в редакторе.
- [ ] Ноды отрисованы по типам (разные цвет/значок), связи — линиями; сетка видна.
- [ ] Пакеты видимо движутся по связям; скрытые пакеты не видны до раскрытия; уничтожение пакета даёт короткую вспышку.
- [ ] HUD показывает кредиты, целостность Ядра, время до конца ночи и номер ночи.
- [ ] Ночь 1 на фиксированной стартовой сети проигрывается от начала до конца без ошибок в консоли; в конце — экран отчёта смены.
- [ ] Клавиша `F4` переключает ускорение ×1/×4 (отладка).

---

## Implementation Notes

- `Assets/Scripts/Game/`, namespace `NightShift.Game`; `[RuntimeInitializeOnLoadMethod]` bootstrap creates everything.
- Rendering with primitives (sprites/LineRenderer or UI Toolkit) — no external assets.
- Read the UI/rendering modules in `docs/engine-reference/unity/modules/` before choosing APIs.

---

## Out of Scope

- 003: building; 004: terminal; 006: styled screens.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/game/unity-night-view_test.cs` OR playtest doc — advisory

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 001
- Unlocks: 003, 004, 007
