# Windows Shutdown Timer

A small Windows 11 tray app for daily shutdown reminders.

Defaults:

- 23:45: voice + Windows notification, "还有15分钟自动关机"
- 23:55: voice + Windows notification, "还有5分钟自动关机"
- 00:00: voice countdown from 10 to 1, then normal shutdown with `shutdown.exe /s /t 0`
- The settings window shows the remaining time until the next scheduled shutdown.

The app does not force-close applications by default. Unsaved documents can block shutdown.

## Normal Windows Install

For normal use, install with:

```text
WindowsShutdownTimer-Setup.exe
```

The installer puts the app under your Windows user profile, creates Start menu shortcuts, can add a desktop shortcut, and can register startup at login. It does not need administrator permission.

## Create Packages on Windows

Install the .NET 8 SDK and Inno Setup 6, then run:

```powershell
.\build.ps1
```

Outputs:

```text
dist\WindowsShutdownTimer-Setup.exe
```

## Settings

Runtime settings are saved to:

```text
%AppData%\WindowsShutdownTimer\settings.json
```

User defaults saved from the settings window are saved to:

```text
%AppData%\WindowsShutdownTimer\defaults.json
```

Uninstalling from Windows Settings or the Start menu shortcut removes the installed app files, startup registry entry, and the `%AppData%\WindowsShutdownTimer` settings folder.

Startup is registered in:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

## Notes

- Windows notifications use Windows App SDK local app notifications.
- Voice reminders use `System.Speech.Synthesis.SpeechSynthesizer` and the system default voice.
- The app does not wake a sleeping computer. It only runs while Windows is awake and the tray app is running.
