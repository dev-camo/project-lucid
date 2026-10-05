# Dependency notices

Project Lucid uses Unity 2022.3.54f1 and packages distributed through Unity's
Package Manager. Package versions are recorded in `Packages/manifest.json` and
`Packages/packages-lock.json`; each package retains its upstream notices.

Extraction and analysis use separate tools downloaded into the local cache:

| Tool | Version | Source and license |
| --- | --- | --- |
| AssetRipper | 2.0.0 | [AssetRipper](https://github.com/AssetRipper/AssetRipper), GPL-3.0 |
| Cpp2IL | b5ad444b82267cb1e4b88b8b373c008105bdea52 | [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL), MIT |
| Il2CppDumper | 6.7.46 | [Il2CppDumper](https://github.com/Perfare/Il2CppDumper), MIT |
| .NET SDK | 10.0.401 | [Microsoft .NET](https://dotnet.microsoft.com/), distribution notices |
| .NET runtime | 6.0.36 | [Microsoft .NET](https://dotnet.microsoft.com/), distribution notices |

Download locations and SHA256 hashes are recorded in `tools/tool-lock.json`.
Downloaded distributions retain their own licenses. Game assets are supplied
by the user and are not distributed in this repository.

The embedded Hardlight packages retain the original game’s assembly identities.
Their implementations are recovered from the supplied bundle and maintained
locally rather than downloaded from a public package registry.
