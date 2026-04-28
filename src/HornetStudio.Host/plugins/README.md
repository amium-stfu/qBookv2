Create one subfolder per plugin in this directory.

Expected shape:
- `src/HornetStudio.Host/plugins/Amium.UdlClient/Amium.UdlClient.dll`
- optional additional managed/native dependencies can stay next to the plugin DLL
- a plugin assembly references `HornetStudio.Contracts.dll`
- it contains one or more non-abstract types implementing `HornetStudio.Contracts.IHostPlugin`
- the host scans this directory recursively and loads those plugins on startup/build
