# Building Worldfall

This folder contains the decompiled Worldfall 0.9.8.9 source and an SDK-style `Worldfall.csproj`. The project targets **.NET Framework 4.8** and produces `Worldfall.dll`.

## Requirements

- A current .NET SDK (8 or newer recommended).
- A local WorldBox installation and its managed assemblies. Game DLLs are intentionally not committed or copied to the build output.
- A `Facepunch.Steamworks` reference (`Facepunch.Steamworks.Win64.dll` or `Facepunch.Steamworks.Posix.dll`). The project will select one automatically if it is in the managed-reference folder; otherwise pass its path explicitly.
- On Windows, install the .NET Framework 4.8 Developer Pack. On Linux/macOS the project restores Microsoft's .NET Framework reference-assemblies package from NuGet.

## Build

Pass `WorldBoxDir` as the **game install directory** (the folder containing `worldbox_Data`), not the `Managed` folder:

```powershell
$game = 'C:\Program Files (x86)\Steam\steamapps\common\worldbox'
dotnet build .\Worldfall.csproj -c Release -p:WorldBoxDir="$game"
```

The project detects `worldbox_Data/Managed` (or `WorldBox_Data/Managed`) and uses the DLLs there as non-copy-local references. If your game/mod-loader setup stores reference assemblies elsewhere, override the relevant properties:

```powershell
dotnet build .\Worldfall.csproj -c Release `
  -p:WorldBoxDir="$game" `
  -p:WorldBoxReferenceDir='D:\path\to\assembly-dependencies' `
  -p:WorldBoxPublicizedAssembly='D:\path\to\Assembly-CSharp-Publicized.dll' `
  -p:WorldBoxSteamAssembly='D:\path\to\Facepunch.Steamworks.Win64.dll'
```

`WorldBoxPublicizedAssembly` is optional when the publicized game assembly is already in `WorldBoxReferenceDir`; otherwise the project falls back to `Assembly-CSharp.dll`. `WorldBoxSteamAssembly` can point at either Facepunch platform build. The mod's runtime resolver handles loading the platform-specific Steamworks assembly.

Build output is `bin/Release/net48/Worldfall.dll`. Nothing is installed into the game by default. To opt into copying the DLL to `worldbox_Data/StreamingAssets/mods`, add `-p:DeployToWorldBox=true` after confirming `WorldBoxDir` is correct.

## Notes

- The game-managed DLLs and Steamworks DLL are references only (`Private=false`); they are not bundled with the output.
- Runtime resources (language files, models, shaders, sprites, and data blobs) are embedded under their original manifest names. The code loads these names directly, so do not rename them.
- The archive's decompiler could not reconstruct `FirstPerson.VrUiTest`. Its self-test dispatch entry is disabled so the missing type does not block compilation; the VR runtime source is otherwise left in place.
- The sandbox used to prepare this project has no .NET/C# toolchain or WorldBox installation, so a real compile and in-game test still need to be run on a machine with the dependencies above.
- `net48` is the chosen target because it is the established target for the related WorldBox mod-loader project. The original Worldfall DLL was not included in the source archive, so its original target framework could not be verified.
- The source archive did not include a license file. This project setup does not add or imply redistribution rights.
