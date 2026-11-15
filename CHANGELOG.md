# Changelog

## 0.1.4 · release
- Behind the scenes: automated tests, test hooks (a test can drive vPad without the mouse or keyboard), and every build uses the newest shared library.
- Behind the scenes: saved passwords, the small forms and reading vTerm's connections are the shared library's (the same code as vTerm and vShell).

## 0.1.3 · pre-release
- The update notice's progress bar follows the download, instead of standing still until the end (vSuite.Core 1.0.24).
- Options: the scrollbar is the new one too, and keeps clear of the window's resize edge.

## 0.1.2
- The scrollbar is easier to grab: its thumb never gets smaller than a finger's width in a long file, it widens when you point at it, and it no longer sits on the window's resize edge.

## 0.1.1 · release
- Tabs look and work like vXplorer's: they share the row (arrows to scroll through them when they don't fit), a dragged tab slides the others out of its way, and one pulled out follows the pointer.
- A tab dragged out gets a window the size of the one it came from.

## 0.1.0
- First version: a text editor in your vShell theme (see-through and blurred like the other vSuite apps), with the vPad icon at the top left as its menu.
- Tabs: drag one out for a window of its own, onto another vPad window to move it there, or along the strip to reorder; middle-click closes; Ctrl+Shift+T opens the last closed one again.
- Syntax colours in the theme's colours for YAML, JSON, XML / HTML, INI and config files, nginx, Shell, PowerShell, Batch, Python, C#, JavaScript / TypeScript, CSS, Markdown, Dockerfile and SQL.
- Markdown side by side with a live preview (or the text or the preview alone) - headings, lists, tables, code, task lists, pictures and links.
- The side panel (Ctrl+B): Files (a folder as a tree), Servers (vTerm's saved connections - open and save files on your servers over SFTP) and Search (text in the folder's files).
- Find and replace (Ctrl+F / Ctrl+H) with match case, whole words and regular expressions; Go to line (Ctrl+G).
- Keeps each file's encoding (UTF-8 with or without BOM, UTF-16, ANSI) and line endings (CRLF / LF) - change either from the status line.
- A file changed by another program is read again (asked first if it has changes in vPad too).
- Opens the files that were open last time; a second start hands its files to the open vPad.
- Updates from vsuite.vrsion.com, as the other vSuite apps; an installer with "Edit with vPad" on files.
