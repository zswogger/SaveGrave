# Save Grave — Features

Save Grave is a cross-platform desktop application that automatically creates versioned
backups of game save directories so you can restore an earlier save if your current one is
deleted, corrupted, or otherwise lost.

- **Platform:** .NET 10, Avalonia UI (desktop), xUnit tests
- **Architecture:** `SaveGuard.Core` (domain + interfaces), `SaveGuard.Infrastructure`
  (filesystem implementations), `SaveGuard.Desktop` (Avalonia UI), `SaveGuard.Tests`
- **Data integrity first:** the app never modifies the active save during normal monitoring; it
  only writes to the save directory when you explicitly restore.

---

## Protection & backups

### Automatic, change-triggered backups
- Each protected game's save folder is watched with a `FileSystemWatcher`.
- Backups are **debounced**: a change marks the save as "dirty" and starts a ~10-second timer.
  Any further change resets the timer, so a burst of writes while a game saves collapses into a
  single snapshot taken only once the folder goes quiet.
- A periodic **reconciliation** pass (every ~5 minutes) compares the live save against the newest
  snapshot, so a missed watcher event never leaves a save unprotected.
- The watcher's buffer-overflow (`Error`) event is handled: it logs a warning and marks the save
  dirty so a backup still happens.

### Manual snapshots (one-click)
- A **Take Snapshot** button (on each game card and on the game-details screen) captures an
  on-demand snapshot immediately.
- Manual snapshots are stored and pruned separately from automatic backups.

### Initial baseline snapshot
- When you add a game, Save Grave captures a snapshot right away, so the game is protected from
  the moment it's added rather than waiting for the first file change.

### Snapshot format
- Snapshots are plain directories (not archives), each named with a UTC timestamp
  (`yyyy-MM-dd_HH-mm-ss`), containing a complete copy of the save directory with structure
  preserved.
- A snapshot is only considered successful after **every** file has copied. Work happens in a
  `.incomplete` staging directory that is promoted to its final name on success; a failed copy is
  cleaned up and never registered as a valid snapshot.

### Three snapshot categories
Each category lives in its own location under the backup folder and is retained independently:

| Category | Purpose | Storage |
| --- | --- | --- |
| **Backups** | Automatic, change-triggered | `<BackupRoot>/<TargetId>/` |
| **Manual Snapshots** | Created on demand via Take Snapshot | `<BackupRoot>/<TargetId>/_manual/` |
| **Safety Snapshots** | Captured automatically just before a restore | `<BackupRoot>/<TargetId>/_safety/` |

### Retention
- Each game has a configurable **recovery points to keep** (`MaxBackups`) value.
- After a successful snapshot, the oldest snapshots are pruned until the count is within the cap.
- Retention is applied **per category**, so manual or safety snapshots can never evict automatic
  backup history (and vice versa). The newest successful snapshot is never deleted because an
  older one failed.

---

## Restore

- Any snapshot — backup, manual, or safety — can be restored.
- Restore is confirmed with an in-app dialog before anything changes.
- **Before** overwriting the current save, Save Grave automatically creates a **safety
  snapshot** of the current save (when it exists and has data), so a restore is itself reversible
  ("undo a restore" by restoring the safety snapshot).
- Restore replaces the save directory's **contents in place** rather than deleting and recreating
  the folder, so open handles (e.g. a File Explorer window) stay valid.
- The file watcher is **paused** for the duration of a restore so the app doesn't react to its own
  writes, then resumes afterward.
- A failed restore never leaves the save half-overwritten (staged copy, then swapped) and surfaces
  an error.

### Misconfiguration protection
- The backup location may not overlap the save folder (in either direction). This is validated
  when adding a game and re-checked on load; an overlapping/legacy target is flagged in the UI and
  not monitored, preventing an infinite backup-within-backup recursion.
- During a snapshot, the backup location is excluded from the copy as a second layer of defense.

---

## Managing recovery points

- **Open snapshot folder** — opens the exact directory of a recovery point in the OS file manager
  (Explorer / Finder / Linux file manager).
- **Copy snapshot path** — copies a recovery point's folder path to the clipboard.
- **Delete snapshot** — manually removes an unwanted recovery point (confirmed first). Deletion is
  serialized against backups/restores for the same game and never touches the active save.
- **Copy save/backup folder path** — per-game clipboard actions in the overflow menu.
- **Open save/backup folder** — per-game actions that reveal the directories in the file manager.

---

## Storage usage

- Shows how much disk Save Grave is consuming, so retention and manual snapshots stay
  transparent.
- **Global:** a header summary — "Save Grave backups are using 1.8 GB."
- **Per game:** total storage on each library card and in the game-details stats.
- **Per category:** the details view breaks counts down into Backups, Manual, and Safety.

---

## Last-checked indicator

- Distinguishes **Last backup** ("3 hours ago") from **Last checked** ("30 seconds ago").
- A save may not have needed a backup for hours simply because nothing changed; showing the most
  recent reconciliation reassures the user that monitoring is still running. The monitor raises a
  check event on every reconciliation pass, which the UI surfaces per game.

---

## User interface

A dark, minimal desktop UI themed from the "Inklog" color system (warm copper accent on a
charcoal palette), built on a centralized set of reusable Avalonia styles (colors, typography,
buttons, cards, inputs, status badges, dialogs, separators, timeline).

### App shell
- Branded header ("Save Grave" + tagline) with a shield mark.
- Centered content area (max ~1180px) that scales gracefully when resized.
- Default window ~1200×760 with a sensible minimum size.
- Native window frame, tinted dark to match the app on supported Windows versions.

### Protected Games library
- A global storage summary in the header ("Save Grave backups are using 1.8 GB.").
- Each game is shown as a card with:
  - A live **status dot** and **status badge**.
  - **Last backup** time (relative, e.g. "3 minutes ago") — reflects the most recent automatic
    backup **or** manual snapshot.
  - **Last checked** time — the most recent monitoring reconciliation, so the user can see
    protection is still running even when nothing has changed for hours.
  - A centered **count breakdown**: Backups · Manual · Safety · Storage.
  - **View Backups** and accent **Take Snapshot** actions.
  - A **⋮ overflow menu**: Open save folder, Open backup folder, Copy save folder path, Copy
    backup folder path, Pause/Resume protection, and Remove (danger-styled).
- A deliberate **empty state** when no games are protected yet.

### Protection status
Reusable status badge with semantic colors:

| State | Appearance |
| --- | --- |
| **Protected** | Green dot (pulsing) + green badge |
| **Backing up** | Accent (copper) dot + badge |
| **Paused** | Yellow dot + yellow badge |
| **Error** | Red dot + red badge |

### Game details (in-window)
- Clicking **View Backups** navigates the main window to a game-details screen (not a separate
  window), with a **← Protected Games** back link.
- Header shows the game name, status, a **Take Snapshot** button, and a stats block: Last backup,
  Last checked, Storage used, Save folder, plus the Backups / Manual / Safety counts.
- Three cards — **Backups**, **Manual Snapshots**, **Safety Snapshots** — each rendering its
  recovery points as a **timeline** (dot + connecting rail) with time, subtitle, size, a
  **Restore** button, and a **⋮ menu** per recovery point: Open snapshot folder, Copy snapshot
  path, and Delete snapshot (confirmed, danger-styled). Each card shows an empty-state line when it
  has no entries.

### Add Game
- Focused in-app dialog with labeled fields: Game name, Save folder (Browse), Backup location
  (Browse), and Recovery points to keep.
- Native cross-platform folder picker.
- Inline validation (existing save folder, non-overlapping backup location, at least one recovery
  point).

### Dialogs & feedback
- Dialogs appear **centered within the app** over a dimmed scrim (not separate OS windows).
- Restore uses a clear confirm dialog explaining the safety-snapshot behavior.
- Successful automatic/manual backups surface a **non-blocking toast** rather than a blocking
  dialog.

### Microinteractions
- Button hover/pressed states, card hover, input focus states, and a subtle live-status pulse.
- Relative "x minutes ago" labels auto-refresh (~every 30s) so they stay accurate without a new
  backup.

---

## Persistence & logging

### Configuration
- Protected games are persisted as a JSON file in a per-user application data directory
  (`%APPDATA%\GameSaveGuard\targets.json` on Windows; `~/.config/GameSaveGuard/...` on
  Linux/macOS), not beside the executable. The on-disk folder name intentionally keeps the
  original `GameSaveGuard` app id so existing data survives the rebrand without migration.
- Writes are atomic (temp file then move) so a crash mid-write can't corrupt the configuration.
- Configuration survives application restarts. App-wide settings persist to a sibling
  `settings.json`.

### Logging
- A file logger writes dated logs to `<app data>/GameSaveGuard/logs/saveguard-YYYY-MM-DD.log`.
- Backup, manual-snapshot, and restore operations log start/completion, and failures log the full
  exception for diagnosis. Logging never throws into the application.

### Settings
- App-wide preferences persist to `<app data>/GameSaveGuard/settings.json` (atomic write; corrupt
  or missing settings fall back to defaults).
- A **Settings** dialog (gear in the header) exposes the toggles below; changes apply and save
  immediately.

---

## Background operation & system tray

- Save Grave can keep monitoring and backing up even when the main window is closed, so
  protection does not depend on keeping a window open.
- A **system tray icon** provides a menu: Open Save Grave, Take Snapshot (all games), Pause All
  Protection, Resume All Protection, and Exit. Clicking the icon opens the window.
- **Close-to-tray** (a setting, on by default): closing the window hides it to the tray and
  protection keeps running; the app only exits via the tray's **Exit**. A one-time notice explains
  this the first time the window is closed. Turning the setting off makes closing the window exit
  the app.

## Launch at startup

- An opt-in **Launch at startup** setting starts Save Grave automatically at login — important
  for an automatic backup utility so protection doesn't depend on the user remembering to open it.
- On **Windows** this registers the app under the per-user `Run` key. On **macOS/Linux** the option
  is reported as unsupported for now (the toggle is disabled) and does nothing, pending a
  platform-native implementation (LaunchAgent / autostart `.desktop`).
- The saved preference is reconciled with the OS registration on each launch.

---

## Reliability & threading

- Filesystem monitoring and backups run off the UI thread (`async`/`await`); UI updates are
  marshaled back to the UI thread.
- Overlapping backups for the same game are prevented via a per-target lock; if changes occur
  while a backup runs, the request is coalesced and reconciliation/later events catch any
  outstanding changes.

---

## Testing

The backup engine is covered by xUnit tests (using temporary directories, not real saves),
including:

- Snapshot copies all files and nested directories, and preserves file contents.
- Failed snapshots write nothing and don't remove previous successful snapshots.
- Retention removes the oldest and never exceeds `MaxBackups` (per category).
- Restore restores contents, replaces stale files, and creates a safety snapshot of the current
  save first (skipping it only when the save is empty).
- Restore from a safety snapshot works ("undo a restore").
- Manual snapshots are stored separately from backups, restore correctly, and are retained
  independently.
- Monitoring/debounce produces a single snapshot for a burst of filesystem events; stopping and
  pausing monitoring halt backups.
- Path-overlap detection (backup folder inside save folder, and vice versa).
- Backup targets round-trip through JSON configuration and persist across store instances.
- Storage usage reports per-category and total bytes; a single recovery point can be deleted.
- App settings round-trip through JSON and fall back to defaults when missing or corrupt.

---

## Running the app

```powershell
dotnet run --project SaveGuard.Desktop/SaveGuard.Desktop.csproj
```
