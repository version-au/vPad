# vPad - notes for Claude Code

vPad is a Windows 11 text editor (C# / .NET 10, WPF) in vShell's look, part of V's suite with vShell, vXplorer, vView and vTerm: tabs
that drag between windows, syntax colours, Markdown with a live preview, a side panel (Files / Servers / Search), files on servers over
SFTP with vTerm's saved connections. Started 2026-11-08 (V picked it from the app ideas; the mock-ups and icon D - the tall spiral
notepad - were chosen on a claude.ai canvas). Owner: V (Steven).
Repo: https://github.com/version-au/vPad (branch `main`, GPL-3.0). This machine's notes (working folder, remotes): `CLAUDE.local.md` (not in git).

## The shared library: vSuite.Core

- **The suite's design guide** is `vSuite.Core\docs\design-guide.md` (the window, theme, icon, controls, keys, words, Options, release): follow it for anything new here.
- **The changelog says which builds were which** (vSuite.Core 1.0.28): every build marks its version's heading in CHANGELOG.md " · pre-release" (`build.cmd pre`) or " · release" (a release replaces the pre-release mark) - commit CHANGELOG.md after each build. Write new headings without a mark.

- Theme, glass, styles, Ask, Instance, Log, updater, update notice, Options blocks, AppIcon / WindowIcons come from **vSuite.Core**
  (https://github.com/version-au/vSuite.Core, MIT), the git submodule `vSuite.Core\` - read its CLAUDE.md.
  A fresh clone needs `git submodule update --init`. Program calls `App.Init("vPad", "VPAD", "https://vsuite.vrsion.com")`.
- A change to a shared piece is made in vSuite.Core (with its tests), pushed there, then `git -C vSuite.Core pull` here and committed
  with vPad's own version bump.

## Build and run

- `dotnet build vPad.csproj -c Release` -> `bin\Release\net10.0-windows\vPad.exe` (or `-o` a scratch folder while a vPad runs).
- "Release build" = `build.cmd upload` (by full path on a network share - see `CLAUDE.local.md`): `dist\vPad.exe` (self-contained,
  one file), `dist\setup\vPad-setup.exe` (Inno Setup, `installer\vPad.iss`, per user, "Edit with vPad", Open with), `dist\site\vpad\`
  for the update site; `upload` puts it on the site (`pre` = a pre-release). Only when V asks.
- Log: `%APPDATA%\vPad\vpad.log`. Settings: `%APPDATA%\vPad\settings.json` (`AppSettings`). Tests: `VPAD_DATA_DIR` = a scratch folder.
- `vpad.ico` = `vPad.exe --make-icon vpad.ico` (the drawing is `src\Ui\Brand.cs`), remade when the drawing changes.
- NuGet: AvalonEdit (the editor), Markdig (the preview), SSH.NET (SFTP).

## House rules (as in vShell, vXplorer, vView and vTerm)

- **Only make a release build when V asks.** Otherwise just make the change and commit it.
- Every user-visible change: bump `<Version>` in `vPad.csproj` (0.1.N - next N once the current one has been given to V), a section at
  the top of **CHANGELOG.md** (`## 0.1.N`, one short line per change), test steps in **TESTING.md**.
- Commit messages: `0.1.N: what changed` (short, lower-case after the colon), one commit per change set.
- Times shown to the user are 12-hour ("h:mm tt"). UI text is plain, friendly English. Colours are `vx.*` resources.

## Layout

- `src/Program.cs` - entry: `--make-icon`, `--update-watchdog`, one copy per settings folder (`Instance.HandOff(StartRequest.ToHandOff(args))`
  - the running vPad gets "vpad:" + the files as JSON over WM_COPYDATA), the `Remote` callbacks (questions on the UI thread), the first window.
- `src/Core/` - `AppSettings` (text, side panel, open tabs, recent files, trusted server keys, window, updates), `TextFile` / `TextFormat`
  (encoding detection: BOM, strict UTF-8, else the ANSI code page; line endings; safe write via a temp file + File.Replace), `Languages`
  (file name -> language -> src\Syntax\*.xshd), `Servers.cs` (RemoteEntry / RemotePath only - vTerm's connections are vSuite.Core's `VTermConnections` / `VTermConnection` since 1.0.27),
  `Remote` (SSH.NET SFTP: one client per connection, reconnects; key, then the saved secret as a password, then asks - as OpenSSH does;
  host keys: vTerm's HostKey or vPad's own `HostKeys`; writes in place so owner / mode stay), (DPAPI secrets: vSuite.Core's `Secret`.)
- `src/Syntax/*.xshd` - AvalonEdit definitions with shared colour names (Comment, String, Number, Keyword, Key, Type, Punct, Variable,
  Heading, Bold, Italic, Code, Link, Tag, Attr); `Ui/Syntax.cs` paints them from the theme (keys / keywords = Main Accent, variables /
  attributes = Secondary Accent, strings / numbers / types = green / orange / yellow for a dark or light theme).
- `src/Ui/MainWindow.cs` - the window (WindowChrome, caption row 40 = icon menu, side panel button, tabs + "+", Markdown switch, update /
  pin / min / max / close; side panel + GridSplitter; editor host with the FindBar and UpdateNotice; status line with encoding / line
  ending / language / indent menus), keys, theme. `.Files.cs` - tabs in the window (`Attach` / `Detach` hook and unhook per tab -
  `Hooks`), opening (local / remote, already-open check across windows), saving, closing, session (the LAST window's tabs are saved),
  disk-change check on Activated. `.TabDrag.cs` - the tab strip is vSuite.Core's `TabStrip` (1.0.21, vXplorer's look and drag): it
  hears Picked / CloseClicked / MenuRequested / Reordered (keeps `_tabs` in the same order) / MovedTo (another window's strip: Detach +
  `Adopt`) / TornOut (`MoveToNewWindow` under the pointer); the only tab moves the window (the strip does it).
- `DocTab` - one file: AvalonEdit `TextEditor` (moved between windows as is), its `StripTab` (moves with it), path or server + remote path, TextFormat,
  language, dirty = `UndoStack.IsOriginalFile`; Markdown: the editor + GridSplitter + `MarkdownView` in its Content grid.
- `MarkdownView` - Markdig AST -> FlowDocument (themed via vx.* resource references), redrawn 300 ms after typing, keeps the scroll.
- `SidePanel` - Files / Servers / Search; `Node` has the shape vSuite's TreeViewItem style binds (Name, Icon, IndentMargin, IsExpanded,
  IsSelected, IsPlaceholder, IsDropTarget, Children) and loads children when first expanded. `Icons` - coloured dots / glyph images.
- `FindBar` (find / replace overlay, its own highlighter), `FormWindow` / `Looks` (vSuite.Core's, 1.0.27: prompts), `OptionsWindow`
  (OptionsWindowBase: General, Text, Look, Updates, About), `Brand` (the icon), `StartRequest` (command line / hand-off).

## Things learned the hard way

- WPF draws the variable Cascadia fonts (the only ones Windows 11 ships) too heavy - the default font is Consolas.
- Test screenshots: capture vPad's own window with PrintWindow (another app may be in front); only click when vPad is the foreground
  window; menus are layered windows - a screen capture needs CAPTUREBLT. PowerShell variable names ignore case (`$h` vs `$H`).
- The global TreeViewItem style in vSuite's Controls.xaml binds to FolderNode-shaped data - any tree here must offer those properties.

## Tests

- `dotnet test tests\vPad.Tests` (xunit) - the parts with no window: reading and saving files (encodings, BOM, line endings, the safe write), languages from file names, the command line. Run them after changing `src\Core` or the command line; add tests for new window-less code.
- `VPAD_TEST_HOOKS=1`: a test drives vPad by WM_COPYDATA (`src\Ui\MainWindow.TestHooks.cs` - open / new / type / save / saveas / close / closewindow / tab / line / find / replaceall / markdown / panel / folder / wrap / newwindow / moveout / options / dump), sent with `vSuite.Core\scripts\test-send.ps1 -ProcessId N -Command "…"`; `VPAD_TEST_ASK=yes|no` answers questions. **Use these, not the real mouse or keyboard** (that took over V's PC and once clicked into another app).
