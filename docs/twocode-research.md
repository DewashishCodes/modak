# TwoCode research (Instagram, 2026-10-30)

Source: @twocode.nl reels, Oct 12 to Oct 30 2026 (series "TwoCode for Windows, part 3" to "part 18" plus promo reels). Frames checked in parts 5, 6, 9, 10, 11, 13, 16, 18 and the snake promo; the other parts from captions only.

## Look and feel

- Idle: small black pill at top centre with only Robo's two eyes, tinted by state.
- Compact activity pill: icon + label ("Reading", "Editing"), Robo eyes in the middle, elapsed time ("0:04") on the right.
- Several activities at once: extra ones fold into small separate circles beside the pill (purple ring = focus, album cover = music). Reel copy: "Fold it in. They stay with you."
- Glow colour language (part 16), used on pill and tiles:
  - red: music
  - orange: Claude working
  - purple: focus timer
  - white: volume
  - blue: Claude done, stays until you look, then turns green and fades
- Expanded panel: header row with Robo eyes, date and time, mini music equalizer, "⋯" menu. Bottom icon tab bar: Home, Claude, Music, Chat, Focus, Clipboard, Note, volume at the right.
- Page titles are two-tone: "Claude **Code**", "Music **Player**", "Focus **Timer**", "Robo **Chat**".
- Each page gets a full gradient in its colour (green when Claude is done, red or cover colour for music, purple for focus).
- Reel format: big title card on top ("Red: your music."), cropped Windows screen recording below.

## Pages and features

### Home (parts 12, 16)
- Three glowing tiles:
  - Claude Code: usage ring ("41%"), "0 jobs today"; turns into "Done" + task summary.
  - Music: cover, title, artist, pause.
  - Focus: "25:00 Ready to start", one tap starts it.
- Quiet state: "Robo: All quiet, Claude did 7 jobs today" with chips "Focus 25 min" and "Ask Robo".

### Claude (parts 7, 13)
- Approval in the pill: "npm install stripe" with Deny / Allow (Allow is the white primary).
- Question ("Claude asks something"): project chip ("webshop"), option cards with title and short description, free-text field, Send.
- Plan ("Claude has a plan"): bullet list, optional "what should change" field, Change it / Approve.
- Done: big "0:15", "Done · every step", summary bubble, 4-step bar Read / Edit / Test / Done.
- Heads-up before hitting the usage limit.

### Music (parts 8, 11, 14, 18)
- Any player (Spotify, Apple Music, YouTube).
- Island takes the album cover colours; colour follows when the song changes.
- Swipe the small island to skip. Robo dances, a different dance per song.
- Lyrics: the current line rolls by in the compact pill; hover shows lines before and after.
- Full player: seek bar, prev / play / next, output device picker ("This PC"), option to tuck music away.

### Focus (part 15)
- Reset, hr/min scroll-wheel picker, Start.
- Presets 5, 15, 25, 50 min; "Done at 16:47".
- Countdown lives in the pill; Robo celebrates when done.

### Volume
- White slider drops into the pill when volume changes; also a slider off the tab bar.

### Clipboard (part 9)
- Recent copies as cards, Clear button.

### Note (parts 4, 9)
- Quick note, saves itself.

### Robo Chat (parts 4, 11)
- Big eyes that follow the mouse, "What can I do for you?".
- Chips: Explain, Check my spelling, Translate, Summarize; text input with send.

### Drag Robo onto a window (part 10, spelling reel)
- Drag Robo out of the island and drop him on any window; chat opens with that window as context.
- Translates a message, explains an error and asks before fixing it, spell-checks Word with fixes as tracked changes.

### Personality (parts 3, 4, 6, pet reels)
- Poke: mad, then dizzy. Music: dances. Away: sleeps. Back: happy.
- Hungry until Claude finishes a task; each finished task feeds him.
- Eyes orange / red / green with the code state.
- Waves goodbye at end of day, Halloween costume by himself, crown, user-picked colour.

### System
- Ctrl+Alt+1 to 6: Home, Music, Claude, Focus, Clipboard, Note.
- Edit mode (part 5): hold a widget, everything wiggles; remove, add (clock), or pick a ready-made layout ("2 big").
- Low battery warning, file tray (drop files on the island).
- Self-update: green dot, one click, Robo installs it ("1.2.0 beta 14").
- The snake reel (Robo eats notifications) looks like a promo animation, not a feature.

## Gap vs Modak (2026-10-30)

Have: notch, approval and done bars, dashboard tiles, mascot moods, focus timer, approval keys.

Missing, roughly by how often TwoCode shows it:
1. Music: any-player media session, cover colour, lyrics, output device.
2. Compact pill with side bubbles for parallel activities; glow colour language.
3. Claude page: question cards and plan approval (no AskUserQuestion / ExitPlanMode handling in `electron/` yet), done summary with step bar, usage limit warning.
4. Tab bar + Ctrl+Alt+1 to 6; Volume, Clipboard, Note pages.
5. Robo Chat and drag-mascot-onto-window (our "Ask about my screen", needs the Claude API).
6. Mascot extras: hungry / fed, dance to music, goodbye wave, seasonal outfits.
7. Edit mode with wiggle and layout presets; self-update (only at packaging time).

Notes: lyrics need an external lyrics source (Windows media info has none). Battery and limit warning visuals not seen, captions only.
