# Windows Shutdown Timer

A small Windows 11 tray app for daily shutdown reminders.

Defaults:

- 23:45: voice + Windows notification, "还有15分钟自动关机"
- 23:55: voice + Windows notification, "还有5分钟自动关机"
- 00:00: voice countdown from 10 to 1, then normal shutdown with `shutdown.exe /s /t 0`

The app does not force-close applications by default. Unsaved documents can block shutdown.

## Normal Windows Install

For normal use, install with:

```text
WindowsShutdownTimer-Setup.exe
```

The installer puts the app under your Windows user profile, creates Start menu shortcuts, can add a desktop shortcut, and can register startup at login. It does not need administrator permission.

If you do not want an installer, use:

```text
WindowsShutdownTimer-portable-win-x64.zip
```

Unzip it anywhere and run `WindowsShutdownTimer.exe`.

## Create Packages on Windows

Install the .NET 8 SDK. To create both the portable zip and normal installer, install Inno Setup 6, then run:

```powershell
.\build.ps1
```

Outputs:

```text
dist\WindowsShutdownTimer-Setup.exe
dist\WindowsShutdownTimer-portable-win-x64.zip
```

If Inno Setup is not installed, create only the portable zip:

```powershell
.\build.ps1 -SkipInstaller
```

## Settings

Runtime settings are saved to:

```text
%AppData%\WindowsShutdownTimer\settings.json
```

Startup is registered in:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

## Notes

- Windows notifications use Windows App SDK local app notifications.
- Voice reminders use `System.Speech.Synthesis.SpeechSynthesizer` and the system default voice.
- The app does not wake a sleeping computer. It only runs while Windows is awake and the tray app is running.
