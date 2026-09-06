# KF2AutoGrenades

Game automation tool for XP grinding in Killing Floor 2. It stares at your screen so you don't have to click "throw grenade" like a peasant for four hours straight.

## What it actually does

You stand in a killbox (ZEDs can't reach you, you can't reach them, everyone's happy), aim at the horde, and this thing watches the screen and reads the game state:

- **Combat** → mashes `G` to throw grenades at whatever's out there
- **Trader time** → drops the console and fires `RequestSkipTrader` to skip ahead

That's it. No memory injection, no exploits, no touching game files. It just automates pressing buttons a human would already be pressing, badly, for way too long.

## Controls

Once it's running, control it from the console window it opens:

- **F8** — toggle the worker thread on/off (pause/resume without closing the tool)
- **F9** — exit the tool

Toggle it off with F8 whenever you need to alt-tab, deal with a menu, or just don't want it throwing grenades at your loading screen.

## Download

Grab the latest build from the [Releases](../../releases) page. **Extract the entire zip** — don't just yank the .exe out and run it standalone, it needs the DLLs, `Templates`, and `tessdata` folders sitting right next to it or Tesseract OCR will fall over and you'll get a stack trace that looks like a crime scene.

## Setup

1. **Install the map.** Drop the provided map file into:
   ```
   KF2/KFGame/BrewedPC/Maps
   ```
2. **Launch KF2**, start a **solo game** on that map.
3. Walk to a spot near grenade box spawns, aim your gun at where the ZEDs will be piling up.
4. Run the tool. Let it work. Go get a coffee, touch grass, whatever.

## Configuration (`appsettings.json`)

Most people won't need to touch this, but if you want to tune it:

```json
{
  "IdlerOptions": {
    "LoopDelayMs": 1000,
    "OcrIntervalMs": 2000,
    "MatchConfidenceThreshold": 0.85,
    "TargetWidth": 1920,
    "TargetHeight": 1080,
    "TemplatesPath": "Templates",
    "TessdataPath": "tessdata",
    "GrenadeKey": "G",
    "PauseMenuKey": "Escape",
    "DebugMode": false,
    "SaveDebugFrames": false,
    "UseConsoleSkipTrader": true,
    "ConsoleToggleKey": "~",
    "ConsoleCommand": "RequestSkipTrader",
    "ConsoleSendDelayMs": 12,
    "ConsoleAttempts": 3,
    "MonitorIndex": 1
  }
}
```

Quick rundown of the knobs you're most likely to actually care about:

- **`TargetWidth` / `TargetHeight`** — set these to your actual game resolution, or the OCR will be looking in the wrong place and think nothing is happening ever.
- **`MonitorIndex`** — if you're running multi-monitor, this decides which screen the tool watches. Get it wrong and it'll confidently analyze your desktop wallpaper.
- **`MatchConfidenceThreshold`** — how sure the OCR needs to be before it trusts what it read. Lower this if it's not detecting states reliably, raise it if it's doing things at the wrong time.
- **`DebugMode` / `SaveDebugFrames`** — turn these on if something's broken and you want to see what the tool is actually seeing.
- **`GrenadeKey` / `PauseMenuKey` / `ConsoleToggleKey`** — only touch these if you've rebound your in-game keys.

If you break your config, delete `appsettings.json` and copy the block above back in. It's not fragile, but it also doesn't forgive typos.

## Linux support

Not there yet, but being worked on. Currently Windows-only because a chunk of this relies on Win32 calls for input simulation and window/screen capture, which don't exist on Linux. If you're on Linux, bruh...

## Building from source

If you don't trust a random .exe from a stranger on the internet (respectable), clone the repo and build it yourself in Visual Studio / `dotnet build`. It's a normal C# console app, nothing exotic.

## Is this against the rules?

Genuinely not sure! I *doubt* it, since this doesn't modify game files, read/write memory, or touch anything TWI would call an exploit. It's just an input-automation loop reacting to pixels on your own screen, the same category of tool as a macro. But I'm not a lawyer, I'm not Tripwire, and "I doubt it" is not a legally binding statement. Use at your own risk, don't cry to me if something changes.

## Disclaimer

This was built to grind XP faster, not to break the game. If it stops working after a patch, that's probably the OCR templates going stale, not a sign of anything sinister. PRs welcome if you want to fix something before I notice it's broken.