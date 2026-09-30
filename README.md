# Razer Battery Tray

A small Windows tray app that shows the battery level of a **Razer DeathAdder V2 X HyperSpeed** mouse. It reads the battery directly from the USB dongle, so **Razer Synapse is not needed**.

## What it does

- Shows the battery percentage as a number on a tray icon.
- Icon color: green above 30%, amber at 30% or below, red at 15% or below, grey with a `?` when no reading is available.
- Hover over the icon for a tooltip, for example `DeathAdder V2 X: 51%`.
- Left-click the icon to refresh. Right-click for **Refresh** and **Exit**.
- Refreshes automatically every 5 minutes.
- Shows **Unavailable** instead of an old value when the mouse is asleep or off, or when the dongle is unplugged.
- Only one copy can run at a time.

## Requirements

- Windows 10 or 11 (64-bit).
- Razer DeathAdder V2 X HyperSpeed connected with its **USB dongle** (2.4 GHz HyperSpeed mode). USB ID `1532:009C`.
- Bluetooth mode is not supported.

This has only been tested on the DeathAdder V2 X HyperSpeed. Other Razer mice use different settings and will probably not work without changes.

## Install

1. Go to the **Releases** page of this repository and download `RazerBatteryTray-Setup.exe`.
2. Run it. If Windows shows "Windows protected your PC", click **More info**, then **Run anyway**. The installer is not digitally signed.
3. Keep **Start Razer Battery Tray when I sign in to Windows** ticked if you want it to start automatically.

The installer needs no administrator rights and includes the .NET runtime, so nothing else has to be installed.

**To uninstall:** Windows Settings, then Apps, then Installed apps, then Razer Battery Tray.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

Run the app directly:

```powershell
dotnet run
```

Build the installer (also needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
winget install -e --id JRSoftware.InnoSetup
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The installer is created at `installer-output\RazerBatteryTray-Setup.exe`.

## How it works

The app finds the dongle's control interface (USB `1532:009C`, the interface with a 91-byte feature report), sends Razer's "get battery level" command, and checks the reply's status, command and checksum before showing a value. The raw 0 to 255 battery value is converted to a percentage. It uses the [HidSharp](https://www.zer7.com/software/hidsharp) library.

The packet layout and battery command come from the [OpenRazer](https://github.com/openrazer/openrazer) project's documentation of Razer's protocol.

## Project files

| File | Purpose |
| :-- | :-- |
| `Program.cs` | The whole app: tray icon, battery reading, icon drawing |
| `RazerBatteryTray.csproj` | Project settings |
| `installer.iss` | Inno Setup script for the installer |
| `build-installer.ps1` | Builds the app and the installer in one step |

## Disclaimer

This is an unofficial personal project and is not affiliated with or endorsed by Razer. Razer and DeathAdder are trademarks of Razer Inc. The app only reads the battery level and does not change any settings on the mouse. Use it at your own risk.
