Create one subfolder per plugin in this directory.

Expected shape:
- `Host/plugins/UdlClient/UdlClient.dll`
- optional additional managed/native dependencies can stay next to the plugin DLL
- a plugin assembly references `Amium.Contracts.dll`
- it contains one or more non-abstract types implementing `Amium.Contracts.IHostPlugin`
- the host scans this directory recursively and loads those plugins on startup/build
