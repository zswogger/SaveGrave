<div align="center">
  <img src="SaveGrave.Desktop/Assets/save-grave-logo.png" width="120" alt="Save Grave logo" />
  <h1>Save Grave</h1>
  <p><em>Automatic, versioned backups for your game saves.</em></p>
</div>

Save Grave is a cross-platform desktop app that watches your game save folders and quietly
creates versioned recovery points whenever your saves change — so if a save is deleted,
corrupted, or overwritten, you can roll back to an earlier one.

## Download

Grab the latest Windows build from the [**Releases**](../../releases) page:

1. Download `SaveGrave-vX.Y.Z-win-x64.zip`.
2. Extract it and run `SaveGrave.exe`. (It's self-contained — no .NET install needed.)
3. On first launch Windows SmartScreen may warn because the build isn't code-signed yet. Click
   **More info → Run anyway**. You can verify your download against the published `.sha256`
   checksum.

## What it does

- **Automatic backups** when your save files change (debounced so a single save = one snapshot).
- **Manual snapshots** on demand, and **safety snapshots** taken automatically before any restore.
- **One-click restore** to any recovery point, with your current save preserved first.
- **Runs in the background** from the system tray; optional launch at startup.
- Shows **storage usage**, **last backup**, and **last checked** so you know protection is active.

See [FEATURES.md](FEATURES.md) for the full list.

## Your data stays yours

Save Grave is a local backup tool. It only reads your save folders and writes copies to the
backup location you choose. It **never** modifies your active save during normal monitoring —
the only time it writes to a save folder is when you explicitly restore, and even then it makes
a safety snapshot of the current save first. No accounts, no cloud, no telemetry.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build SaveGrave.slnx            # build everything
dotnet test SaveGrave.slnx             # run the tests
dotnet run --project SaveGrave.Desktop     # run locally
```

To produce a release build like the one on the Releases page:

```bash
dotnet publish SaveGrave.Desktop/SaveGrave.Desktop.csproj -c Release -r win-x64 -o publish/win-x64
```

## License

See [LICENSE](LICENSE).
