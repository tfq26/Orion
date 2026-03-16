# Orion Desktop

Orion Desktop wraps the existing dashboard in a Tauri shell for local-first operation.

## Target platforms

- Windows
- Linux

macOS is intentionally excluded for now to avoid Apple signing and notarization requirements during the initial desktop rollout.

## Location

The desktop shell lives in:

- `src/Orion.Dashboard.Solid/src-tauri`

The Solid frontend remains the UI source of truth:

- `src/Orion.Dashboard.Solid`

## Commands

From `src/Orion.Dashboard.Solid`:

- `npm run tauri:dev`
- `npm run tauri:build`

## Current shape

- Tauri hosts the existing Solid dashboard
- Desktop builds are intended only for Windows and Linux
- Unsupported targets fail at compile time in `src-tauri/src/lib.rs`

## Next steps

- Start and manage `Orion.Api` from the Tauri shell
- Add native folder import instead of browser upload fallbacks
- Add WorkOS sign-in via desktop deeplink callback
- Introduce secure desktop session storage
