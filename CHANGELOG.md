# Changelog

## 1.0.5 - 2026-09-27

### Added
- First-run setup wizard (640x540 stepper: Display / Test presets / Finish) offered
  automatically until both presets have been verified at least once.
- Output-mismatch banner ("Output doesn't match the <preset> preset") with a
  Re-apply button when a previously-verified preset drifts from the live output;
  the Movie card shows "Output differs" and the tray icon switches to Unknown.
- "Display not found" state: red banner wording, a Retry button next to the
  display picker, and disabled preset cards / Restore Desktop while the TV is
  disconnected. Detection resumes automatically on the next poll once the TV is
  back, and Retry re-checks immediately.
- CHANGELOG.md (this file).

### Changed
- Dark theme contrast fix: the old muted text colour (#6D7A8C, fails 4.5:1) is
  replaced by a new tertiary text colour (#8391A4) used everywhere `TextMuted`
  was referenced.
- Brand gradient angle changed from 18 to 105 degrees (active preset ring,
  ACTIVE pill, primary buttons, "on" toggle switches).
- Tray menu restructured into grouped sections: header (app name / mode /
  Windows HDR), Movie/Desktop radio-style items, Automatic checkbox, "Settings
  & live status...", "Choose display...", an Options submenu (Desktop fallback,
  Start with Windows) and a Tools submenu (Test next switch again, Open Windows
  HDR settings, Open activity log, Open README), then Exit and restore Desktop.
- Wording pass: "Players open: N" replaces "MPC: N"; tray tooltip now reads
  "<Mode> - 4K<Hz> RGB Full <bits>-bit - HDR <state> - Auto/Manual" (kept under
  127 chars); the keep/revert dialog window title is "MPC Movie Tray" with a
  dynamic heading "Keep the <Preset> preset?" and a "Revert now" button; the
  Windows-HDR-is-off activity log line now reads "Windows HDR is Off (reported
  only, not changed)."; the Windows HDR note under the pill is now dynamic for
  Movie / Desktop / HDR-On.
- Diagnostics log tail extended from 4 to 6 lines; the "Display access" row is
  now labelled "Display identity" to match the not-found wording.
- Desktop tray menu item and preset card text now update immediately when the
  YCbCr 4:2:0 fallback is toggled.

### Fixed
- None (no defects were reported for this release; see the manual test
  checklist below for TV verification).

### Notes
- The Movie preset remains 24 Hz (`Settings.MovieHz`, configurable), not 30 Hz,
  to avoid 3:2 pulldown judder introduced in 1.0.3. Wording that referenced
  "30 Hz" continues to use the real, dynamic MovieHz value instead.
- Windows HDR is still read-only everywhere in this release: the app reports
  it but never turns it on or off.
- Release package: `MpcMovieTray-1.0.5.zip`, containing `MpcMovieTray.exe`,
  `MpcMovieTray.exe.config`, `README.txt`, and `SHA256.txt`.

### Manual test checklist (run on the TV)
- MPC opens -> Movie applied and verified; last player closes -> Desktop.
- Manual preset pick -> "Control: Manual"; Restore Desktop is disabled on Desktop.
- Test next switch -> dialog counts down; Keep keeps it; timeout and Revert both
  roll back.
- Fallback on/off changes the Desktop mode and the card/menu text.
- Unplug the TV -> not-found state; reconnect -> recovers.
- Force a different mode in the NVIDIA Control Panel -> mismatch banner;
  Re-apply fixes it.
- Toggle HDR in Windows -> the app reports it but never changes it.
- The tray menu and settings window stay in sync.
