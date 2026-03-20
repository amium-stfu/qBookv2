Create one subfolder per plugin in this directory.

Expected shape:
- `Host/plugins/UdlClient/UdlClient.dll`
- optional additional managed/native dependencies can stay next to the plugin DLL
- a plugin assembly references `UiEditor.Contracts.dll`
- it contains one or more non-abstract types implementing `UiEditor.Contracts.IHostPlugin`
- the host scans this directory recursively and loads those plugins on startup/build
