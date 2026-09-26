# Story 006: Сюжет и экраны

> **Epic**: Ночная смена (MVP)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 35 min
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6 (письма офицеров ИБ, реплики в логе) + fail state / финал

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] Титульный экран: название, «Начать смену».
- [ ] Перед каждым днём — 1–2 письма от офицеров ИБ; тон мрачнеет от ночи к ночи; к ночи 5 письма искажены.
- [ ] Во время ночи в лог приходят сюжетные реплики по таймингу из данных.
- [ ] Отчёт смены: заработано / заблокировано / пропущено / целостность.
- [ ] Целостность 0 → экран «Вы уволены» с кнопкой «Заново».
- [ ] `shutdown --all` в ночь 5 → экран концовки.
- [ ] Все тексты на русском; вёрстка читается при 1280×720.

---

## Implementation Notes

- Texts in a data file, not in UI code.

---

## Out of Scope

- 007: 3D presentation.

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: UI
**Required evidence**:
- UI: `production/qa/evidence/story-and-screens-evidence.md` with retained screenshot — the look is NOT waived

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 003, 004, 005
- Unlocks: 007
