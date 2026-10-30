# Peninsula

A little peninsula at the top of your Windows screen that keeps you in the loop with Claude Code.

## Run

```
npm install
node node_modules/electron/install.js   # only if Electron says it "failed to install correctly"
npm run dev                             # starts Vite and opens the app
```

`npm run dev` is the only command you need; it opens Electron once Vite is ready. Close the app to stop both.

## M0 spike checklist

- [ ] Pill sits at the top center, joined to the screen edge
- [ ] Hover grows it with a spring, leaving shrinks it back, and the fps readout stays near your refresh rate
- [ ] Clicks outside the pill reach the apps underneath
- [ ] It stays visible over a fullscreen browser (F11)
- [ ] It looks right at 125% and 150% display scaling (Settings, System, Display, Scale)
