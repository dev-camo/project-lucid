# Dependency notices

Project Lucid uses Unity 2022.3.54f1 and packages distributed through Unity's
Package Manager. Package versions are recorded in `Packages/manifest.json` and
`Packages/packages-lock.json`; each package retains its upstream notices.

Extraction and analysis use separate tools downloaded into the local cache:

| Tool | Version | Source and license |
| --- | --- | --- |
| AssetRipper | 2.0.0 | [AssetRipper](https://github.com/AssetRipper/AssetRipper), GPL-3.0 |
| AssetRipper source build | 1ac666f47d8e9dedf96afb0b914c70d7656151ea | [AssetRipper](https://github.com/AssetRipper/AssetRipper) tag 2.0.0, GPL-3.0 |
| Cpp2IL | b5ad444b82267cb1e4b88b8b373c008105bdea52 | [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL), MIT |
| Il2CppDumper | 6.7.46 | [Il2CppDumper](https://github.com/Perfare/Il2CppDumper), MIT |
| .NET SDK and runtime | 10.0.403 (runtime 10.0.14) | [Microsoft .NET](https://dotnet.microsoft.com/), distribution notices; also runs Il2CppDumper |

Download locations and SHA-256 or SHA-512 hashes are recorded in `tools/tool-lock.json`.
Downloaded distributions retain their own licenses. Game assets are supplied
by the user and are not distributed in this repository.

`lucid.py bootstrap --tool assetripper-source` builds AssetRipper 2.0.0 from the pinned
source commit (without native AOT) beside the prebuilt AssetRipper download, which it
does not replace. The source is checked file by file against
`tools/assetripper/source-manifest.txt`, restored in locked mode from the
`packages.lock.json` files under `tools/assetripper/locks/`, and fetched only from
nuget.org (`tools/assetripper/nuget.config`). AssetRipper is GPL-3.0; its license text is
`tools/assetripper/LICENSE.md`, and the build output carries it as `LICENSE.txt`.
The verified source is then patched with the add-only `tools/assetripper/lucid-identity.patch`
(pinned as `patch_sha256` in `tools/tool-lock.json`), which only records which original
objects each exported file holds, in `AuxiliaryFiles/export_identity.json`; see
`tools/assetripper/IDENTITY.md`.

The embedded Hardlight packages retain the original game’s assembly identities.
Their implementations are recovered from the supplied bundle and maintained
locally rather than downloaded from a public package registry.
