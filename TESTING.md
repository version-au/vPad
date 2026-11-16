# vPad - testing

Test with a scratch settings folder so your own vPad is never touched: `set VPAD_DATA_DIR=<scratch folder>` before starting
`bin\Release\net10.0-windows\vPad.exe`. Servers reads vTerm's real settings.json (read only; `VTERM_DATA_DIR` points it elsewhere).

## 0.1.5: saving never loses text
1. `dotnet test tests\vPad.Tests`: passes (`AnsiSaysWhatItCantHold`).
2. Open an ANSI file (the status line says ANSI - e.g. a Windows-1252 file with "café"), paste an emoji or "→", Ctrl+S: "Couldn't save …:
   It's stored as ANSI, which can't hold "→" …" - the file on disk is unchanged and the tab still shows it as changed. Click ANSI on the
   status line > UTF-8, Ctrl+S: saved, and the character is there when you open it again. Without such a character an ANSI file saves as ANSI as before.
3. A save that can't write (`TextFile.Write`): the new text goes to a file beside it first, and a failure there (full disk, share gone)
   stops before the real file is touched - it used to be written straight over. Only where the swap itself doesn't work (some network
   shares) is the file written directly; if that fails too, the error says which .tmp file beside it holds what you were saving.

## 0.1.5: more ways a save could lose text; settings; updates
1. `dotnet test tests\vPad.Tests`: passes (`SettingsFileTests`).
2. Open a file on a server (or a slow share), Ctrl+S and keep typing while "Saving…" shows: when it says Saved the tab still has its changed dot; closing asks to save; Ctrl+S again and it is clean. Without typing meanwhile: clean straight away, as before.
3. A server file in a folder you can write to: while it saves, a file <name>.vpad-xxxxxx.tmp is beside it and is gone afterwards. Cut the connection during a big save: the error says the file may be cut short and names the .tmp file that holds your text. A file in a folder you can't write to (/etc/hosts as its owner): saves in place as before (no .tmp). A full disk: "The server couldn't take the file … left as it was".
4. A file with more than 10,000 of something (60,000 commas): Replace all "," with ";": "Replaced 60000" and none are left. The bar still lights up at most 10,000.
5. Close vPad, cut %APPDATA%\vPad\settings.json short (delete its last lines), start vPad: tabs, recent files and trusted servers are as at the save before; settings.broken-<time>.json holds the broken file; vpad.log says so. An empty folder (first start): as before.
6. Update with an unsaved tab: Install and restart > "Save the changes?" - wait a minute, then Cancel: vPad stays open with the text. Close it later: the new version starts then. (The log: "still has a window open - waiting for it to close".)

## 0.1.5: speed
1. Leave three tabs open on a server, close vPad, switch the server off, then double-click a .txt in Explorer: the file is there at once; "Opening … from <server>…" shows for about 12 s once (not three times) and those tabs are left out. With the server on: its tabs come in a moment later, behind the file you opened; started with no file, the tab that was in front last time is in front again.
2. A file over a million characters (a 20 MB log) with Ctrl+F open and a search with many matches: typing in the text is as quick as with the bar closed; the highlights come back 0.4 s after you stop. F3 / Enter / Replace right after typing still go to the right place. A small file: highlights follow every key, as before.

## 0.1.4: tests and test hooks
1. `dotnet test tests\vPad.Tests`: passes.
2. Start a built vPad.exe with VPAD_TEST_HOOKS=1 (and a scratch VPAD_DATA_DIR); `vSuite.Core\scripts\test-send.ps1 -ProcessId <pid> -Command "dump C:\temp\s.txt"`:
   the file lists its windows and tabs. Without the variable the same message does nothing.

## 0.1.3: the update's progress bar (vSuite.Core 1.0.24)
1. Update to a newer release from this version (the running copy downloads it): the notice's bar and percentage move steadily with the
   download, and the window stays responsive during "Checking and installing…". (Updating TO this version still shows the old behaviour -
   it's the version doing the download that matters.)
2. Settings / Options made small enough to scroll: its scrollbar is the thin one that widens when pointed at, with a ~40 px thumb, and sits a
   little in from the window's right edge - grabbing it never starts a resize.

## 0.1.2: scrollbars (vSuite.Core 1.0.23). Open a long file (tens of thousands of lines): the scrollbar's thumb is about 40 px tall, not a sliver; dragging it from top to bottom
   goes through the whole file.. Point at the scrollbar: the thumb widens (6 to 10 px) and stays wide while dragged.. The window not maximised: there's a small gap between the scrollbar and the window's right edge - grabbing the scrollbar never starts
   a resize, and the edge still resizes. Maximised: the text goes to the edge.. The side panel's and the Markdown preview's scrollbars look the same.

## 0.1.1: vXplorer's tabs (vSuite.Core 1.0.21 TabStrip)
1. Tabs look like vXplorer's: a card with rounded top corners, the file's dot, the name, the Main Accent line under the chosen one; close
   shows on the chosen tab and under the pointer; an unsaved file shows a dot there until the pointer comes.
2. Open a dozen files: the tabs get narrower (down to 90 px), then arrows show either side - they scroll one tab at a time (greyed at
   the ends); the wheel over the tabs scrolls them. The chosen tab is scrolled into sight.
3. Drag a tab along the strip: it follows the pointer, the others slide aside, and it glides into place on release. Near either end of a
   strip that scrolls, it scrolls along.
4. Pull a tab down out of the strip: it fades and a copy follows the pointer. Let go on empty desktop: a new window of the same size,
   its tab under the pointer. On another vPad window's tabs: it joins there, where it was let go. Back over its own window: nothing.
5. A window's only tab: dragging it moves the window; let go on another window's tabs and it joins them (the empty window closes).
6. Esc while dragging: everything goes back.

## 0.1.0

**Window and theme**
1. The window is in the vShell theme with its glass (Options > Look > Use the theme's opacity off = solid); change vShell's theme: vPad
   follows straight away, the syntax colours too. The icon at the top left (a notepad in the theme's colours) opens the menu.
2. Drag the empty part of the top row to move; double-click it to maximise; resize from the edges. Close and start again: same place.
3. Pin: kept on top (every window).

**Opening and saving**
1. `vPad.exe a.yml b.md --line 6`: both open, a.yml at line 6. With vPad running, the same command: they open in the running vPad.
2. Ctrl+O (several files), drag files onto the window, Open recent in the menu. A folder dropped: shown in Files.
3. A program or picture: "doesn't look like text - open anyway?". Over 20 MB: asked first; over 200 MB: refused.
4. Type: a dot on the tab, "•" in the window title. Ctrl+S: saved, "Saved at 3:42 PM" in the status line; the file keeps its encoding
   (UTF-8 BOM stays, ANSI stays ANSI) and line endings. A new file: Ctrl+S asks where (Save as).
5. Status line: click the encoding / line endings to change them (the tab shows unsaved); the language to change the colours; Spaces to
   change tabs, or turn a file's leading tabs into spaces (one undo).
6. Change an open file in another program, come back to vPad: it's read again ("changed outside vPad"); with changes in vPad too: asked.
7. Ctrl+W with changes: Save / Don't save / Cancel. Closing a window with changes: one question for all of them.
8. Close vPad with files open, start it again: they open again (servers too, if their connection signs in without asking), the same one
   in front, at the line it was on. Options > General can turn that off.

**Tabs and windows**
1. + / Ctrl+N: "Untitled 1, 2 …" (opened files don't use up the numbers). Opening a file while the only tab is an empty new one: it
   takes its place.
2. Drag a tab 8 px: a label follows the pointer. Let go on empty desktop: a new window there with that tab. Onto another vPad window
   (its tab strip, or anywhere on it): it moves there, at that place in the strip. Along its own strip: it moves along. The last tab
   leaving a window closes it. A window's only tab dragged onto the desktop: the window moves there. Esc while dragging: nothing moves.
3. Right-click a tab: close / close the others / to the right, Move to a new window (Ctrl+Shift+O), Move to the window with …, Copy the
   path, Show in its folder, Read it again. Middle-click closes. Ctrl+Tab / Ctrl+PageDown move between tabs. Ctrl+Shift+T: the last
   closed file again.

**Side panel** (Ctrl+B)
1. Files: the folder of the first file opened (or Choose a folder…); folders open with a click; a click on a file opens it. Up a folder,
   Refresh (open folders stay open). Right-click: Show this folder here, Show in its folder, Copy the path.
2. Servers: vTerm's saved connections in their folders. Click one: it connects (the server's key trusted by vTerm is accepted; a new one
   is asked about) and shows its home folder (or vTerm's SFTP folder for it), with "/ (the whole server)". A key the server turns down:
   the saved password is tried, else asked (kept until vPad closes). Open a file: its tab shows the server's name. Save it: "Saved to
   docker-vm"; a file the user can't write (root's): the message says so and offers Save as (a copy on this PC).
3. Search: type and Enter - the lines with it in the folder's text files (not .git / node_modules / bin / obj, files over 2 MB, or
   programs), by file; click one: opened at that line.

**Find** (Ctrl+F)
1. The selected word goes in; every match lit up, "3 of 12"; Enter / F3 next, Shift+Enter / Shift+F3 back, round the ends. Aa, ab, .*
   (a bad expression says so). Ctrl+H: Replace / Replace all (one undo; $1 works with .*). Esc closes, the keyboard back in the text.

**Markdown**
1. A .md file: the Text / Side by side / Preview switch at the top (Ctrl+Shift+M). The preview follows typing a moment later and keeps
   where it was scrolled; headings, lists, task lists, quotes, code, tables, pictures beside the file; links open in the browser, a link
   to another .md opens it in vPad.

**Keys**: Ctrl+N / Ctrl+Shift+N (new window) / Ctrl+O / Ctrl+S / Ctrl+Shift+S / Ctrl+W / Ctrl+G / Ctrl+B / Ctrl+Shift+F (search) /
Alt+Z (wrap) / Ctrl + wheel, Ctrl+= / Ctrl+- / Ctrl+0 (text size).

**Installer** (only on a release build): installs to %LOCALAPPDATA%\Programs\vPad; "Edit with vPad" on a file opens it; vPad is in
"Open with" for text files; uninstall offers to delete %APPDATA%\vPad.
