> Original plan, written when the app was called Peninsula. The app and mascot are now Modak; see README.md and CLAUDE.md for the current state.

# Peninsula: PRD and Build Plan

## Context
The user saw twocode.nl (a Windows "island" for Claude Code and Codex) and wants their own, built from scratch: a new direction, not the old `island/` project. Stack decided: Electron. The bar is "as smooth as the site's demo". Name: **Peninsula**, a pill joined to the top edge of the screen so only three sides show. Mascot: our own blob/mochi creature. v1 supports Claude Code only; Codex comes later.

New folder: `C:\Users\Dewashish Lambore\desktop\projects\peninsula\` with its own git repo.

---

## PRD

### Problem
While Claude Code works, you either watch the terminal or miss its permission prompts and miss when it finishes. You need a small, always-visible spot that shows what the agent is doing, lets you approve actions, and tells you when it's done.

### User
A developer on Windows 10/11 who runs one or more Claude Code sessions in terminals.

### v1 features
1. **The peninsula.** A dark pill at the top center of the screen, joined to the top edge (rounded bottom corners only). It is always on top, including over fullscreen apps. Clicks go through to the apps underneath except on the pill.
2. **Live activity.** Collapsed, it shows the mascot, the folder name and the current action (for example "Editing app.tsx" or "Running npm test"). Hover expands it to show each session's latest step and last answer.
3. **Approvals.** When Claude Code wants permission, the pill expands with the tool, its input and Allow / Deny / Answer in terminal buttons. Several requests queue up, one per session.
4. **Done.** A soft sound plays, the mascot celebrates and the last answer shows up. It fades back after a few seconds.
5. **Mascot moods.** Idle (blinks), working (focused), asking (curious, wobbles), done (happy). Poking it too often makes it dizzy.
6. **Setup.** On first run it asks before adding its hooks to `~/.claude/settings.json`. It keeps a backup and merges with what's there. A tray menu can remove the hooks, quit the app or start it with Windows.

### Later
- Codex support through a command hook that forwards to the app (Codex has no HTTP hooks).
- "Ask about my screen": a screenshot sent to the Claude API, with the user's key stored using Electron `safeStorage`.
- Hide the pill when a fullscreen game is running, plus multi-monitor choice and settings UI polish.

### Non-goals
- Starting or running agents. No chat UI. Not on Mac or Linux.

### Smoothness requirements
- 60 fps on the morph between collapsed and expanded, measured in the DevTools performance panel.
- The OS window is never resized during an animation. Only `transform` and `opacity` are animated, with spring easing.
- An approval shows up in the pill within 150 ms of Claude Code asking.

### Success
Use it for a full day of real Claude Code work without switching to the terminal to approve anything, with no jank and no missed prompt.

---

## Technical design

### Facts this design relies on (checked in the official hook docs at code.claude.com/docs/en/hooks)
- Claude Code supports **HTTP hooks**: `{"type":"http","url":...,"timeout":N}`. It POSTs the event JSON and reads the response like command output, so **no hook script or extra process is needed**.
- `PermissionRequest` fires only when Claude Code is about to ask the user. We answer it with `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"|"deny","message"?}}}`.
- An empty 2xx body or a failed connection means the normal flow continues: the terminal prompt shows as usual. So if Peninsula is closed or crashes, Claude Code works exactly as before.
- Every event carries `session_id`, `cwd`, `transcript_path` and `hook_event_name`. The tool events add `tool_name` and `tool_input`. `Notification` adds `notification_type` (`permission_prompt`, `idle_prompt`, ...). `Stop` adds `last_assistant_message`.

### Architecture
```
Claude Code ──HTTP POST──> main process (node:http on 127.0.0.1:47821)
                              │ session store (Map by session_id)
                              │ pending approvals (holds the HTTP response open)
                              └─ IPC ──> renderer (React + Motion + Rive) ── click ──> IPC back ──> answers the held response
```

### Hooks Peninsula installs (all `type: "http"`, matcher `*`)
| Event | Use | Response |
|---|---|---|
| SessionStart / SessionEnd | add or remove a session | empty 200 |
| UserPromptSubmit | mood: working | empty 200 |
| PreToolUse / PostToolUse | live activity line | empty 200 (no decision, so no effect on Claude) |
| PermissionRequest (`timeout: 600`) | approval card | held until the user clicks; empty 200 for "Answer in terminal" |
| Notification | `idle_prompt` puts the mood in asking | empty 200 |
| Stop | done: sound, last answer | empty 200 |

Each hook entry is tagged (url contains `/peninsula/`) so uninstall removes only ours.

### Edge cases
- **You answer in the terminal instead:** the next PostToolUse, Stop or PermissionRequest from that session clears its open card, and the held response gets an empty 200.
- **You quit with requests still open:** answer them all with empty 200s, so the terminal prompt takes over.
- **Port already in use:** show a tray error. `shortcut: fixed port, make it configurable if anyone hits a clash`.
- **Security:** listen only on 127.0.0.1. Reject anything that isn't `Content-Type: application/json`, which blocks cross-site browser POSTs because we never answer the CORS preflight. Reject a `Host` header that isn't `127.0.0.1:47821` or `localhost:47821`, to stop DNS rebinding. Cap request bodies at 1 MB.
- **Malformed JSON:** return an empty 200 and never throw. A broken event must never block Claude Code.

### Window ("smooth" rules)
- One `BrowserWindow`: `transparent`, `frame:false`, `resizable:false`, `skipTaskbar`, `focusable:false` while collapsed, `alwaysOnTop` at level `'screen-saver'`. Fixed size around 520x360, top center of the primary display, sized from `screen.getPrimaryDisplay().workArea` (DPI aware).
- `setIgnoreMouseEvents(true, {forward:true})` by default. The renderer sends `mouse-over-pill` / `mouse-left-pill` over IPC to toggle it. The window becomes focusable only while an approval card is open, so keyboard shortcuts work.
- The pill's size morph uses Motion `layout` with springs (`stiffness ~400, damping ~30`). No CSS width or height transitions.
- Mascot: a CSS/SVG blob placeholder in M3, swapped for a Rive state machine (inputs `mood`, `poke`) in M4.

### Project layout
```
peninsula/
  package.json            electron, vite, react, motion, @rive-app/react-canvas, electron-builder
  electron/main.js        window, tray, IPC, starts server
  electron/server.js      http server, session store, pending approvals
  electron/hooks.js       install/uninstall hooks in ~/.claude/settings.json (backup + merge)
  electron/preload.js     contextBridge: onEvent, decide(id, behavior), setHover
  src/App.jsx             pill states: collapsed / expanded / approval / done
  src/Mascot.jsx          placeholder blob, later Rive
  src/assets/done.mp3, mascot.riv
  test/server.test.js     node:test
```
Build tooling: Vite for the renderer, `electron-builder` NSIS installer, auto-update from GitHub Releases (M5).

---

## Milestones (each ends with a walkthrough for the user)

**M0. Window spike (the gate).** Transparent, clickthrough-with-hover pill, plus a spring morph between collapsed and expanded on a button. Check on the user's machine: 60 fps, display scaling at 125% and 150%, shows over a fullscreen browser, clicks pass through. If hover forwarding is janky, fix it here before anything else.

**M1. Hook server and live activity.** `server.js` and `hooks.js` (install with confirmation). The pill shows the session folder and current tool. Hovering shows every session.

**M2. Approvals.** Held-response flow, the card UI, the queue, the terminal-answer cleanup and the quit cleanup.

**M3. Done and moods.** Sound, last answer preview, placeholder blob with four moods and the poke-to-dizzy reaction.

**M4. Rive mascot.** Design the blob in the Rive editor (the user or I author the `.riv`), then wire up its `mood` and `poke` inputs.

**M5. Ship.** Tray (uninstall hooks, start with Windows, quit), NSIS installer, auto-update.

**M6 (later).** Codex forwarder, "Ask about my screen".

---

## Verification
- **Unit tests (`node --test`):** the server maps events to session state; PermissionRequest is held and then answered with exactly the allow or deny JSON; bad JSON, a wrong content type or a wrong Host returns empty or 4xx without crashing; cleanup on terminal answer and on quit; hooks install and uninstall leave the user's other hooks untouched (run on a temp copy of settings.json).
- **Manual with curl:** POST sample events to `127.0.0.1:47821` and watch the pill. For PermissionRequest, curl blocks until Allow is clicked and then prints the decision JSON.
- **Real end to end:** run `claude` in a test folder and ask it to create and run a file. Check that the pill shows the steps, that Allow in the pill lets the action run and Deny blocks it, that the sound plays on finish, and that with Peninsula closed Claude falls back to the terminal prompt.
- **Smoothness:** a DevTools performance recording of the morph shows no long frames, checked at 100% and 150% display scaling.
