# The committed identity table (issue #552)

`identity-table.txt` maps every exported collection of the 1.10.1 build (input `6c89-acb`), keyed by the
original object it is anchored on (serialized file, PathID, ClassID), to the baseline project's GUID,
paths, `timeCreated` and file hashes. The format is documented in `tools/lucidlib/identitymap.py`; the
relabel step that uses it is `identitymap.build_baseline_project`. It is plain text, one object per line,
tab separated, sorted, so that it can be regenerated and diffed. It discloses original asset paths and
nothing else.

It is made once by the maintainer, from the immutable baseline export (read only) and a patched witness
export, and committed. A different input, AssetRipper commit or patch changes the header and is refused.

| header field | value | where it comes from |
| --- | --- | --- |
| `input` | `15268415fc3309c1aa8dafd72ec8e7559c117749043d5a38faa226a9dea4b6d5` | sha256 of the `input-manifest.json` that `extract-assets` writes for this input (the baseline run's file) |
| `assetripper` | `1ac666f47d8e9dedf96afb0b914c70d7656151ea` | `tools/tool-lock.json`, `assetripper-source` |
| `patch` | `18f417cf86131bafb64982441f6709ccb2cbbeddd7c53a5cd2241b4e8c6a50f8` | sha256 of `lucid-identity.patch` |

## Regenerating it (maintainer)

```
python3 tools/lucid.py bootstrap --tool assetripper-source
python3 tools/assetripper_identity_export.py --ripper <patched AssetRipper.GUI.Free> \
    --app input/SonicDreamTeam.app --out <new dir>
python3 tools/assetripper_identity_table.py build --witness <new dir>/project \
    --baseline <baseline run dir> --app input/SonicDreamTeam.app --out <table> \
    --expect tools/assetripper/identity-table.txt
```

`build` refuses unless (1) the input's manifest hash equals the pinned one, (2) the witness equals the
baseline apart from the known random fields (`assetripper_identity_compare.py compare`: the GUIDs, `.meta`
`timeCreated`, the 110 `Main.mixer` values and the 16-byte MVID of 95 game assemblies; no other
difference), (3) the identity record agrees with the exported files (`identitymap.validate_identity`),
and (4) every collection has exactly one anchor under its rule. `--expect` makes it the regeneration gate:
exit status 1 unless the bytes equal the committed file. Round trip on a witness:

```
python3 tools/assetripper_identity_table.py roundtrip --witness <new dir>/project \
    --baseline <baseline run dir> --table tools/assetripper/identity-table.txt --stage <new, empty dir>
```

That runs `identitymap.build_baseline_project` and then compares every output file with the baseline
file of the same path (not only with the table's hashes). `check` parses the table and its header.

Two independent witness exports of the input made the same table, byte for byte (sha256
`57029c814290dd35f777ce5111ceb53f30f1a94bf41fea546fcc13fef450476e`). The roundtrip on the second witness
rewrote 29,285 `.meta` GUIDs, 33,062 `timeCreated` values, 1,877,440 references, 1 build-settings scene entry
and 619 mixer tokens, and all 71,187 files of `ExportedProject` then equalled the baseline's by sha256 (about
110 minutes on an Apple Silicon Mac, mostly in Python regular expressions over the 20 GB project).

## What is in it (1.10.1)

Lines: 39,286 (header plus 39,285 rows), 12,819,590 bytes.

| row | count | meaning |
| --- | ---: | --- |
| `C` | 29,285 | exported collections that own files (not scripts) |
| `S` | 3,777 | scripts: verified against the export, never remapped |
| `F` | 5,063 | files no collection owns (ProjectSettings and the like), hash only |
| `M` | 619 | baseline token at each bare-token position of `Main.mixer` |
| `U` | 2 | baseline reference GUIDs that resolve to nothing |
| `W` | 539 | classes of content-identical collections (1,249 collections), order-witnessed |

### Collection types and their anchor rules

The 29,359 collections of the export, by AssetRipper type name, with the rule `identitymap.ANCHOR_RULES`
applies (written serialized member = a member read from a serialized file and written as a file):

| type | collections | rule |
| --- | ---: | --- |
| ``AssetExportCollection`1`` | 7,848 | `asset`: the only written member; if there is none, see below |
| `ScriptableObjectExportCollection` | 8,170 | `single` |
| `YamlStreamedAssetExportCollection` | 5,912 | `single` |
| `PrefabExportCollection` | 3,588 | `prefab_root`: the GameObject whose transform has no father |
| `TextureExportCollection` | 1,645 | `classes` 28 or 89 (Texture2D or Cubemap) |
| `AudioClipExportCollection` | 1,589 | `single` |
| `YamlShaderExportCollection` | 210 | `single` |
| `ScriptableObjectGroupExportCollection` | 154 | `first`: lowest PathID of the one serialized file |
| `AnimatorControllerExportCollection` | 92 | `class` 91 |
| `SceneExportCollection` | 57 | `first` |
| `SceneAssetExportCollection` | 57 | `none`: no file, no GUID |
| `ManagerExportCollection` | 15 | `unowned`: no file; a random GUID that is never written |
| `VideoClipExportCollection` | 9 | `single` |
| `FontAssetExportCollection` | 4 | `class` 128 (Font) |
| `TextureArrayAssetExportCollection` | 3 | `single` |
| `TextAssetExportCollection` | 2 | `single` |
| `AudioMixerExportCollection` | 1 | `class` 241 |
| `EditorBuildSettingsExportCollection` | 1 | `unowned` |
| `TerrainYamlExportCollection` | 1 | `single` |
| `ScriptExportCollection` | 1 | `script`: 3,777 files, one deterministic GUID each |

Findings that changed the rules the relabel step started with (all had tests added):

- The real type names differ from the first guesses (``AssetExportCollection`1`` is generic, there is no
  `MaterialExportCollection`). Every real type now has a rule; a type without one is still refused.
- `TextureExportCollection` includes 147 cubemaps (class 89) and `FontAssetExportCollection` writes a
  material, a texture and a font, so the old rules `class 28` and `single` could not select one member.
- `ManagerExportCollection` (15) and `EditorBuildSettingsExportCollection` (1) own no file but do have a
  random GUID, so the old `none` rule (no GUID) refused them. The new `unowned` rule accepts the GUID and
  records it so that it can never appear in the output.
- 57 ``AssetExportCollection`1`` collections hold one generated `LightingDataAsset` (class 1120) and no
  original member: the path is `<scene folder>/LightingData.asset` and the object lives in AssetRipper's
  `Generated Lighting Data Assets`, with a generation-order PathID. They are anchored on the scene their
  `m_Scene` names: the scene's anchor file and PathID with class 1120. This rule is more than the
  owner's recorded decision ("reject collections that have no original member") allows, because without
  it no input can finish; it is limited to a lone generated class 1120 member whose `m_Scene` names
  exactly one exported scene, and it needs the owner's approval before the extraction step relies on it.

### Main.mixer

`Main.mixer` has 619 bare 32-hex tokens (positions) with 156 distinct baseline values. In both witness
exports the tokens map by position to the baseline's: 156 distinct witness values, the value mapping is a
function and injective (`identitymap._mixer_edits` refuses otherwise). 115 of the 619 positions (46 of the
156 values) are the same in the witness and the baseline (AssetRipper does not regenerate them); the other
values are fresh in every export and are rewritten by position. Comparing the two witnesses directly, the
same 115 positions and 46 values are equal, and the W1 to W2 mapping is also a function.

### Order-witnessed classes (`W` rows)

Colour refinement over the baseline's collections, with no path, name, PathID or serialized-file
information: the start colour is the file content with every GUID position masked (and the `.meta`
without `guid:` and `timeCreated:`), and each round adds the colours of the collections referenced (in file
order) and of the referrers. 539 classes (1,249 collections) stay in one class after 5 rounds: sizes 2
(518 classes), 3 (4), 4 (6), 5 (1), 6 (2), 8 (2), 17 (1), 18 (2), 23 (1), 26 (1), 42 (1). By the directory
of the first member: 483 of the size-2 classes are sprites below `Assets/Sprite` (the same sprite in the HD
and the SD atlas bundle, as the design expected); the larger classes are `PolybrushMesh-*` meshes and
`Custom_Mesh_-_BakedMat_*` materials, and the rest are a few ScriptableObjects, audio clips, two pairs of scenes, a
cubemap and a prefab. 520 of the 539 classes have members that share a path once a collision suffix is
removed. For these the baseline GUID given to each collection rests on AssetRipper's export order, which
two witness exports reproduced; the bytes of the output cannot differ either way. They are listed as `W`
rows, reported by `build_baseline_project` as `order_witnessed`, and are never used as provenance evidence.

### Unresolved references (`U` rows)

Two baseline `{fileID, guid, type}` references name no exported asset:
`0000000000000000e000000000000000` and `0000000000000000f000000000000000` (Unity's built-in resources).
The asset map's `unresolved_references` (2,370) is a looser count: it also counts 2,368 `m_guid:` fields
of game data that are not references; they are bare tokens outside any reference position, are never
rewritten, and a fresh GUID equal to one is refused.

## The static check that does not use AssetRipper

`tools/assetripper_identity_static.py` reads the input directly (`tools/lucidlib/unityfs.py`: UnityFS
block and node tables, and the SerializedFile header, type table and object table; LZ4 blocks are decoded
with `shaders.decode_lz4_block`) and confirms that each identity-record member that came from a serialized
file is in that file's object table, with the same ClassID.

- `scenes`: the 57 scene collections, 2,522,193 members, 57 serialized files (56 bundle nodes and
  `level0`).
- `all`: all 29,359 collections, 6,946,257 members, 3,551 serialized files (3,546 bundle nodes and 5 plain
  files). Members whose `from a serialized file` flag is false are generated and are not checked.

No failure in either. It is a check of the exporter's record against the input, not a reimplementation of
the export: it does not derive paths, GUIDs or fileIDs.
