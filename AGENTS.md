# AGENTS.md

This repository is a Unity project (`6000.3.1f1`).

## Scope
- Use this file as the default guide for code-reading and code-editing tasks.
- Keep changes focused and minimal.

## Main Folders To Work In
- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `UserSettings/` (only when explicitly needed)

## Folders That Are Usually Safe To Ignore
- `Library/`
- `Temp/`
- `Logs/`
- `Obj/` (if present)
- `Build/` and `Builds/` (if present)
- `MemoryCaptures/` (if present)
- `Recordings/` (if present)
- `.vs/`, `.idea/`, `.vscode/` (if present)

## Unity Asset Rules
- Keep asset files and their `.meta` files in sync.
- Do not leave partial moves/deletes (asset without `.meta`, or `.meta` without asset).
- Prefer project-relative paths and portable settings (no machine-specific absolute paths).
