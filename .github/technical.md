In `uDWM.dll` there's a struct (piece of memory with settings), that struct contains the settings to choose whether a window has square or rounded (default to rounded on Windows 11)

- We download the debug symbols of the .dll
- Find the global field holding that struct
- Change the setting to ours of our choosing

All this happens in memory and nothing is permanent.

Structs may change their layout during different versions of Windows (22h2 and 24h2 differs slightly)

This caused an issue that instead of toggling `UseSquareCorners` it toggled `IsHighContrastMode` Since this bugs out windows, a simple restart of `dwm.exe` resolves the issue. Terminating the process causes Windows (winlogon.exe) to start it back up for us.