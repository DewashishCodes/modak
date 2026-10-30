# Modak: notes for Claude

Windows desktop "island" for Claude Code, Electron + React. Formerly called Peninsula (the hook code still recognises old `/peninsula/` hook URLs on purpose). Inspired by twocode.nl; the user compares the look against its demo video and wants **visual, not textual** UI.

## Commands

- `npm run dev`: Vite on 5280 plus Electron (the Vite config spawns Electron, see `vite.config.mjs`).
- `npm test`: `node --test`, covers server, hooks and widgets.
- `node scripts/modak-gen.js`: regenerates `src/modak.generated.js` from the mascot design.
- Browser preview with fake data: http://localhost:5280/ (`?approval`, `?done`, `?nophotos`), from `src/mock.js` (dev only).
- If Electron "failed to install correctly": `node node_modules/electron/install.js` (npm 12 skipped its download once).

## Code map

| File | What |
|---|---|
| `electron/main.js` | Fixed transparent window (1040x480, never resized, only the pill animates), click-through with hover forwarding, tray, connect dialog, IPC, global approval keys, `modak-photo://` protocol |
| `electron/server.js` | HTTP hook server: sessions, previews, held PermissionRequest responses, request guards |
| `electron/describe.js` | Tool call to activity line, approval question, diff/terminal/answer preview |
| `electron/hooks.js` | Install, uninstall, stale detection of our hooks in `~/.claude/settings.json` (backup, atomic write, never overwrites unparseable JSON) |
| `electron/widgets/*` | `system` CPU/RAM, `git` branch/files/ahead-behind, `usage` today's tokens from transcripts (incremental reads), `photos` folder listing and safe path resolver |
| `src/App.jsx` | Views: notch, approval bar, done bar, dashboard grid, settings; shift-hover ghost mode; hover open delay |
| `src/AgentTile.jsx`, `src/Preview.jsx` | Agent tile and its mini terminal |
| `src/widgets.jsx` | Photos, Clock, Focus, Usage, Git, System, Settings tiles, Ring |
| `src/focus.js` | Focus timer hook; lives in App so closing the dashboard does not reset it |
| `src/Mascot.jsx` | Modak: mood logic (poke, wink, dizzy, sleepy, love) around generated SVG |
| `scripts/modak-gen.js` | Source of the Modak design (approved v3). Edit here, then regenerate |

## Conventions (from the user)

- Commit small and often, one commit per logical step. Commits are single author (no Claude co-author trailer).
- Never use em or en dashes anywhere: code, comments, docs, chat.
- Finish features and get the user's check before any packaging, installer or release step.
- The user wants it to feel as smooth and polished as twocode.nl: animate only transform and opacity, springs, never resize the OS window.
- Keep Modak original (not a robot, not TwoCode's Robo).

## Status (2026-10-30)

Done and checked in the browser mock: notch, approval and done bars, dashboard with all tiles, settings, Modak with 8 moods, focus timer with reset, shift click-through, approval keys. Earlier milestones were tested by the user with real Claude Code (activity, Allow, Deny, done chime).

Not verified on the real app since the last round: the photo slideshow with real files, the `modak-photo://` protocol, the old-hook migration on the user's real settings, the approval keys during a real approval.

## Next

- User checks the latest build with real Claude Code sessions and real photos.
- Codex support (Codex has hooks too, but only command hooks: needs a tiny forwarder script).
- "Ask about my screen" (screenshot to the Claude API), from the original PRD.
- Packaging only after the user approves features: electron-builder NSIS, auto-update; add `asarUnpack` for anything read from disk at runtime.
