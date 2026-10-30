# Modak

A little modak at the top of your Windows screen that keeps you in the loop with Claude Code.

Modak sits in a small notch joined to the top edge of the screen. It glows while Claude works, asks you to approve actions without switching to the terminal, chimes when the work is done, and opens into a dashboard when you hover it.

## Features

- **Notch:** Modak's face only, with a glow by state: orange working, amber asking, green done. While a focus timer runs, the notch shows a ring and the countdown.
- **Approvals:** a slim bar, "webshop: Allow `git push origin main`?" with Allow, Deny and "answer in terminal". Click the text to see the full command or diff. Keys while an approval waits: `Ctrl+Alt+Y` allows, `Ctrl+Alt+N` denies.
- **Done:** chime, a green "Done in <folder>" bar with the start of the answer, and a happy Modak.
- **Dashboard (hover):**
  - Agent tile: big Modak, folder, status, session switcher (1/2), and a mini terminal that shows the live diff of an edit, a command with its real output, or the final answer.
  - Photo slideshow of your favourite images (folder: `Pictures\Modak` by default, change it in settings).
  - Analog plus digital clock.
  - 25 minute focus timer with a progress ring, pause and reset.
  - CPU and RAM rings.
  - Usage today: output tokens, token mix bar, hourly chart (read from Claude Code transcripts).
  - Git for the session's folder: branch, ahead or behind, change chips, changed files, last commit.
  - Settings: connect or disconnect Claude Code, sound, start with Windows, photos folder.
- **Shift hover:** hold Shift over Modak and it fades away so you can click whatever is behind it.
- **Modak's moods:** idle (blinks), working (focused, steaming), asking (curious), done (hop), love (after Allow), wink (one poke), dizzy (five quick pokes), sleepy (after two idle minutes).

## Run

```
npm install
node node_modules/electron/install.js   # only if Electron says it "failed to install correctly"
npm run dev                             # starts Vite and opens the app
npm test                                # node --test, 21 tests
```

`npm run dev` is the only command you need: it opens Electron once Vite is ready. Close the app to stop both.

On first launch Modak asks to add its hooks to `~/.claude/settings.json` (a backup is kept next to it as `settings.json.modak-backup`). Your other settings and hooks are left alone. Disconnect any time from settings or the tray.

To look at the UI without Electron, open http://localhost:5280/ in a browser while `npm run dev` runs. A mock fills in sample sessions. Add `?approval`, `?done` or `?nophotos` to see those states.

## How it works

Claude Code supports HTTP hooks, so there is no hook script. Modak listens on `127.0.0.1:47821/modak/hook`:

- Tool, prompt, notification and stop events update each session's state and live preview.
- `PermissionRequest` is held open until you click Allow or Deny, then answered with Claude Code's decision JSON. "Answer in terminal", quitting Modak, or Modak not running all return an empty reply, and Claude Code falls back to its normal terminal prompt.
- The server only accepts `application/json` from `127.0.0.1` or `localhost` on its own port, with a 1 MB cap.

See `CLAUDE.md` for the code map and `docs/PRD.md` for the original plan.
