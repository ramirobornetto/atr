<div align="center">

[Español](DESCRIPCION.md) · **English**

<img width="256" height="256" alt="image" src="https://github.com/user-attachments/assets/87711d51-5cc5-47b7-8c29-2f6c6a58d22d" />

# Achievement Timeline Replay — ATR

**[Download v0.19.1 for Windows x64](https://github.com/ramirobornetto/atr/releases/download/v0.19.1/ATR_v0.19.1_Windows_x64.zip)**

**Recover your achievements and prepare their migration to your official Steam copy.**

Have you ever played an "unofficial" copy of a game and later decided to buy it? If so, you have probably wanted to unlock the achievements you had already earned so you could display them on your Steam profile. Apps that do this already exist, but manually choosing which achievements to unlock, entering random times for each one, and checking and rechecking what will be unlocked (pirates, but honest ones!) can be a very tedious task.

I'm a simple person: I had a problem to solve and couldn't find a tool that did what I needed. Since I lack programming skills and knowledge, I worked entirely with CODEX to develop this solution. Again: **I AM NOT A PROGRAMMER.**

Achievement Timeline Replay is a Windows application that brings together achievement histories, lets you review them, and prepares their unlocking on Steam through [SAM Rewritten](https://github.com/PaulCombal/SamRewritten). Its main purpose is to recover progress earned in previous copies of your games, including emulation histories, and transfer compatible achievements to an official license. **IT DOES NOT UNLOCK ACHIEVEMENTS THAT HAVE NOT ALREADY BEEN UNLOCKED.** Other tools already exist for that.

Instead of unlocking every achievement at once, ATR lets you choose which ones to apply, keep or change their order, and spread the unlocks across an execution plan.

## Supported histories

**Steam:** reads local histories and their achievement definitions.

**SteamIdra/LuaTools:** classifies Steam histories using the local LUA files associated with the game.

**Goldberg and GSE:** reads and imports compatible `achievements.json` files.

**CODEX and RUNE:** reads and imports supported variants of `achievements.ini`.

**Playnite Achievements:** imports histories from its local cache.

**Xbox Live and Epic Games:** connects your account and queries games with unlocked achievements.

Histories remain separate by source and identity. When a game has several compatible alternatives, you can choose which one to use; their achievements are not indiscriminately combined. The Xbox and Epic lists represent histories ATR can recover, not a complete inventory of purchased games.

## Migration preparation tools

<img width="1426" height="893" alt="image" src="https://github.com/user-attachments/assets/aaa1e229-c93d-4965-9b43-a0df8bfb1b88" />

- Persistent library with search, origin filters, and source history selection.

- Assisted mappings between achievements from another platform and their Steam equivalents, with game search and Spanish or English texts when available.

- Confirmation of mappings before adding them to the plan.

- Individual achievement selection, custom ordering with live dragging, and timing edits.

- Configurable automatic duration, long-wait limit, and initial delay.

- Current Steam status queries to identify achievements that are already unlocked.

- Saved plans, protected copies of imported histories, and recovery of interrupted runs.

- Progress and per-achievement reports distinguishing verified, pending, and failed operations.

- Built-in updater and a Windows x64 package with .NET included.

**Interface available in Spanish, English, and Brazilian Portuguese**, with your language choice saved between sessions.

## How it works

**Scan or import → choose a history → review mappings if needed → prepare the plan → check Steam → confirm and run.**

ATR queries Xbox and Epic without modifying their achievements. Steam is the execution target, and SAM Rewritten performs the queries and unlock operations. Playnite is optional: you only need it to import its histories.

Historical dates are used to build the plan's order and intervals. **Achievements unlocked on Steam receive the actual execution date:** ATR cannot assign their original historical dates. Mappings between platforms or editions require user review.

To get started, read the [usage tutorial in English](en-howto.md).

<a href='https://cafecito.app/ramirobornetto' rel='noopener' target='_blank'><img srcset='https://cdn.cafecito.app/imgs/buttons/button_3.png 1x, https://cdn.cafecito.app/imgs/buttons/button_3_2x.png 2x, https://cdn.cafecito.app/imgs/buttons/button_3_3.75x.png 3.75x' src='https://cdn.cafecito.app/imgs/buttons/button_3.png' alt='Buy me a coffee' /></a>

</div>
