# Ait Recoil – Anti-Recoil Macro

## Screenshot

!(assets/screenshot.png)

Ait Recoil is a Windows-only **anti-recoil macro** built with WPF and low-level Win32 APIs.  
It lets you:

- Fine-tune **vertical & horizontal recoil**
- Create **named presets** (e.g. “AK-47”, “M4”) with saved recoil settings
- Assign **global hotkeys** to presets so you can switch configs in-game
- Run a background loop that automatically **pulls your mouse** while you hold Right Mouse + Left Mouse

---

## ✨ Features

- 🎛️ **Precise recoil control**
  - Vertical slider: `0` – `50` (supports 0.01 steps, displayed with 3 decimals)
  - Horizontal slider: `-50` – `50` (supports 0.01 steps, displayed with 3 decimals)
  - Internal **sub-pixel accumulation** so small values (e.g. `0.05`) still have an effect

- 💾 **Persistent presets**
  - Presets stored as JSON in:
    - `%APPDATA%\AitRecoil\presets.json`
  - Create, update, and delete presets from the UI

- 🖱️ **Simple usage**
  - Click **Start** in the app
  - **Hold Right Mouse + Left Mouse** and the macro will steadily move your mouse to counter recoil
  - Click **Stop** to disable the macro loop

---

## 🛠️ How It Works (Internals)

- Uses Win32 APIs:
  - `SendInput` to simulate mouse movement
  - `GetAsyncKeyState` to poll mouse buttons
  - `SetWindowsHookEx` (WH_KEYBOARD_LL) for global keyboard hooks
- WPF UI:
  - Sliders + numeric boxes for vertical/horizontal recoil
  - Preset management: name, shortcut key, save/update, delete
  - Preset loading via ComboBox

### Sub-pixel / 0.x precision

Windows mouse movement is integer-based (pixels), but the app:

1. Reads double values from the sliders (e.g. `0.05`, `0.13`)
2. **Accumulates** them every tick (every few ms) in `accumulatedX` and `accumulatedY`
3. Once the accumulated movement reaches ±1 pixel, it sends a movement event and subtracts what it used

This means very **small 0.x / 0.0x values** still have a visible effect over time, giving you smoother and more controllable recoil.

---

## 📦 Requirements

- **OS:** Windows 10/11 (64-bit)
- **Framework:** .NET (WPF) – e.g. .NET 6+ or .NET Framework 4.8

---

> ⚠️ **Disclaimer:** This tool sends synthetic mouse input and hooks your keyboard globally.  
> Use it **only where macros are allowed** (e.g. single-player / offline / training).  
> Using it in online games may violate the game’s Terms of Service and/or trigger anti-cheat systems.  
> You are fully responsible for how you use this software.
