# Changelog

## 0.1.7 · release

- Windows 11's snap layouts show when the pointer rests on the maximise button, if Windows' setting for them is on (vSuite.Core 1.0.46).

## 0.1.6 · release

- The see-through background follows the window across monitors and past a screen's edge, and big windows drag smoothly (vSuite.Core 1.0.43).

## 0.1.5 · release
- An ANSI file with a character ANSI can't hold (an arrow, an emoji, another alphabet) isn't saved with "?" in its place any more - vPad says so and you choose UTF-8.
- A save that fails part-way (a full disk, a share that drops) leaves the file as it was.
- Typing while a save is still running (a server, a slow share) no longer counts as saved - the tab stays marked as changed.
- Saving a file on a server keeps what you were saving in a file beside it until the save has worked, so a save that stops part way can't leave you with neither.
- Replace all replaces every match - it stopped at 10,000 and said it was done.
- A settings.json that can't be read is kept (settings.broken-…json) and the save before it is used - vPad no longer starts again from nothing and saves over it.
- Installing an update while vPad asks "Save the changes?" waits for your answer instead of closing vPad after 20 seconds (vSuite.Core 1.0.40).
- Starting vPad with a file no longer waits for last time's tabs on a server that is off - those come in afterwards, behind what you opened.
- Typing in a very large file with the find bar open no longer crawls: it looks again when you pause.
- A tab's right-click menu stays open (it closed as soon as it opened).

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
