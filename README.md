# Skybrush Launcher

Windows control panel for launching Skybrush Server with Skybrush Live, setting a static IPv4 address on an Ethernet adapter, and pinging an IP address.

## Run

Build with the .NET 8 SDK on Windows:

```powershell
dotnet publish .\SkybrushLauncher.csproj -c Release -r win-x64 --self-contained false
```

The launcher expects:

- Skybrush Server at `Documents\skybrush-server-2.52.0\skybrush-server-2.52.0`
- `uv.exe` installed for the current user
- Skybrush Live installed in the current user's Local AppData Programs directory

It runs the server with `etc\conf\skybrush725.jsonc`.

Applying a static IP requests administrator approval and changes only the selected Ethernet adapter.
