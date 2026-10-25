# How to use Achievement Timeline Replay

This guide explains how to recover an achievement history and prepare its migration to Steam. You do not need to program or compile the project.

*The screenshots are preserved from the Spanish guide. The instructions below use the English interface labels.*

## 1. Get everything ready

- Download the ATR package from [the project repository](https://github.com/ramirobornetto/atr/releases/latest).
- Have **Steam** open and signed in to the target account.
- Have [**SAM Rewritten**](https://github.com/PaulCombal/SamRewritten) available, including `samrewritten-cli.exe`.
- Have a supported history: local achievement files, a Playnite Achievements cache, or an Xbox/Epic account with unlocks.

Extract **the entire ATR ZIP** into a folder and run `AchievementTimelineReplay.exe`. The main folder contains that executable and `Support`, which holds documentation and licenses. Dependencies are bundled; sample files are not included.

If you use ATR 0.18.3 or earlier, download the current ZIP and extract it into a new folder. The transition package is no longer distributed. Your library and plans remain in your Windows profile. From 0.18.4 and the compact versions, you can use **Update ATR**.

## 2. Configure Steam and SAM

In the **Scan** tab:

1. Under **Steam directory**, click **Choose folder…** and select the folder where Steam is installed.
2. Under **SAM Rewritten directory**, select the folder containing `samrewritten-cli.exe`. ATR selects that executable automatically.
3. Click **Validate** and check that SAM is recognized.

Do not select a game's folder as the Steam directory, or a parent folder containing several SAM versions: choose the folder that directly contains the CLI file.

## 3. Choose where to import from

The checkboxes under **Available sources** determine what is queried during a scan. Hover over a local source to see its default paths.

| Source | When to use it |
| --- | --- |
| Steam | To read local Steam histories. |
| SteamIdra/LuaTools | For histories whose AppID has an associated LUA in the local configuration. |
| Goldberg / GSE | For `achievements.json` histories in those formats. |
| CODEX / RUNE | For compatible `achievements.ini` histories. |
| CODEX/RUNE compatible | For manually imported compatible files whose path does not identify CODEX or RUNE. |
| Playnite Achievements | To read its saved histories. |
| Xbox Live / Epic | To query the connected account. |

**Unchecking a source does not delete anything you have already imported.**

### Import a file manually

The buttons on the right let you choose files outside the usual paths:

- **Goldberg:** select the state `achievements.json`, usually inside a folder named after the AppID. The file with the same name under `steam_settings` contains definitions and is used to fill in names, not as an unlock history.
- **Codex/Rune:** select `achievements.ini`, usually inside a folder named after the AppID or its `remote` subfolder.
- **Playnite Achievements:** close Playnite and select `achievement_cache.db`. If you use manual tracking, keep `game_custom_data.db` beside that cache.

### Connect Xbox Live

1. Click **Connect Xbox**, then **Connect Microsoft account**.
2. Complete sign-in and authorization on Microsoft's official page. If a code is requested, use the one shown by ATR.
3. Return to ATR and wait for connection confirmation.
4. Leave **Xbox Live** checked and run a scan.

Only games with unlocks accessible to ATR's query are recovered. A game with unavailable information may be skipped even if it appears in your Xbox library.

### Connect Epic Games

1. Click **Connect Epic**, then **Sign in to Epic**.
2. Sign in on the official page that opens and complete your account's verification steps.
3. Linking completes automatically. If sign-in has finished but the window is still waiting, click **Continue after signing in**.
4. Leave **Epic** checked and run a scan.

Only games with unlocks accessible to ATR's query are recovered. A game with unavailable information may be skipped even if it appears in your Epic library.

## 4. Scan and choose a history

Click **Scan library**. When it finishes, open **Library**.

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/d338de72-4cc7-47e6-b9b5-53bd35604e51" />

- Search by game name or ID.
- Use the origin filters to narrow the list. You can enable several.
- Check **Source**, **Achievements**, file availability, and snapshot status.
- Double-click a game to open its **Achievement plan**.

If a game has several sources, **Source history** lets you choose one. The automatic option prioritizes the history with the most unlocks. **It does not combine achievements from all sources.**

<img width="405" height="138" alt="image" src="https://github.com/user-attachments/assets/5e56d204-e0e6-4634-91d3-bf35aaaba918" />

Changing **Origin** corrects a classification label; it does not convert the history to another platform or look for equivalent achievements.

<img width="250" height="387" alt="image" src="https://github.com/user-attachments/assets/c6bfd083-1d3e-4e63-9537-43372abcf08c" />

**Refresh library** scans again. **Remove from library** removes the selected entry from the catalog; the trash button deletes the entire saved catalog. These actions do not delete original achievement files or ATR's plans and snapshots. A new scan may show sources again if they are still available.

## 5. Map achievements from Xbox or Epic

**IF YOU DO NOT OWN A COPY OF THE GAME ON STEAM, SAM WILL RETURN AN ERROR.**

1. In the source game's plan, click **Map achievements to Steam…**.
2. Search for the game by name and choose the correct edition. You can also enter its **Steam AppID**, the number in its store URL.
3. Choose the language and click **Load Steam achievements**.
4. Compare each source achievement with the suggested target: name, description, requirements, and edition/DLC.
5. Under **Steam equivalent**, correct the suggestion if needed or leave **No equivalent**.
6. Check **Confirm** for mappings you have reviewed. The header checkbox can check or uncheck all valid mappings without duplicate targets: review them before using it.
7. Check the game/edition confirmation and click **Prepare Steam plan**.

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/88425bbc-d694-4412-a34a-e521e122a367" />

Only confirmed, valid mappings enter the plan. **Save mappings** lets you keep your work and continue later. A suggestion does not guarantee that two achievements have the same requirements, and two source achievements cannot point to the same target achievement.

Histories that already contain compatible Steam achievement codes can go directly to plan preparation, without this conversion between platforms.

## 6. Adjust the achievement plan

### Selection and Steam status

Check the achievements you want to include. Click **Check Steam status** to see their current state: achievements already unlocked are unchecked. The imported history retains its original data.

### Timings

| Control | What it does |
| --- | --- |
| Automatic duration | Scales the plan's automatic intervals to the selected duration. |
| Limit long automatic waits | Caps each automatic wait; manual timings take priority. |
| New wait | Sets the wait for that achievement relative to the previous selected one. |
| New offset | Places an achievement at a relative time within the plan. |
| Initial delay | Adds a pause before starting. |
| Clear manual timings | Removes manual interval adjustments from the draft. |

You can enter times such as `00:30:00`, `90m`, or `1h 15m`. Always check the **effective duration**, as limits and manual waits may make it differ from the automatic duration you entered.

<img width="551" height="175" alt="image" src="https://github.com/user-attachments/assets/0800aabe-c3f6-45c5-ac2e-bfd5b2f843cd" />

<img width="548" height="179" alt="image" src="https://github.com/user-attachments/assets/10fc9d8e-e580-4b62-b3b4-2f1ff1d450d3" />

<img width="537" height="179" alt="image" src="https://github.com/user-attachments/assets/73f54ea0-8586-428c-8f4c-b09e54655d5f" />

Historical gaps may include days without playing. They do not represent hours played. Undated achievements are retained in formats that can identify their unlocked state; the plan's suggested timings are not recovered historical dates.

### Additional options

- **Repair mode: lock first:** locks selected achievements before unlocking them again. Leave it disabled for a normal migration. It can change the official state and date of achievements you already have.
- **Record playtime (SAM idle):** attempts to keep the game active during migration so Steam records playtime. It does not reconstruct historical hours or guarantee how Steam will count them.

### Save your work

**Save plan** keeps selection, order, timings, and options. **Load saved** restores that configuration. **Restore original** discards draft changes and returns to the imported history; it does not modify Steam or delete the saved plan.

## 7. Review and run

**IF YOU DO NOT OWN A COPY OF THE GAME ON STEAM, SAM WILL RETURN AN ERROR.**

1. Open **Execution** and click **Start migration**.
2. ATR queries Steam and shows a summary. Check the game, AppID, detected account, history, pending and already unlocked achievements, and parameters.
3. If achievement codes are missing, return to the plan and correct the selection or mappings.
4. Confirm the summary to start. If the state changes during review, ATR may ask you to confirm again.
5. Follow progress in Execution. **Cancel migration** stops subsequent steps; it does not undo achievements already applied.

Only one migration can run at a time. You can prepare other games while it runs.

<img width="1170" height="673" alt="image" src="https://github.com/user-attachments/assets/6b4d5b97-9fbf-4199-b63c-0d6693d972df" />

**Steam unlocks receive the actual date when they are performed.** The plan duration controls when they happen, but **cannot assign dates in the past.**

## 8. Review results or resume a run

The report distinguishes already unlocked, applied, pending, failed, and unverified operations. Use **Copy report** to keep or share the details.

After an interruption:

1. Select the same game and history.
2. Open Execution and click **Resume last migration…**.
3. Review the recovered plan and click **Start migration**.
4. Confirm the pending achievements after the new Steam query.

Verified recovery uses SAM's modern CLI. ATR queries Steam again: the local report alone does not prove an achievement was applied. Repair mode is disabled during recovery to avoid relocking completed achievements.

## 9. Understand omissions and errors

| Message or situation | How to interpret it |
| --- | --- |
| No unlocked achievements with valid dates | The local file provided no usable dates to import. It may be an empty cache; it does not prove the account has no achievements. |
| No catalog | The service did not provide a usable achievement catalog for that entry. |
| No progress available | Progress was not received; it is not equivalent to zero unlocks. |
| No unlocks | The queried response contained no unlocks to import. |
| Unmatched history or dates | Some records could not be matched to current definitions; they are not added to the plan automatically. |
| Unsupported INI variant | The file does not match the formats ATR can interpret. |
| `Steam connection failed` | SAM could not complete the query. On its own, this message does not identify the cause or prove that a license is missing. |

Check **Logs** for details. When asking for help, include the ATR version, game, source, and exact error. Do not share access codes or account session files.

## 10. Other information

User data is stored in:

```text
%LOCALAPPDATA%\AchievementTimelineReplay
```

Keep a copy of that folder to back up the library, plans, mappings, snapshots, and runs. Back up source files stored elsewhere separately. Protected Xbox/Epic sessions are tied to the Windows user and may require reconnecting when changing PCs.

## Interface language

In the bottom bar, beside the version, you can choose **Español**, **English**, or **Português (Brasil)**. Your choice is saved, and a prompt offers **Restart now** or **Later**. English is the default when no choice has been saved. This does not interrupt a migration or change achievement names and descriptions. The language of Steam texts is chosen separately in the mappings window.
