# Design council – round 1

Basis: `--snapshot` renderings of the first implementation (three-page settings, status popup, setup wizard, tray icon; each in light and dark mode). The UI under review was German.

| Member | Role | Score |
|---|---|---|
| Claude Opus | Senior visual designer | **7** |
| Claude Sonnet | UX & information architecture | **7** |
| Claude Haiku | Accessibility & eye comfort | **6** |
| Claude Fable | Critical product design lead | **6** |

---

## Sonnet – UX & information architecture (7/10)

**Strengths**
- Clear basic structure: Overview, Input and General are cleanly separated; every setting has an explanatory line.
- Exemplary setup: one purpose per step, honest details (6 MB, admin prompt), visible progress.
- States are encoded by color and text; empty and error states exist.

**Problems**
1. Green "Active" with 0 controllers contradicts the empty state → state "Ready – waiting for controllers".
2. Footnote about paused controllers is pointless in the empty state → only show it when controllers exist.
3. Metrics "4 ms · 250 Hz" / "Rumble On" are jargon and duplicated → reduce, use plain language ("player 2" instead of "XInput slot 2").
4. Popup: "Output/Input" is abstract, "Active" appears three times → clearer section titles, no triple "Active".
5. Popup error phrased three times, no action → one message + "Set up driver" action, hide lists.
6. Repair dialog: jargon title, order contradicts the text (restart first) → reorder, "Quit" → "Close".
7. "Per stick / per axis", "dead zone" not understandable, rule table reads like log text → plain terms, full sentences.
8. Component list is developer info at the same level as everyday options → move to "Technical details".

**Needed for 9/10:** contradiction-free states (one error = one message + one action), plain language, a concise popup.

---

## Fable – Critical product design lead (6/10)

**Strengths**
- Calm basic structure, good spacing, light and dark of equal quality.
- Good German microcopy.
- Honest, trust-building setup.

**Problems**
1. "Active" five times on screen, "250 Hz" twice → remove the sidebar status card and the labels next to switches.
2. Green "Active" with 0 controllers → neutral "Ready".
3. Error three times in three wordings → one message, one term, hide sections.
4. No character: the same gamepad glyph six times, banal metric tiles → make controller families distinguishable, stage the hero as "N → 1".
5. "3 controllers merged" while "2 of 3" are active → consistently "2 of 3 active".
6. Setup button "Installing …" while "Verify authenticity" runs; repair order contradictory.
7. Three different content widths; active navigation barely visible in light mode.
8. Mixed vocabulary (German/English terms); red "Quit" too dominant.

**Needed for 9/10:** every piece of information exactly once, an own visual idea instead of glyph repetition, consistent system discipline (width, vocabulary, state-accurate button texts).

---

## Opus – Senior visual designer (7/10)

**Strengths**
- Clear type hierarchy, light and dark of one piece, accent sensible per theme.
- Calm cards with subtle borders and generous padding; popup and settings speak the same language.
- Tidy empty state.

**Problems**
1. Content width jumps per page (≈510/560/580 DIP) → one consistent width.
2. Icon tiles 44 vs. 34 DIP → text columns misaligned → 40 DIP everywhere, 16 DIP spacing.
3. Popup error three times, banner without icon/action → one message + "Install driver" button.
4. Setup height jumps per state; repair uses bare digits instead of circle steppers → fixed height, consistent style.
5. Disabled primary button "Installing …" looks broken; no connecting lines between steps.
6. Active menu item barely visible in light mode → accent tint; sidebar lighter than content.
7. Green "Active" with 0 controllers; footnote without controllers → neutral "Ready".
8. Tray dot at 16 px merges with the outline → cut-out, 6 px diameter.

Minor: "Quit" permanently red is too alarming; mixed terms; component values inconsistent (pill vs. bold text).

**Needed for 9/10:** consistent grid (width, alignment lines, setup height), error/empty states without duplication, a high-quality working state and 16 px tray icon.

---

## Haiku – Accessibility & eye comfort (6/10)

**Strengths**
- State never by color alone (switch position + text + dot).
- Light and dark structurally identical.
- Primary text very legible (≈13.8:1 and 17:1).

**Problems**
1. Tertiary text dark #6F7680 on #1C1F23 ≈ 3.6:1 → #8E96A1 (≈5.6:1).
2. Tertiary text light #8A909A on white ≈ 3.2:1 → #666D78; secondary to #4A515C.
3. Success pills in light mode ≈ 3.7–4.2:1 → #1A7A4E.
4. Switch off-state ≈ 1.2–1.3:1 (WCAG 1.4.11 requires 3:1) → outline on the track.
5. Tray state by dot color only (green/gray), red-green deficiency → also shape (hollow dot), outline.
6. Unfilled slider track barely visible → ≈3:1.
7. Active navigation / active segment in light mode ≈1.1:1 → accent tint or border.

**Needed for 9/10:** tertiary text ≥ 4.5:1, non-text contrast ≥ 3:1 (switches, slider), tray state by shape.

---

## Consolidated actions for round 2

| # | Action | Source |
|---|---|---|
| 1 | Honest states: "Active" only with ≥1 active controller, otherwise "Ready" (neutral) or "Paused"; summary "2 of 3 controllers active" | all |
| 2 | Every piece of information exactly once: sidebar status card, metric tiles and switch labels removed | Fable, Sonnet |
| 3 | Hero "N → 1": active controllers as chips → virtual controller | Fable |
| 4 | Controller families distinguishable (monogram tiles PS/XB/NS …) | Fable |
| 5 | One error = one message + one action ("Set up driver") in popup and overview | Sonnet, Opus, Fable |
| 6 | Plain language: player 2 instead of XInput slot, consistent terms, "whole stick / individual axes", rules as full sentences | Sonnet, Fable, Opus |
| 7 | Technical details (driver, SDL, port) moved to the end, values consistently as text | Sonnet, Opus |
| 8 | Consistent content width, 40 DIP tiles with 16 DIP spacing | Opus, Fable |
| 9 | Contrast: tertiary/secondary/success raised, switch off-state with outline, slider track ≥3:1, navigation/segments with accent tint or border | Haiku, Opus |
| 10 | Setup: fixed height, connecting lines, state-accurate button text without a "broken" disabled look, repair hints in stepper style sorted by likelihood | Opus, Sonnet, Fable |
| 11 | Popup: "Quit" neutral (red only on hover), clear section titles | Opus, Fable, Sonnet |
| 12 | Tray: state also by shape (filled / hollow / exclamation mark), bigger cut-out at 16 px | Haiku, Opus |
