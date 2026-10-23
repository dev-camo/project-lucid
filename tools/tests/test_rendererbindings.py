"""Render pipeline bindings: rules, asset profiles, shader slots, the selected pipeline and the player-plane route."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import playerschema, rendererbindings, scriptbindings
from lucidlib.rendererbindings import (PIPELINE_CLOSURE, PIPELINE_LEVELS, check_pipeline_selection,
                                       package_shader_map, rewrite_renderer_pointers)
from lucidlib.scriptbindings import (RULES, URP_LOCK, URP_PACKAGE_RULES, RENDER_GAME_RULES, resolve_bindings,
                                     rewrite_prepared_script_pointers)
from lucidlib.verification import artifact_fingerprint
import test_scriptbindings

REPO = Path(__file__).resolve().parents[2]
URP = "Unity.RenderPipelines.Universal.Runtime"
RENDERER = (URP, "UnityEngine.Rendering.Universal.UniversalRendererData")
DECAL = (URP, "UnityEngine.Rendering.Universal.DecalRendererFeature")


def row(owner, exported, target):
    return {"assembly": owner[0], "full_name": owner[1], "exported_guid": exported, "exported_file_id": 11500000,
            "target_guid": target, "target_file_id": 11500000}


def asset(script, body, name="Asset"):
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: " + script + ", type: 3}\n  m_Name: " + name + "\n"
            "  m_EditorClassIdentifier:\n" + body).encode()


def shader(guid, kind=2, file_id=4800000):
    return "{fileID: %d, guid: %s, type: %d}" % (file_id, guid, kind)


def renderer_body(blit="1" * 32):
    return ("  debugShaders:\n    debugReplacementPS: " + shader("2" * 32) + "\n    hdrDebugViewPS: {fileID: 0}\n"
            "  m_RendererFeatures:\n  - {fileID: 11400000, guid: " + "3" * 32 + ", type: 2}\n"
            "  m_RendererFeatureMap: 1cf19d61d5bdc94a\n  m_UseNativeRenderPass: 1\n  postProcessData: {fileID: 0}\n"
            "  xrSystemData: {fileID: 0}\n  shaders:\n    blitPS: " + shader(blit) + "\n    screenSpaceShadowPS: {fileID: 0}\n"
            "  m_AssetVersion: 2\n  m_OpaqueLayerMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
            "  m_TransparentLayerMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
            "  m_DefaultStencilState:\n    overrideStencilState: 0\n  m_ShadowTransparentReceive: 1\n  m_RenderingMode: 0\n"
            "  m_DepthPrimingMode: 0\n  m_CopyDepthMode: 0\n  m_AccurateGbufferNormals: 0\n  m_IntermediateTextureMode: 1\n")


class RendererDocumentTests(unittest.TestCase):
    SCRIPT, TARGET = "a" * 32, "b" * 32
    SHADERS = {"1" * 32: "c" * 32, "2" * 32: "d" * 32}

    def rewrite(self, content, owner=RENDERER, shaders=None, log=None):
        return rewrite_renderer_pointers(content, [row(owner, self.SCRIPT, self.TARGET)],
                                         self.SHADERS if shaders is None else shaders, log)

    def test_script_and_mapped_shader_slots_change_and_nothing_else(self):
        content = asset(self.SCRIPT, renderer_body())
        log = []
        rewritten, count = self.rewrite(content, log=log)
        expected = content.replace(b"guid: " + self.SCRIPT.encode(), b"guid: " + self.TARGET.encode())
        expected = expected.replace(shader("1" * 32).encode(), shader("c" * 32, 3).encode()).replace(
            shader("2" * 32).encode(), shader("d" * 32, 3).encode())
        self.assertEqual((expected, 1), (rewritten, count))
        self.assertEqual([("debugReplacementPS", "2" * 32, "d" * 32), ("blitPS", "1" * 32, "c" * 32)],
                         [(e["slot"], e["exported_guid"], e["package_guid"]) for e in log])
        # Through the prepared router, with the same map.
        self.assertEqual((expected, 1), rewrite_prepared_script_pointers(content, [row(RENDERER, self.SCRIPT, self.TARGET)], self.SHADERS))

    def test_unmapped_shader_slots_stay_and_are_logged(self):
        content = asset(self.SCRIPT, renderer_body(blit="9" * 32))
        log = []
        rewritten, _ = self.rewrite(content, log=log)
        self.assertIn(shader("9" * 32).encode(), rewritten)
        self.assertIn(("blitPS", "9" * 32, None), [(e["slot"], e["exported_guid"], e["package_guid"]) for e in log])

    def test_decal_top_level_shader_fields_are_slots(self):
        body = ("  m_Active: 1\n  m_Settings:\n    technique: 2\n    dBufferSettings:\n      surfaceData: 2\n"
                "  m_CopyDepthPS: " + shader("1" * 32) + "\n  m_DBufferClear: " + shader("2" * 32) + "\n")
        rewritten, count = self.rewrite(asset(self.SCRIPT, body), owner=DECAL)
        self.assertIn(("  m_CopyDepthPS: " + shader("c" * 32, 3)).encode(), rewritten)
        self.assertIn(("  m_DBufferClear: " + shader("d" * 32, 3)).encode(), rewritten)

    def test_unsupported_shapes_are_refused(self):
        body = renderer_body()
        cases = {
            "roster": body.replace("  m_AssetVersion: 2\n", ""),
            "extra": body + "  m_Extra: 1\n",
            "duplicate": body + "  m_RenderingMode: 0\n",
            "nested-outside-block": body.replace("  m_RenderingMode: 0\n", "  m_RenderingMode: 0\n    nested: 1\n"),
            "scalar-opens-block": body.replace("  m_RenderingMode: 0\n", "  m_RenderingMode:\n    value: 0\n"),
            "shader-outside-slot": body.replace("  postProcessData: {fileID: 0}\n", "  postProcessData: " + shader("1" * 32) + "\n"),
            "slot-type-3": body.replace(shader("1" * 32), shader("1" * 32, 3)),
            "slot-file-id": body.replace(shader("1" * 32), shader("1" * 32, 2, 11400000)),
            "slot-local": body.replace("screenSpaceShadowPS: {fileID: 0}", "screenSpaceShadowPS: {fileID: 7}"),
            "slot-list": body.replace("    blitPS: ", "  - blitPS: "),
        }
        for name, changed in cases.items():
            with self.subTest(case=name), self.assertRaises(ValueError):
                self.rewrite(asset(self.SCRIPT, changed))
        content = asset(self.SCRIPT, body)
        for name, changed in {"crlf": content.replace(b"\n", b"\r\n"), "two-documents": content + b"--- !u!114 &2\nMonoBehaviour:\n",
                              "prefab": content.replace(b"m_GameObject: {fileID: 0}", b"m_GameObject: {fileID: 5}"),
                              "class-id": content.replace(b"m_EditorClassIdentifier:\n", b"m_EditorClassIdentifier: X\n"),
                              "script-file-id": content.replace(b"fileID: 11500000, guid: " + self.SCRIPT.encode(), b"fileID: 1, guid: " + self.SCRIPT.encode())}.items():
            with self.subTest(case=name), self.assertRaises(ValueError):
                self.rewrite(changed)
        with self.assertRaises(ValueError):
            self.rewrite(content, shaders={"1" * 32: "1" * 32})
        # Another owner's roster never fits a renderer document.
        with self.assertRaises(ValueError):
            self.rewrite(content, owner=DECAL)


class PackageShaderMapTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory).resolve() / "project"
        export = Path(directory).resolve() / "export"
        package = root / "Library/PackageCache/com.unity.render-pipelines.universal@14.0.11"
        (root / "Packages").mkdir(parents=True)
        (root / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.render-pipelines.universal": "14.0.11"}}))
        (root / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": {"com.unity.render-pipelines.universal": URP_LOCK}}))
        package.mkdir(parents=True)
        descriptor = b'{"name": "com.unity.render-pipelines.universal", "version": "14.0.11"}'
        (package / "package.json").write_bytes(descriptor)
        guard = patch("lucidlib.scriptbindings.URP_PACKAGE_SHA256", hashlib.sha256(descriptor).hexdigest())
        guard.start(); self.addCleanup(guard.stop)

        def package_shader(relative, name, guid):
            path = package / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('// comment\nShader "' + name + '"\n{\n}\n')
            path.with_name(path.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")
        package_shader("Shaders/Utils/Blit.shader", "Hidden/Universal Render Pipeline/Blit", "c" * 32)
        package_shader("Shaders/Twin/A.shader", "Hidden/Twin", "d" * 32)
        package_shader("Shaders/Twin/B.shader", "Hidden/Twin", "e" * 32)
        package_shader("Samples~/Hidden.shader", "Hidden/Sample", "f" * 32)
        rows = []

        def exported(name, guid):
            path = export / ("Assets/Shader/" + guid + ".asset")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!48 &4800000\nShader:\n  serializedVersion: 3\n"
                            "  m_Name: " + name + "\n  m_ParsedForm:\n    m_Name: Other\n")
            path.with_name(path.name + ".meta").write_text("guid: " + guid + "\n")
            rows.append({"path": "Assets/Shader/" + guid + ".asset", "guid": guid, "shader_quarantined": True})
        exported("Hidden/Universal Render Pipeline/Blit", "1" * 32)
        exported("Hidden/Universal Render Pipeline/Blit", "2" * 32)
        exported("Hidden/Twin", "3" * 32)
        exported("Hidden/Sample", "4" * 32)
        exported("HardLight/Game", "5" * 32)
        return root, export, {"assets": rows}, package

    def test_exact_unique_names_map_many_exports_to_one_package_shader(self):
        with tempfile.TemporaryDirectory() as directory:
            root, export, mapping, _ = self.fixture(directory)
            result = package_shader_map(root, export, mapping)
            self.assertEqual({"1" * 32: "c" * 32, "2" * 32: "c" * 32}, result["map"])
            self.assertEqual([("3" * 32, 2), ("4" * 32, 0), ("5" * 32, 0)],
                             [(u["guid"], u["package_candidates"]) for u in result["unresolved"]])

    def test_package_identity_and_exported_identity_are_checked(self):
        for change in ("lock", "descriptor", "meta", "not-compiled", "package-meta", "two-declarations"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                root, export, mapping, package = self.fixture(directory)
                if change == "lock":
                    (root / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": {
                        "com.unity.render-pipelines.universal": dict(URP_LOCK, source="embedded")}}))
                elif change == "descriptor": (package / "package.json").write_text("{}")
                elif change == "meta": (export / mapping["assets"][0]["path"]).with_suffix(".asset.meta").write_text("guid: " + "9" * 32 + "\n")
                elif change == "not-compiled": (export / mapping["assets"][0]["path"]).write_text("%YAML 1.1\n")
                elif change == "package-meta": (package / "Shaders/Utils/Blit.shader.meta").write_text("no guid\n")
                elif change == "two-declarations":
                    blit = package / "Shaders/Utils/Blit.shader"
                    blit.write_text(blit.read_text() + 'Shader "Hidden/Other"\n{\n}\n')
                with self.assertRaises(ValueError):
                    package_shader_map(root, export, mapping)


class PipelineSelectionTests(unittest.TestCase):
    def fixture(self, directory):
        root, export = Path(directory).resolve() / "project", Path(directory).resolve() / "export"
        (root / "ProjectSettings").mkdir(parents=True)
        for relative in ("ProjectSettings/QualitySettings.asset", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/ProjectSettings.asset",
                         "Assets/UniversalRenderPipelineGlobalSettings.asset", "Assets/UniversalRenderPipelineGlobalSettings.asset.meta"):
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((REPO / relative).read_bytes())
        shipped = export / rendererbindings.SHIPPED_SETTINGS[1]
        shipped.parent.mkdir(parents=True)
        committed = (REPO / "Assets/UniversalRenderPipelineGlobalSettings.asset").read_text()
        shipped.write_text(committed.replace("m_EditorClassIdentifier: \n", "m_EditorClassIdentifier:\n").replace(
            "guid: 2ec995e51a6e251468d2a3fd8a686257", "guid: 1ff2bf2226d999dd6f9ab1920a2db6a7"))
        scripts = {}
        rows = []
        for index, (guid, path) in enumerate(sorted(PIPELINE_CLOSURE.items())):
            script = "%032x" % (index + 1)
            scripts[guid] = script
            rows.append({"guid": guid, "path": path, "script_references": [script]})
        settings = row(rendererbindings.SETTINGS_OWNER, "1ff2bf2226d999dd6f9ab1920a2db6a7", "2ec995e51a6e251468d2a3fd8a686257")
        bound = [settings] + [{"exported_guid": s} for s in scripts.values()]
        return root, export, {"assets": rows}, bound

    def test_the_committed_settings_select_the_pinned_closure(self):
        with tempfile.TemporaryDirectory() as directory:
            root, export, mapping, bound = self.fixture(directory)
            result = check_pipeline_selection(root, mapping, bound, export)
            self.assertEqual("bound", result["status"]); self.assertEqual(14, result["closure_count"])
            self.assertEqual({"selected_guid": "c47d17c57ee26482b9be927eac28d91e", "shipped_guid": "0c710afde549a354ca370f9cef075d2b",
                              "fields_equal": True}, result["global_settings"])

    def test_wrong_selection_missing_or_unbound_closure_and_changed_settings_fail(self):
        for change in ("level-order", "default", "missing-row", "moved-row", "unbound", "no-scripts", "settings-data",
                       "settings-script", "settings-selected", "settings-meta", "no-settings-binding", "color-space"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                root, export, mapping, bound = self.fixture(directory)
                quality, graphics = root / "ProjectSettings/QualitySettings.asset", root / "ProjectSettings/GraphicsSettings.asset"
                selected = root / "Assets/UniversalRenderPipelineGlobalSettings.asset"
                if change == "level-order":
                    text = quality.read_text()
                    text = text.replace(PIPELINE_LEVELS[1][0], "TEMP").replace(PIPELINE_LEVELS[2][0], PIPELINE_LEVELS[1][0]).replace("TEMP", PIPELINE_LEVELS[2][0])
                    quality.write_text(text)
                elif change == "default": graphics.write_text(graphics.read_text().replace(PIPELINE_LEVELS[0][0], PIPELINE_LEVELS[1][0]))
                elif change == "missing-row": mapping["assets"].pop()
                elif change == "moved-row": mapping["assets"][0]["path"] = "Assets/Other.asset"
                elif change == "unbound": bound.pop()
                elif change == "no-scripts": mapping["assets"][0]["script_references"] = []
                elif change == "settings-data": selected.write_text(selected.read_text().replace("m_ValidRenderingLayers: 3", "m_ValidRenderingLayers: 1"))
                elif change == "settings-script": selected.write_text(selected.read_text().replace("2ec995e51a6e251468d2a3fd8a686257", "f" * 32))
                elif change == "settings-selected": graphics.write_text(graphics.read_text().replace("c47d17c57ee26482b9be927eac28d91e", "0c710afde549a354ca370f9cef075d2b"))
                elif change == "settings-meta": (root / "Assets/UniversalRenderPipelineGlobalSettings.asset.meta").write_text("guid: " + "e" * 32 + "\n")
                elif change == "no-settings-binding": bound.pop(0)
                elif change == "color-space":
                    player = root / "ProjectSettings/ProjectSettings.asset"
                    player.write_text(player.read_text().replace("  m_ActiveColorSpace: 1\n", "  m_ActiveColorSpace: 0\n"))
                with self.assertRaises(ValueError):
                    check_pipeline_selection(root, mapping, bound, export)


class PlayerPlaneResolverTests(unittest.TestCase):
    PREFIX = "Packages/com.unity.render-pipelines.universal/"

    def fixture(self, directory):
        root, work, export, mapping, comparison, path, summary = test_scriptbindings.ScriptBindingTests.fixture(self, directory)
        self.input_fingerprint = {"critical_files": {
            "Contents/Frameworks/GameAssembly.dylib": RULES[0]["binary_sha256"],
            "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": RULES[0]["metadata_sha256"]}}
        package = root / "Library/PackageCache/com.unity.render-pipelines.universal@14.0.11"
        (root / "Packages").mkdir(exist_ok=True)
        (root / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.render-pipelines.universal": "14.0.11"}}))
        (root / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": {"com.unity.render-pipelines.universal": URP_LOCK}}))
        hashes = {}
        for relative in URP_PACKAGE_RULES[0]["runtime_sources"]:
            target = package / relative[len(self.PREFIX):]
            target.parent.mkdir(parents=True, exist_ok=True)
            data = ("// fixture " + relative + "\n").encode()
            target.write_bytes(data)
            hashes[relative] = hashlib.sha256(data).hexdigest()
        descriptor = b'{"name": "com.unity.render-pipelines.universal"}'
        (package / "package.json").write_bytes(descriptor)
        for guard in (patch.dict("lucidlib.scriptbindings.URP_RUNTIME_SOURCES", hashes, clear=True),
                      patch("lucidlib.scriptbindings.URP_PACKAGE_SHA256", hashlib.sha256(descriptor).hexdigest())):
            guard.start(); self.addCleanup(guard.stop)
        self.verdicts = {}
        for index, rule in enumerate(URP_PACKAGE_RULES):
            guid, target_guid = "%032x" % (index + 1), "%032x" % (index + 101)
            original = export / rule["export_path"]
            original.parent.mkdir(parents=True, exist_ok=True)
            original.write_text("// export\n")
            original.with_name(original.name + ".meta").write_text("guid: " + guid + "\n")
            mapping["assets"].append({"path": rule["export_path"], "guid": guid, "code_quarantined": True})
            source = package / rule["maintained_path"][len(self.PREFIX):]
            source.with_name(source.name + ".meta").write_text("guid: " + target_guid + "\n")
            player = rule.get("layout_plane") == "player"
            comparison["candidates"].append({"assembly": rule["assembly"], "full_name": rule["full_name"],
                "status": "blocked" if player else "compatible_layout",
                "issues": [{"reason": "type_attributes_mismatch"}] if player else [], "original_token": "0x02000001",
                "loaded_scripts": [{"path": rule["maintained_path"], "guid": target_guid, "file_id": 11500000,
                    "class_resolved": True, "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                    "source_path": str(source), "package_name": "com.unity.render-pipelines.universal", "package_version": "14.0.11",
                    "package_builtin": True, "package_resolved_path": str(package)}]})
            if player:
                self.verdicts[(rule["assembly"], rule["full_name"])] = {"status": "compatible_layout", "issues": [], "notes": []}
        comparison["source_fingerprint"] = summary["source_fingerprint"] = artifact_fingerprint(root)
        path.write_text(json.dumps(comparison))
        return root, work, export, mapping, comparison, path, summary

    def plane(self):
        return {"verdicts": self.verdicts, "editor_only": {}, "report_path": "/x", "report_sha256": "0" * 64,
                "inventory_sha256": "0" * 64, "target": "macos"}

    def resolve(self, root, work, export, mapping, summary, plane=None):
        with patch("lucidlib.scriptbindings.run_layout_inventory", return_value=summary), \
                patch("lucidlib.scriptbindings._player_plane", return_value=plane or self.plane()) as run:
            return resolve_bindings(root, work, export, mapping, input_fingerprint=self.input_fingerprint), run

    def test_player_verdicts_decide_and_editor_candidates_identify(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = self.fixture(directory)
            result, run = self.resolve(root, work, export, mapping, summary)
            self.assertEqual(1, run.call_count)
            rows = [r for r in result["bindings"] if r.get("package") == "com.unity.render-pipelines.universal"]
            self.assertEqual(7, len(rows))
            self.assertEqual(["%032x" % (i + 101) for i in range(7)], [r["target_guid"] for r in rows])
            self.assertEqual(5, sum(1 for r in rows if r.get("layout_plane") == "player"))
            self.assertIn("player_plane", result)

    def test_a_blocked_or_absent_player_verdict_and_a_missing_editor_candidate_refuse(self):
        for change in ("blocked", "issues", "absent", "no-candidate", "editor-blocked", "unknown-plane"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                root, work, export, mapping, comparison, path, summary = self.fixture(directory)
                key = (URP, "FullScreenPassRendererFeature")
                plane = self.plane()
                if change == "blocked": plane["verdicts"][key] = dict(plane["verdicts"][key], status="blocked")
                elif change == "issues": plane["verdicts"][key] = dict(plane["verdicts"][key], issues=[{"reason": "x"}])
                elif change == "absent": del plane["verdicts"][key]
                elif change == "no-candidate":
                    comparison["candidates"] = [c for c in comparison["candidates"] if c["full_name"] != key[1]]
                    path.write_text(json.dumps(comparison))
                elif change == "editor-blocked":
                    # An Editor-plane rule never takes the player verdict.
                    for candidate in comparison["candidates"]:
                        if candidate["full_name"].endswith("PostProcessData"):
                            candidate.update(status="blocked", issues=[{"reason": "x"}])
                    path.write_text(json.dumps(comparison))
                with self.assertRaises(ValueError):
                    if change == "unknown-plane":
                        rules = tuple(dict(r, layout_plane="bytes") if r["full_name"] == key[1] else r for r in RULES)
                        with patch("lucidlib.scriptbindings.RULES", rules):
                            self.resolve(root, work, export, mapping, summary, plane)
                    else:
                        self.resolve(root, work, export, mapping, summary, plane)

    def test_no_player_rule_means_no_player_compile(self):
        with tempfile.TemporaryDirectory() as directory:
            root, work, export, mapping, comparison, path, summary = test_scriptbindings.ScriptBindingTests.fixture(self, directory)
            self.verdicts = {}
            self.input_fingerprint = {"critical_files": {
                "Contents/Frameworks/GameAssembly.dylib": RULES[0]["binary_sha256"],
                "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat": RULES[0]["metadata_sha256"]}}
            result, run = self.resolve(root, work, export, mapping, summary)
            self.assertEqual(0, run.call_count)
            self.assertNotIn("player_plane", result)


class RenderRuleRosterTests(unittest.TestCase):
    def test_rules_bind_the_package_scripts_and_the_two_game_features(self):
        # The player roots are exactly the rules whose verdict comes from the player plane.
        self.assertEqual(playerschema.URP_ROOTS, {(r["assembly"], r["full_name"]) for r in RULES if r.get("layout_plane") == "player"})
        self.assertEqual(["UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset", "UnityEngine.Rendering.Universal.UniversalRendererData",
                          "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings", "FullScreenPassRendererFeature",
                          "UnityEngine.Rendering.Universal.DecalRendererFeature", "UnityEngine.Rendering.Universal.PostProcessData",
                          "UnityEngine.Rendering.Universal.XRSystemData"], [r["full_name"] for r in URP_PACKAGE_RULES])
        for rule in URP_PACKAGE_RULES:
            self.assertEqual("com.unity.render-pipelines.universal", rule["package"])
            self.assertTrue(rule["maintained_path"].startswith("Packages/com.unity.render-pipelines.universal/Runtime/"))
            self.assertIn(rule["maintained_path"], rule["runtime_sources"])
        # The full-screen pass binds to the package script itself (exact GetType lookup).
        self.assertEqual("Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs",
                         URP_PACKAGE_RULES[3]["maintained_path"])
        self.assertEqual([("Game.Runtime", "HardlightProject.UIBlurRenderFeature"), ("HLUnityCore.Runtime", "Hardlight.ScalableFeature")],
                         [(r["assembly"], r["full_name"]) for r in RENDER_GAME_RULES])
        self.assertEqual(("14.0.11", URP_LOCK, scriptbindings.URP_PACKAGE_SHA256, True),
                         scriptbindings._package_identity("com.unity.render-pipelines.universal"))
        with self.assertRaises(ValueError):
            scriptbindings._package_identity("com.unity.render-pipelines.core")
        self.assertEqual(set(rendererbindings.PROFILES), {(r["assembly"], r["full_name"]) for r in URP_PACKAGE_RULES} |
                         {("Game.Runtime", "HardlightProject.UIBlurRenderFeature")})


if __name__ == "__main__":
    unittest.main()
