# Design council – round 2

Basis: renderings after implementing the twelve consolidated actions from round 1. Members explicitly checked whether their own points had been resolved.

| Member | Role | R1 | **R2** |
|---|---|---|---|
| Claude Opus | Senior visual designer | 7 | **8** |
| Claude Sonnet | UX & information architecture | 7 | **8.5** |
| Claude Haiku | Accessibility & eye comfort | 6 | **8** |
| Claude Fable | Critical product design lead | 6 | **8** |

## Resolved according to the council
- Consistent grid: same content width, shared alignment line, indented dividers (Opus)
- Honest states (Active / Ready / Paused / Error), "2 of 3 controllers active" (all)
- Own product language: monograms, "Controlled by", "Visible to games as player 2" (Fable)
- Error case: one message, one action (Fable, Sonnet)
- All contrast issues from R1 fixed in text, switches, segments and navigation (Haiku)

## Open points (consolidated)

| # | Problem | Raised by | Action for round 3 |
|---|---|---|---|
| 1 | "Set up driver" looks disabled, while the locked "Verifying …" is fully lit | all | Cause: in snapshot mode the action was missing, so the button was disabled → fixed. Waiting state redesigned: tinted surface, spinner, not clickable, "Step 2 of 3" |
| 2 | Success dialog too empty | Opus, Sonnet, Fable | Card with the two tray gestures (right-click / double-click) |
| 3 | Repair stepper suggests a sequence; "Check again" duplicated | Fable | "What usually helps" as a neutral list without lines/numbers, duplicates removed |
| 4 | Empty state duplicated (hero + card) | Opus, Fable | Hero only says "No active controller yet" |
| 5 | In the error state the controller list competes with the error; "ViGEmBus" in body text | Sonnet, Fable | Controller section hidden on error; text without product name; hero "Games currently see no controller" |
| 6 | App icon (violet) vs. accent (blue); two near-identical tiles in the popup | Opus, Haiku, Fable | Logo gradient in accent blue; virtual controller gets its own "360" tile |
| 7 | Help box without heading, too loud, off-grid | Opus, Fable, Sonnet | Own "Help" section at the page end, neutral surface, 40/16 grid, two sentences |
| 8 | Check marks before fixed rules read like options | Fable | Neutral bullets |
| 9 | Only the selected merge option is explained | Sonnet | Both options explained, the selected one highlighted |
| 10 | Switch alone is ambiguous for non-experts | Sonnet | "On"/"Off" labels as in Windows settings |
| 11 | "Quit" without a color cue | Haiku | Icon in red, text neutral, red hover kept |
| 12 | Tray at 16 px: "!" too small, ring too thin | Haiku, Opus | Bigger badge (8 px), pixel-aligned, ring 1.5 px |
| 13 | Light slider track 2.8:1 | Haiku | #858C97 (≈3.4:1) |
| 14 | Setup footer lighter than content in light mode | Opus | Own footer token (#EBEDF0) |
