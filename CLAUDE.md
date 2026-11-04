# Piece of Cake: instructions for Claude

Personal Windows tray/flyout panel (C# / .NET 8 / WPF, WinForms NotifyIcon). The owner does not write C#;
Claude writes and maintains all the code. Talk to the owner in Polish.

## Rules
- The code must build without errors or warnings after every change. If you find an error (even outside the
  current task), fix it right away.
- File and folder names are English only.
- User-facing UI text is Polish; code, comments and commit messages are English.
- Never commit secrets. The Hue app key lives only in `%APPDATA%\PieceOfCake\settings.json` (DPAPI). This repo is public.
- Keep third-party licenses up to date in `THIRD_PARTY_NOTICES.md`.

## Workflow
1. Make the change.
2. `dotnet build -c Release` must pass with 0 warnings and 0 errors.
3. Run `.\deploy.bat`: it builds, closes the running app, copies changed files into
   `%LOCALAPPDATA%\Programs\Piece of Cake` and starts the app again.
4. Check `%APPDATA%\PieceOfCake\error.log` for new errors.
5. Commit with a short English message and push to `main`. The owner has given standing permission: push
   right away after every verified change, without asking for confirmation each time.
Only the owner can judge the look of the UI: ask for a screenshot when a visual change matters.

## Layout
- `Services/` Hue client, volume, power plans, metrics, tray, settings, startup, passport report.
- `ViewModels/`, `MainWindow*.cs/xaml` panel UI. `Native/` P/Invoke and DWM effects.
- `Assets/` icons (`app.ico` pink, `tray-white.ico` / `tray-black.ico` switch with the Windows theme).
- `installer/PieceOfCake.iss` Inno Setup script (needs the `publish` folder). Rebuild it only for a new full release.

## Release
Push a tag `vX.Y.Z`. GitHub Actions builds the app and attaches `PieceOfCake-win-x64.zip` and `PieceOfCake.dll`.
