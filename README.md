# vPad

A text editor for Windows 11 in your vShell theme, part of the vSuite (vShell, vXplorer, vView, vTerm).

- Tabs that drag between windows, syntax colours for config and script files, Markdown with a live preview.
- A side panel with your folder, your servers (vTerm's saved connections, over SFTP) and search in files.
- Keeps each file's encoding and line endings; updates itself from https://vsuite.vrsion.com.

Build: `build.cmd` (needs the .NET 10 SDK; Inno Setup 6 for the installer). A fresh clone needs `git submodule update --init`.

Made with AvalonEdit (MIT), Markdig (BSD-2) and SSH.NET (MIT).

## Licence

Copyright (C) 2026 Steven Van Teylingen (version-au). vPad is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License, version 3 - see [LICENSE](LICENSE). It comes with no warranty. The shared library vSuite.Core is MIT, and
the parts by others are listed in [THIRD-PARTY.txt](THIRD-PARTY.txt).

Source: https://github.com/version-au/vPad · Downloads and updates: https://vsuite.vrsion.com
