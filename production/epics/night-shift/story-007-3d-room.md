# Story 007: 3D-комната с монитором

> **Epic**: Ночная смена (MVP)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: 30 min (cut first)
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7 (3D-комната, игра на экране монитора)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (from `docs/engine-reference/unity/VERSION.md` timeline row for 6.3 LTS — that table is flagged unverified)
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (the **Player goal & fail state** field + the MVP feature this story implements), scoped to this story:*

- [ ] Простая комната из примитивов: стол, монитор, лампа, стена; фиксированная камера.
- [ ] Игра рендерится на экран монитора (render texture), ввод мыши/клавиатуры работает как без комнаты.
- [ ] Эмбиент гула серверной; помехи/мерцание лампы усиливаются к ночи 5.
- [ ] Отключаемо одним флагом (фоллбэк — 2D на весь экран), если ломает ввод или не успеваем.

---

## Implementation Notes

- Built from code like 002; no imported models.

---

## Out of Scope

- Walking around the room (out of scope in the brief).

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` the evidence below is **waived** (advisory, never "must exist and pass").*

**Story Type**: Visual/Feel
**Required evidence**:
- Visual/Feel: `production/qa/evidence/3d-room-evidence.md` with retained screenshot — the look is NOT waived

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 002, 006
- Unlocks: None
