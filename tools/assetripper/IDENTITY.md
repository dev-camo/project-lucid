# AssetRipper identity patch (issue #552)

`lucid-identity.patch` is applied to the manifest-verified AssetRipper 2.0.0 source before
it is built (`lucid.py bootstrap --tool assetripper-source`). Its sha256 is pinned as
`patch_sha256` in `tools/tool-lock.json` and is part of the build identity. The patch is
add-only: `apply_add_only_patch` in `tools/lucidlib/bootstrap.py` refuses any removal, fuzz,
rename or context mismatch, and the build step checks the patched files again after the
build. AssetRipper is GPL-3.0; the patch is a derivative of it and is covered by
`LICENSE.md` in this directory.

## What it records

After the export loop in `ProjectExporter.Export` it writes
`AuxiliaryFiles/export_identity.json` (`IdentityManifest.cs`, a new file in
`AssetRipper.Export.UnityProjects`). For every export collection whose `Export` returned
true (`not_exported` counts the others) it writes one entry:

| field | meaning |
| --- | --- |
| `order` | position in AssetRipper's export order |
| `type` | the collection's C# type name |
| `guid` | the collection's export GUID, or `null` when the collection has none (see below) |
| `paths` | the asset files whose `.meta` it wrote, relative to `ExportedProject`, sorted |
| `members` | `[serialized file, PathID, ClassID, local fileID, from a serialized file, written]` for every object of the collection, sorted |

- The serialized file is the name AssetRipper gives the collection (`cab-<32 hex>`,
  `globalgamemanagers`, ...), the same names as `path_id_map.json`.
- The local fileID is what `collection.GetExportID` returns, the value used in the YAML
  anchors and `{fileID: ...}` references.
- "From a serialized file" is false for objects AssetRipper generates (collection names such
  as `Generated Prefabs`); their PathIDs are not original.
- "Written" is true when the object is in the collection's `ExportableAssets`, that is,
  written as a YAML document. Generated wrapper objects (prefab class 1001, scene hierarchy
  class 1032, scriptable-object group class -1) are recorded but not written; their fileID
  is `null` for the last two.
- Collections without `.meta` files have empty `paths`: the 57 `SceneAssetExportCollection`,
  15 `ManagerExportCollection` and the `EditorBuildSettingsExportCollection`
  (`ProjectSettings`).
- `ScriptExportCollection` writes one deterministic GUID per script (an MD5 of assembly,
  namespace and class), not one per collection, so its `guid` is `null`. Scripts need no
  identity record.
- The file is large (about 580 MB for the 1.10.1 build: 29,359 collections, 6,956,412
  members, most of them prefab and scene objects) and is a per-export artifact, never
  committed.

## Why it is inert

It adds five calls (`NotePath` in `ExportCollection.ExportMeta`; `Reset`, `Begin`, `End` and
`Write` in `ProjectExporter.Export`) and one new class. Everything it calls is a read: `collection.Assets`, `collection.ExportableAssets`,
`collection.GetExportID`, `ExportCollection.GUID`, and each object's `Collection.Name`,
`PathID` and `ClassID`. Nothing it computes flows back into the export: no GUID, fileID,
path, order or exporter decision reads it. Exceptions from `GUID` and `GetExportID` are
caught inside the recorder and recorded as `null`. The collection state that enumeration
touches (`File`, set by the `Assets` and `ExportableAssets` iterators) is left as the export
itself leaves it, because `ExportableAssets` is enumerated last, as the exporter does.
The only new output is the one extra file, written after all collections.

The evidence is empirical and is recorded in the PR for the patch: two exports of the
1.10.1 input with the patched build were compared with the immutable baseline export, and
differed only in the random fields audited below plus `export_identity.json`, and the
identity file matched the exported files (`tools/lucidlib/exportidentity.py`,
`tools/assetripper_identity_compare.py`).

## Audit of random and clock call sites (tag 2.0.0, commit 1ac666f4)

Searched in the pinned tree for `NewGuid`, `Random`, `RandomNumberGenerator`,
`DateTime.Now/UtcNow`, `Stopwatch` and `Environment.TickCount`, excluding tests and the
assembly dumper. The patch calls none of them and adds no random or clock call.

| site | what it does | reaches the exported project? |
| --- | --- | --- |
| `AssetRipper.Export.UnityProjects/AssetExportCollection.cs:78` | `GUID { get; } = UnityGuid.NewGuid()` for every non-script, non-scene collection | yes: `.meta` `guid:` (29,285 files) and every `guid:` reference |
| `AssetRipper.Assets/Collections/SceneDefinition.cs:23` and `:39` | `GUID = guid.IsZero ? UnityGuid.NewGuid() : guid` for scenes | yes: the 57 scene GUIDs (the input has no `OcclusionCullingSettings.SceneGUID`, so all are random) |
| `AssetRipper.Processing/AudioMixers/GuidIndexTable.cs:27` (used from `AudioMixerProcessor.cs:87-160`) | a fresh GUID for each mixer group, volume, pitch, send and parameter index | yes: the 110 GUIDs inside `Main.mixer` |
| `AssetRipper.Processing/AudioMixers/AudioMixerProcessor.cs:179` | `effect.EffectID` of an appended Attenuation effect | yes, in `Main.mixer` |
| `AssetRipper.Export.UnityProjects/Meta.cs:75` | `timeCreated` from `DateTimeOffset.Now` unless `SOURCE_DATE_EPOCH` is set | yes: `timeCreated` of every `.meta` |
| `AssetRipper.SourceGenerated.Extensions/SpriteMetaDataExtensions.cs:67` | `SpriteID = Guid.NewGuid()` in `FillSpriteMetaData`, called from `TextureExportCollection.cs:148,158` | no value is emitted in this export: of the baseline's `.meta` files, 1,648 have a `spriteID:` key and none has a value, and the patched exports' `.meta` files differ from the baseline only in `guid:` and `timeCreated:` |
| `AssetRipper.Assets/Bundles/ProcessedBundle.cs:30` | random name of an unnamed `ProcessedBundle` | no: the only caller, `GameBundle.AddNewProcessedBundle`, is called with the name `Generated Hierarchy Assets` (`PrefabProcessor.cs:18`) |
| `AssetRipper.Export/Scripts/ILSpyWholeProjectDecompiler.cs:12` | `Guid.NewGuid()` as the decompiler's project GUID | no: it is passed to the decompiler base class and the exporter's project file writer is a `NullProjectFileWriter` (`ScriptDecompiler.cs:62`) |
| `AssetRipper.IO.Files/FileSystem.cs:27,41,183` and `LocalFileSystem.cs:22,24` | random names of temporary directories | no: temporary files are not part of the project |
| `AssetRipper.GUI.Web/Pages/Accordian.cs:7`, `HtmlTab.cs:11`, `Commands.cs:146`, `WebApplicationLauncher.cs:81` and `AssetRipper.Import/AssetRipperRuntimeInformation.cs:77` | HTML element ids, log and zip names, version text | no: not in the exported project |

Script GUIDs and fileIDs are deterministic (`Scripts/ScriptHashing.cs`), as are the
main-object fileIDs (`ExportIdHandler.cs`) and the prefab sub-object fileIDs (an `XxHash64`
of the insertion index, `AssetsExportCollection.cs`). Scene objects keep their original
PathIDs (`SceneExportCollection.cs:22-31`). The exports the patch was checked with match
the baseline on all of these.
