MPC MOVIE TRAY 1.0.2
Panasonic DX900 / NVIDIA / Windows 10 or 11 x64

START HERE
1. Close the previous PowerShell watcher and MPC, and let any recovery finish.
2. Extract this entire ZIP to a permanent folder.
3. Double-click MpcMovieTray.exe. Choose your TV when prompted.
4. Look in the Windows notification area (including the hidden-icons arrow).
   You can drag the icon out of the overflow area to keep it visible.
5. Click the tray icon to toggle movie/desktop. Right-click for all controls.

This is a compiled Windows GUI executable: no PowerShell console, installer,
Internet access or NVIDIA Control Panel automation is required. It uses the
installed NVIDIA driver and Windows display APIs. It requires .NET Framework
4.8 (included with Windows 11 and recent Windows 10 releases).
The build is unsigned, so Windows may show an unknown-publisher warning.
Source code and a local Build.cmd are included for inspection/rebuilding.

THE TWO PRESETS
Movie:    3840 x 2160, 24 Hz, RGB Full, 10 bits per colour channel.
Desktop:  3840 x 2160, 60 Hz, RGB Full, 8 bits per colour channel.

Every application checks resolution, refresh rate, colour format, output range
and bit depth after the switch. It does not silently report success for a
substituted 8-bit movie mode or a different colour format.

RGB Full at 4K60 remains unconfirmed on your particular connection. The tray
menu includes an EXPLICIT desktop fallback: 4K60 YCbCr 4:2:0 Limited / 8-bit.
Select that option, then select Desktop to test it. It never activates silently.

FIRST-TIME TESTS AND ROLLBACK
Each new display/preset combination asks you to Keep or Revert within 15 seconds.
No confirmation means automatic rollback to the captured previous output.
The confirmation is remembered; future switches to that preset do not interrupt
playback. Use "Test next switch again" after changing your HDMI setup or driver.
The test countdown starts after the mode is applied; the screen may go black
briefly while the driver switches. Rejected/unverified changes also roll back.

An independent helper process watches unfinished changes. If the main app is
killed during a switch, it attempts rollback. If the UI hangs during a test,
the helper terminates this app's exact process instance after the longer safety
deadline (35 seconds after showing confirmation; 60 seconds during application)
and attempts restoration. Recovery can take several extra seconds.

CONTROLS
Left-click icon      Toggle presets and switch to Manual control.
Right-click          Open the menu.
Show live status     Opens the settings window: display selector, preset cards,
                     live output, HDR indicator, toggles and diagnostics.
Automatic            Movie mode whenever MPC-HC or MPC-BE is open.
Choose TV/monitor    Select the target display; return the old one to Desktop first.
Windows HDR settings Open Windows' HDR settings; it does not toggle HDR itself.
Start with Windows   Optional, off by default; current-user startup only.
Exit                 Apply/verify the Desktop preset and exit.

Green M icon = verified movie preset.
Blue D icon  = verified desktop preset.
Amber ? icon = another mode, unavailable display or unverified settings.
Hover text shows the current refresh rate, format, bit depth, HDR and control mode.
Right-click and the live-status window provide the full resolution and range.

AUTOMATIC MPC MODE
Automatic is enabled by default on a new setup. It watches the player PROCESS,
not playback: launching MPC, opening a file in MPC, or an already-open MPC instance
activates movie mode. Pause, stop, minimise, change files or reach the end of a
video: movie mode stays active. Audio files and an empty player count too.
It returns to Desktop after the last MPC instance closes, confirmed by two
consecutive one-second checks. It then waits for the next launch.

Supported standard names: mpc-hc.exe, mpc-hc64.exe, mpc-be.exe, mpc-be64.exe.
Only the current Windows session is monitored. Renamed player executables are
not detected. The Web Interface is not required.

Manual selection pauses Automatic until you explicitly re-enable it. This avoids
MPC immediately undoing your choice. The app does not change file associations.

HDR STATUS
The app reads Windows HDR state for the SELECTED display, not the primary monitor.
On Windows versions supporting the newer API, it distinguishes HDR from SDR/WCG.
If the older API only reports advanced colour enabled, the label is
"Unknown (advanced colour on)" rather than claiming HDR is definitely active.
An unreadable/disconnected display produces Unknown, never a fabricated On state.

Selecting movie mode while Windows HDR is Off gives a non-blocking notification.
The player may subsequently enable HDR; the status is polled and updated.
Windows HDR status does not prove that the file is HDR or that the TV has entered
HDR mode through a separate renderer/driver path. This app does not inspect video
metadata, control RTX Video HDR, or modify Windows HDR or HDR passthrough.

PICTURE QUALITY / PANASONIC DX900
RGB preserves full colour detail and is preferred for desktop text and coloured
edges. YCbCr 4:2:0 reduces colour resolution and may blur those edges. Use the
4:2:0 desktop fallback only if RGB at 4K60 is unavailable/unreliable.

For RGB Full, the TV's HDMI RGB range must match Full, or Auto must detect it
correctly. The explicit YCbCr fallback uses Limited range; the TV must interpret
that correctly too. A mismatch can cause washed-out blacks or crushed detail.
Full is not inherently better than a correctly matched Limited signal.

Use HDR passthrough for native HDR10 movies. RTX Video HDR is an optional SDR-to-
HDR conversion and is not required to watch native HDR. 10-bit output alone does
not enable HDR. Configure Windows and the video renderer for your desired HDR
behaviour, and enable HDMI HDR for the TV input.

MOVIE REFRESH RATE
The movie preset targets 24 Hz so that 24 fps films play one frame per refresh,
instead of being spread over 30 Hz by 3:2 pulldown. That pulldown is the usual
cause of judder in 24p material. The rate is stored as MovieHz in settings.json
and can be changed without rebuilding.

Windows reports refresh rates as whole numbers: Settings > System > Display >
Advanced display shows 24 for 24.000 Hz and 23 for 23.976 Hz. Most films are
23.976, not 24.000. On a 24.000 Hz output, 23.976 fps content repeats a frame
roughly every 42 seconds. Setting MovieHz to 23 removes that, if the TV
advertises a 23 Hz mode. Check that dropdown on the TV and use whichever rate it
actually offers; both are a large improvement on 30 Hz.

If the requested rate is not advertised, the switch fails cleanly with "No
supported progressive 24 Hz mode at the requested resolution" and nothing is
changed. Changing MovieHz also changes the preset's confirmation key, so the
next switch to it is tested again with the 15-second rollback.

This app still does not implement automatic per-file frame-rate matching, 1440p
or 12-bit presets, and does not claim those are confirmed capabilities of your
TV.

EXIT, CRASHES AND RECOVERY
Normal Exit deliberately returns to the selected Desktop preset even if MPC is
still open. Cancelling a first-use Desktop test leaves the app running. Closing
only the live-status window keeps the app in the tray.

While movie mode is active, the app keeps a recovery snapshot of the output from
before the movie session. After an unexpected main-app exit:
- An unfinished transition is rolled back first.
- In Automatic mode the helper waits until MPC closes, then restores the saved
  pre-session output (which may differ from the explicit Desktop preset).
- In Manual mode the helper restores the saved pre-session output immediately.
If the app started in a mode it did not itself change, it cannot reconstruct an
unknown earlier desktop setting.

The helper appears as a second MpcMovieTray.exe process. It is not a service.
The app and old PowerShell watcher use the same single-instance lock to prevent
competing changes. If recovery is holding the lock, close MPC and wait before
starting another copy. Do not delete/move the app while it or its helper runs.

Recovery files and activity.log are in %LOCALAPPDATA%\MpcMovieTray.
Failed restoration retains the recovery files; reconnect the original monitor
and layout, close MPC and relaunch the app. A previous unfinished session is
recovered before accepting new changes. The old PowerShell package's recovery
files are separate: use its Restore.cmd for an unfinished old-script session.
OS/GPU crashes, power loss, removal of the monitor, or killing both processes can
prevent automatic recovery. Do not delete recovery files to dismiss an error.

LIMITATIONS
- Windows x64 with .NET Framework 4.8 and an installed NVIDIA driver.
- The selected display must be directly driven by NVIDIA.
- Supported, advertised progressive 3840 x 2160 modes at 30 and 60 Hz are required.
  No custom timings are generated and 29/59 Hz modes are not silently substituted.
- Standalone/extended desktop only. Duplicate/clone and Surround are unsupported.
  Keep the selected monitor connected and its layout/identity unchanged.
- Disable automatic refresh-rate switching in MPC/madVR/other renderers. External
  apps or Windows can override display settings; the tray shows the actual state
  but does not repeatedly fight another program's changes.
- NVIDIA or Windows may reject manual output settings in particular HDR modes.
  A failed verification causes rollback and pauses Automatic control.
- The 15-second tests check user-visible operation, not signal integrity over hours.

STARTUP / REMOVAL
Start with Windows is opt-in. It writes only the current user's Run entry named
MpcMovieTray. Keep the app in a permanent folder before enabling it. To remove:
turn off Start with Windows, choose Exit, wait for recovery to finish, then delete
the extracted folder. Preferences/logs remain in the LocalAppData folder above.

VALIDATION
Built as a Windows GUI-targeted .NET Framework executable (PE32+ x64).
Compilation, native-structure layout, preset verification logic, HDR-state
interpretation, MPC lifecycle policy and persistence/recovery data are checked.
The GUI and NVIDIA hardware switching cannot be run in the Linux build environment;
first-use confirmation tests on your PC are therefore essential. No claim is made
that this executable has already been hardware-tested on your DX900.

SOURCE / BUILD
src contains the complete source and application manifest.
Build.cmd invokes the Windows .NET Framework C# compiler, without NuGet downloads.
This folder is the portable app; MpcMovieTray.exe.config specifies .NET Framework 4.8.
No administrator privileges are requested. The application has no Internet client.

REFERENCES
https://docs.nvidia.com/nvapi/group__dispcontrol.html
https://github.com/NVIDIA/nvapi/blob/main/nvapi.h
https://github.com/NVIDIA/nvapi/blob/main/nvapi_interface.h
https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_device_info_type
https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/wingdi.h

SETTINGS WINDOW (1.0.2)
"Show live status / controls" opens a dark settings window in the app's cyan-to-
pink theme. It holds the display selector, both preset cards with the ACTIVE one
marked, the live actual output (resolution, refresh rate, colour format, range
and bit depth), a SEPARATE Windows HDR indicator (On / Off / Unknown), an "Open
Windows HDR settings" button, the Automatic and Start with Windows toggles, a
Restore Desktop button, the Advanced YCbCr 4:2:0 fallback checkbox and an
expandable Diagnostics section.

Bit depth and Windows HDR are deliberately shown as independent indicators:
selecting the 10-bit movie preset does NOT mean Windows HDR is enabled. The
window only displays state and requests changes. All switching, verification,
rollback and recovery behaviour is unchanged from 1.0.1.

The tray menu uses the same dark theme, and the 15-second confirmation shows a
countdown bar with Keep settings / Restore now.

UI TEST MODE
Running MpcMovieTray.exe --uitest opens the settings window with fake data for
checking layout, DPI scaling and the confirmation dialog on any machine. It makes
no NVIDIA or display API calls, takes no single-instance lock, starts no recovery
helper and saves nothing. Selecting a preset opens the real 15-second confirm
dialog so the countdown and Restore button can be exercised safely; "Open Windows
HDR settings" cycles the HDR indicator through On / Off / Unknown. Close the
window to exit. Build.cmd also refreshes SHA256.txt after a successful build.

ICON UPDATE (1.0.1)
The supplied cyan-to-pink HDR monitor artwork is embedded in the executable
as a multi-resolution Windows icon (16 through 256 pixels). The original PNG
and ICO are included in assets. Build.cmd embeds the icon on rebuild.
Tray icons use a simplified monitor silhouette: green M = movie preset,
blue D = desktop preset, amber ? = other/unknown settings. These icons do
not assert that Windows HDR is enabled; check the separate HDR status.

UPDATING
Exit the previous tray app, then extract this package over the same folder
and launch MpcMovieTray.exe. Your saved preferences remain in LocalAppData.
Keeping the same folder preserves the existing startup registration.
