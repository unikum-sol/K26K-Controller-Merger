# Design-Council – Runde 1

Grundlage: `--snapshot`-Renderings der ersten Implementierung (Einstellungen in drei Seiten, Status-Popup, Einrichtungsassistent, Tray-Symbol; jeweils Hell und Dunkel).

| Mitglied | Rolle | Score |
|---|---|---|
| Claude Opus | Senior Visual Designer | **7** |
| Claude Sonnet | UX & Informationsarchitektur | **7** |
| Claude Haiku | Accessibility & Augenkomfort | **6** |
| Claude Fable | Kritischer Product-Design-Lead | **6** |

---

## Sonnet – UX & Informationsarchitektur (7/10)

**Stärken**
- Klare Grundstruktur: Übersicht, Eingabe und Allgemein sind sauber getrennt; jede Einstellung hat eine Erklärzeile.
- Vorbildliches Setup: ein Zweck pro Schritt, ehrliche Angaben (6 MB, Admin-Abfrage), sichtbarer Fortschritt.
- Zustände sind farblich und textlich kodiert, Leer- und Fehlerzustand existieren.

**Probleme**
1. Grünes „Aktiv“ bei 0 Controllern widerspricht dem Leerzustand → Zustand „Bereit – wartet auf Controller“.
2. Fußnote zu pausierten Controllern im Leerzustand sinnlos → nur bei vorhandenen Controllern zeigen.
3. Kennzahlen „4 ms · 250 Hz“ / „Vibration Ein“ sind Jargon und doppelt → reduzieren, Laiensprache („Spieler 2“ statt „XInput-Slot 2“).
4. Popup: „Ausgabe/Eingabe“ abstrakt, „Aktiv“ dreifach → klarere Abschnittstitel, kein dreifaches „Aktiv“.
5. Popup-Fehler dreimal formuliert, keine Handlung → eine Meldung + Aktion „Treiber einrichten“, Listen ausblenden.
6. Reparatur-Dialog: Jargon-Titel, Reihenfolge widerspricht dem Text (Neustart zuerst) → umordnen, „Beenden“ → „Schließen“.
7. „Pro Stick / Pro Achse“, „Deadzone“ unverständlich, Regel-Tabelle wirkt wie Logtext → Laienbegriffe, ganze Sätze.
8. Komponentenliste ist Entwicklerinfo gleichrangig mit Alltagsoptionen → als „Technische Details“ nach hinten.

**Für 9/10 nötig:** widerspruchsfreie Zustände (ein Fehler = eine Meldung + eine Handlung), Laiensprache, knappes Popup.

---

## Fable – Kritischer Product-Design-Lead (6/10)

**Stärken**
- Ruhige Grundstruktur, gute Abstände, Hell und Dunkel gleichwertig.
- Gute deutsche Mikrotexte.
- Ehrliches, vertrauensbildendes Setup.

**Probleme**
1. „Aktiv“ fünfmal im Bild, „250 Hz“ doppelt → Sidebar-Statuskarte streichen, Labels neben Schaltern entfernen.
2. Grünes „Aktiv“ bei 0 Controllern → neutrales „Bereit“.
3. Fehler dreimal mit drei Formulierungen → eine Meldung, ein Begriff, Sektionen ausblenden.
4. Kein Charakter: dasselbe Gamepad-Glyph sechsmal, banale Kennzahl-Kacheln → Controller-Familien unterscheidbar machen, Hero als „N → 1“ inszenieren.
5. „3 Controller zusammengeführt“ bei „2 von 3“ aktiv → einheitlich „2 von 3 aktiv“.
6. Setup-Button „Wird installiert …“ während „Echtheit prüfen“ läuft; Reparatur-Reihenfolge widersprüchlich.
7. Drei verschiedene Inhaltsbreiten; aktive Navigation im Hell-Theme kaum sichtbar.
8. Sprachmix (Deadzone, Rumble, Buttons, Steuerkreuz); rotes „Beenden“ zu dominant.

**Für 9/10 nötig:** jede Information genau einmal, eine eigene visuelle Idee statt Glyph-Wiederholung, durchgängige Systemdisziplin (Breite, Vokabular, zustandsgetreue Buttontexte).

---

## Opus – Senior Visual Designer (7/10)

**Stärken**
- Klare Typo-Hierarchie, Hell und Dunkel aus einem Guss, Akzent pro Theme sinnvoll.
- Ruhige Karten mit dezenten Rändern und großzügigem Innenabstand; Popup und Einstellungen sprechen dieselbe Sprache.
- Ordentlicher Leerzustand.

**Probleme**
1. Inhaltsbreite springt je Seite (≈510/560/580 DIP) → eine einheitliche Breite.
2. Icon-Kacheln 44 vs. 34 DIP → Textspalten versetzt → überall 40 DIP, 16 DIP Abstand.
3. Popup-Fehler dreifach, Banner ohne Icon/Aktion → eine Meldung + Button „Treiber installieren“.
4. Setup-Höhe springt je Zustand; Reparatur nutzt nackte Ziffern statt Kreis-Stepper → feste Höhe, einheitlicher Stil.
5. Deaktivierter Primärbutton „Wird installiert …“ wirkt kaputt; keine Verbindungslinien zwischen Schritten.
6. Aktiver Menüpunkt im Hell-Theme kaum sichtbar → Akzent-Tönung; Sidebar heller als Inhalt.
7. Grünes „Aktiv“ bei 0 Controllern; Fußnote ohne Controller → neutrales „Bereit“.
8. Tray-Punkt bei 16 px verschmilzt mit der Kontur → Ausstanzung, 6 px Durchmesser.

Kleinere Punkte: „Beenden“ dauerhaft rot zu alarmierend; „Deadzone/Buttons“ → „Totzone/Tasten“; Komponenten-Werte uneinheitlich (Pill vs. fetter Text).

**Für 9/10 nötig:** einheitliches Raster (Breite, Fluchtlinien, Setup-Höhe), Fehler-/Leerzustände ohne Dopplung, hochwertiger Working-Zustand und 16-px-Tray-Symbol.

---

## Haiku – Accessibility & Augenkomfort (6/10)

**Stärken**
- Zustand nie nur über Farbe (Schalterposition + Text + Punkt).
- Hell und Dunkel strukturell identisch.
- Primärtext sehr gut lesbar (≈13,8:1 bzw. 17:1).

**Probleme**
1. Tertiärtext dunkel #6F7680 auf #1C1F23 ≈ 3,6:1 → #8E96A1 (≈5,6:1).
2. Tertiärtext hell #8A909A auf Weiß ≈ 3,2:1 → #666D78; Sekundär auf #4A515C.
3. Erfolgs-Pills hell ≈ 3,7–4,2:1 → #1A7A4E.
4. Aus-Zustand der Schalter ≈ 1,2–1,3:1 (WCAG 1.4.11 verlangt 3:1) → Kontur am Track.
5. Tray-Status nur über Punktfarbe (Grün/Grau), Rot-Grün-Schwäche → zusätzlich Form (hohler Punkt), Kontur.
6. Unbenutzter Slider-Track kaum sichtbar → ≈3:1.
7. Aktive Navigation / aktives Segment im Hell-Theme ≈1,1:1 → Akzent-Tönung bzw. Rand.

**Für 9/10 nötig:** Tertiärtext ≥ 4,5:1, Nicht-Text-Kontrast ≥ 3:1 (Schalter, Slider), Tray-Status über Form.

---

## Konsolidierte Maßnahmen für Runde 2

| # | Maßnahme | Quelle |
|---|---|---|
| 1 | Ehrliche Zustände: „Aktiv“ nur mit ≥1 aktivem Controller, sonst „Bereit“ (neutral) bzw. „Pausiert“; Zusammenfassung „2 von 3 Controllern aktiv“ | alle |
| 2 | Jede Information genau einmal: Sidebar-Statuskarte, Kennzahl-Kacheln und Schalter-Labels entfernt | Fable, Sonnet |
| 3 | Hero „N → 1“: aktive Controller als Chips → Pfeil → virtueller Controller | Fable |
| 4 | Controller-Familien unterscheidbar (Monogramm-Kacheln PS/XB/NS …) | Fable |
| 5 | Ein Fehler = eine Meldung + eine Handlung („Treiber einrichten“) im Popup und in der Übersicht | Sonnet, Opus, Fable |
| 6 | Laiensprache: Spieler 2 statt XInput-Slot, Totzone, Vibration, Tasten, „Ganzer Stick / Einzelne Achsen“, Regeln als ganze Sätze | Sonnet, Fable, Opus |
| 7 | Technische Details (Treiber, SDL, Anschluss) nach hinten, Werte einheitlich als Text | Sonnet, Opus |
| 8 | Einheitliche Inhaltsbreite, 40-DIP-Kacheln mit 16 DIP Abstand | Opus, Fable |
| 9 | Kontrast: Tertiär/Sekundär/Erfolg angehoben, Schalter-Aus mit Kontur, Slider-Track ≥3:1, Navigation/Segmente mit Akzent-Tönung bzw. Rand | Haiku, Opus |
| 10 | Setup: feste Höhe, Verbindungslinien, zustandsgetreuer Buttontext ohne „kaputten“ Disabled-Look, Reparatur-Hinweise im Stepper-Stil und nach Wahrscheinlichkeit sortiert | Opus, Sonnet, Fable |
| 11 | Popup: „Beenden“ neutral (Rot nur bei Hover), klare Abschnittstitel | Opus, Fable, Sonnet |
| 12 | Tray: Status zusätzlich über Form (gefüllt / hohl / Ausrufezeichen), größere Ausstanzung bei 16 px | Haiku, Opus |
