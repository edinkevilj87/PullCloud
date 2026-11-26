# Pull from the Cloud calculator

Windows desktop app (.NET 8, WinForms) for support staff: tells whether a US incident report can be pulled from the cloud — at least 6 hours must pass since it was created.

- Input: *Incident created on* (US local time), **State** (required), **City** (optional — needed for states that span several time zones).
- Result: **PULL AVAILABLE** / **WAIT** / **NOT YET**, with the exact pull time in your own time zone.
- Daylight saving time is handled for the US, Europe and every other location (IANA time zones).
- Without a city in a multi-zone state, the latest (safest) pull time is used.

## Build

```powershell
# Small exe (needs .NET 8 Desktop Runtime on the PC)
dotnet publish src -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o app

# Package for colleagues: runs via Microsoft's signed dotnet.exe (needs .NET 8+ Desktop Runtime).
# Unsigned .exe files are held up by the company Defender ASR rule; dotnet.exe is not.
dotnet publish src -c Release -r win-x64 --self-contained false -p:UseAppHost=false -p:PublishReadyToRun=true -p:DebugType=none -p:SatelliteResourceLanguages=en -o dist/PullCloud/app
copy src\PullCloud.ico dist\PullCloud\app\
copy launcher\*.cmd dist\PullCloud\
# + dist/PullCloud/README.txt, then zip the dist/PullCloud folder
```

## Releasing

Releases are automatic. Push a version tag and GitHub Actions ([.github/workflows/release.yml](.github/workflows/release.yml))
runs the tests, builds `PullCloud.zip` (app + `launcher/` files) and publishes it as the latest GitHub Release:

```powershell
git tag -a v1.2.0 -m "v1.2.0"
git push origin main v1.2.0
```

End-user files shipped in the zip live in `launcher/` (`README.txt`, start/shortcut scripts, `RELEASE_NOTES.md`).

## Tests

```powershell
dotnet run --project tests            # optional arg: folder for UI screenshots
```

Covers time-zone data, DST offsets and transition days, pull status logic, city matching, the paste parser, online city lookup and the main window. The tests back up and restore `%APPDATA%\PullCloud\settings.json`.

## Project layout

| Path | Purpose |
|---|---|
| `src/PullCalc.cs` | 6-hour pull rule and status logic |
| `src/TimeZones.cs` | Time-zone helpers (DST gap/overlap handling) |
| `src/UsStates.cs` | US states, zones, cities in multi-zone states, world locations |
| `src/Geo.cs` | Online city lookup (Open-Meteo geocoding, no API key) |
| `src/PasteParser.cs` | Reads pasted US date/time strings |
| `src/MainForm.cs`, `src/Ui.cs` | User interface |
| `dist/PullCloud/README.txt` | End-user guide shipped in the app folder (not in git) |
