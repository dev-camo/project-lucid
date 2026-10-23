"""Bounded render pipeline asset support; callers must qualify bindings first.

The URP pipeline assets, renderer data, renderer features, global settings and
the game's UI blur feature are standalone single-document ScriptableObjects
with nested blocks (renderer lists, shader and texture resources, stencil and
layer-mask structs, feature settings). Their top-level field names and order
must equal the shape observed in this release, and every nested line must lie
inside a block one of the profile's fields opens.

Two things change: the m_Script line, and, only in the profile's declared
shader slots, a reference to an exported compiled shader whose name is a
URP package shader. A compiled shader stays in quarantine, so such a slot
would otherwise point at nothing; the caller supplies the exact
exported-GUID-to-package-GUID map (see package_shader_map).
"""

import re

from .configurationbindings import COMMON, GUID, SCRIPT

URP = "Unity.RenderPipelines.Universal.Runtime"
PROFILES = {
    (URP, "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"): (
        "k_AssetVersion", "k_AssetPreviousVersion", "m_RendererType", "m_RendererData", "m_RendererDataList",
        "m_DefaultRendererIndex", "m_RequireDepthTexture", "m_RequireOpaqueTexture", "m_OpaqueDownsampling",
        "m_SupportsTerrainHoles", "m_SupportsHDR", "m_HDRColorBufferPrecision", "m_MSAA", "m_RenderScale",
        "m_UpscalingFilter", "m_FsrOverrideSharpness", "m_FsrSharpness", "m_EnableLODCrossFade",
        "m_LODCrossFadeDitheringType", "m_ShEvalMode", "m_MainLightRenderingMode", "m_MainLightShadowsSupported",
        "m_MainLightShadowmapResolution", "m_AdditionalLightsRenderingMode", "m_AdditionalLightsPerObjectLimit",
        "m_AdditionalLightShadowsSupported", "m_AdditionalLightsShadowmapResolution",
        "m_AdditionalLightsShadowResolutionTierLow", "m_AdditionalLightsShadowResolutionTierMedium",
        "m_AdditionalLightsShadowResolutionTierHigh", "m_ReflectionProbeBlending", "m_ReflectionProbeBoxProjection",
        "m_ShadowDistance", "m_ShadowCascadeCount", "m_Cascade2Split", "m_Cascade3Split", "m_Cascade4Split",
        "m_CascadeBorder", "m_ShadowDepthBias", "m_ShadowNormalBias", "m_SoftShadowsSupported",
        "m_ConservativeEnclosingSphere", "m_NumIterationsEnclosingSphere", "m_SoftShadowQuality",
        "m_AdditionalLightsCookieResolution", "m_AdditionalLightsCookieFormat", "m_UseSRPBatcher",
        "m_SupportsDynamicBatching", "m_MixedLightingSupported", "m_SupportsLightCookies", "m_SupportsLightLayers",
        "m_DebugLevel", "m_StoreActionsOptimization", "m_EnableRenderGraph", "m_UseAdaptivePerformance",
        "m_ColorGradingMode", "m_ColorGradingLutSize", "m_UseFastSRGBLinearConversion", "m_SupportDataDrivenLensFlare",
        "m_ShadowType", "m_LocalShadowsSupported", "m_LocalShadowsAtlasResolution", "m_MaxPixelLights",
        "m_ShadowAtlasResolution", "m_VolumeFrameworkUpdateMode", "m_Textures", "m_ShaderVariantLogLevel",
        "m_ShadowCascades"),
    (URP, "UnityEngine.Rendering.Universal.UniversalRendererData"): (
        "debugShaders", "m_RendererFeatures", "m_RendererFeatureMap", "m_UseNativeRenderPass", "postProcessData",
        "xrSystemData", "shaders", "m_AssetVersion", "m_OpaqueLayerMask", "m_TransparentLayerMask",
        "m_DefaultStencilState", "m_ShadowTransparentReceive", "m_RenderingMode", "m_DepthPrimingMode",
        "m_CopyDepthMode", "m_AccurateGbufferNormals", "m_IntermediateTextureMode"),
    (URP, "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings"): (
        "k_AssetVersion", "m_RenderingLayerNames", "m_ValidRenderingLayers", "lightLayerName0", "lightLayerName1",
        "lightLayerName2", "lightLayerName3", "lightLayerName4", "lightLayerName5", "lightLayerName6",
        "lightLayerName7", "m_StripDebugVariants", "m_StripUnusedPostProcessingVariants", "m_StripUnusedVariants",
        "m_StripUnusedLODCrossFadeVariants", "m_StripScreenCoordOverrideVariants", "supportRuntimeDebugDisplay",
        "m_ShaderVariantLogLevel", "m_ExportShaderVariants"),
    (URP, "FullScreenPassRendererFeature"): (
        "m_Active", "injectionPoint", "fetchColorBuffer", "requirements", "passMaterial", "passIndex",
        "bindDepthStencilAttachment", "m_Version"),
    (URP, "UnityEngine.Rendering.Universal.DecalRendererFeature"): ("m_Active", "m_Settings", "m_CopyDepthPS", "m_DBufferClear"),
    (URP, "UnityEngine.Rendering.Universal.PostProcessData"): ("shaders", "textures"),
    (URP, "UnityEngine.Rendering.Universal.XRSystemData"): ("shaders",),
    ("Game.Runtime", "HardlightProject.UIBlurRenderFeature"): ("m_Active", "settings"),
}
# The profile fields that open a nested block; no other field may.
NESTED = {
    (URP, "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"): ("m_RendererDataList", "m_Textures"),
    (URP, "UnityEngine.Rendering.Universal.UniversalRendererData"): (
        "debugShaders", "m_RendererFeatures", "shaders", "m_OpaqueLayerMask", "m_TransparentLayerMask",
        "m_DefaultStencilState"),
    (URP, "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings"): ("m_RenderingLayerNames",),
    (URP, "FullScreenPassRendererFeature"): (),
    (URP, "UnityEngine.Rendering.Universal.DecalRendererFeature"): ("m_Settings",),
    (URP, "UnityEngine.Rendering.Universal.PostProcessData"): ("shaders", "textures"),
    (URP, "UnityEngine.Rendering.Universal.XRSystemData"): ("shaders",),
    ("Game.Runtime", "HardlightProject.UIBlurRenderFeature"): ("settings",),
}
# Shader slots: the blocks whose member lines are Shader references, and the
# top-level Shader fields. Only these lines may name a shader, mapped or not.
SHADER_BLOCKS = {
    (URP, "UnityEngine.Rendering.Universal.UniversalRendererData"): ("debugShaders", "shaders"),
    (URP, "UnityEngine.Rendering.Universal.PostProcessData"): ("shaders",),
    (URP, "UnityEngine.Rendering.Universal.XRSystemData"): ("shaders",),
}
SHADER_FIELDS = {
    (URP, "UnityEngine.Rendering.Universal.DecalRendererFeature"): ("m_CopyDepthPS", "m_DBufferClear"),
}
HEADER = "--- !u!114 &11400000\nMonoBehaviour:\n"
PREFIX = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + HEADER
TOP = re.compile(r"  ([A-Za-z_][A-Za-z_0-9]*):(?: (.*))?")
SLOT = re.compile(r"(?P<head> {2,4}[A-Za-z_][A-Za-z_0-9]*: )\{fileID: (?P<id>-?[0-9]+)(?:, guid: (?P<guid>[0-9a-f]{32}), type: (?P<type>[0-9]+))?\}")
SHADER_FILE_ID = "4800000"


def _bindings(rows):
    result, owners, targets = {}, set(), set()
    for row in rows:
        owner = (row.get("assembly"), row.get("full_name")) if isinstance(row, dict) else None
        if owner not in PROFILES:
            raise ValueError("Unsupported render pipeline binding owner")
        for key in ("exported_guid", "target_guid"):
            if not isinstance(row.get(key), str) or GUID.fullmatch(row[key]) is None or row[key] == "0" * 32:
                raise ValueError("Invalid render pipeline script GUID")
        if type(row.get("exported_file_id")) is not int or row["exported_file_id"] != 11500000:
            raise ValueError("Unsupported exported render pipeline script file ID")
        if type(row.get("target_file_id")) is not int or not -(1 << 63) <= row["target_file_id"] < (1 << 63) or row["target_file_id"] == 0:
            raise ValueError("Invalid target render pipeline script file ID")
        target = (row["target_guid"], row["target_file_id"])
        if row["exported_guid"] in result or owner in owners or target in targets:
            raise ValueError("Duplicate render pipeline script binding")
        owners.add(owner)
        targets.add(target)
        result[row["exported_guid"]] = row
    return result


def _shader_map(shaders):
    if not isinstance(shaders, dict):
        raise ValueError("Render pipeline shader map is required")
    for exported, package in shaders.items():
        if (not isinstance(exported, str) or GUID.fullmatch(exported) is None or not isinstance(package, str) or
                GUID.fullmatch(package) is None or exported == package or "0" * 32 in (exported, package)):
            raise ValueError("Invalid render pipeline shader identity")
    return shaders


def rewrite_renderer_pointers(content, bindings, shaders, shader_log=None):
    """Validate one selected render pipeline asset; replace its script and shader slots.

    Pure apart from appending to ``shader_log``: unsupported shapes are rejected
    before any transformed content is returned. Returns (bytes, script changes).
    """
    if not isinstance(content, bytes):
        raise ValueError("Render pipeline content must be bytes")
    mapping, shaders = _bindings(bindings), _shader_map(shaders)
    if not mapping:
        return content, 0
    try:
        text = content.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError("Unsupported render pipeline UTF8") from error
    if not any(guid in text.lower() for guid in mapping):
        return content, 0
    if "\r" in text or any((ord(c) < 32 and c != "\n") or ord(c) == 127 for c in text):
        raise ValueError("Unsupported render pipeline control or newline")
    if not text.startswith(PREFIX) or text.count("--- !u!") != 1 or not text.endswith("\n"):
        raise ValueError("Unsupported render pipeline asset envelope")
    lines = text[len(PREFIX):].split("\n")[:-1]
    fields, values, block, owner, row = [], {}, None, None, None
    output, script_changes, slots = [], 0, []
    for line in lines:
        match = TOP.fullmatch(line)
        if match is not None:
            name, value = match[1], match[2]
            if name in values:
                raise ValueError("Duplicate render pipeline field")
            fields.append(name)
            values[name] = value if value is not None else ""
            if name == "m_Script":
                pointer = SCRIPT.fullmatch(values[name])
                if pointer is None or pointer[2] not in mapping or pointer[1] != "11500000":
                    raise ValueError("Unsupported selected render pipeline script identity")
                row = mapping[pointer[2]]
                owner = (row["assembly"], row["full_name"])
                target = "{fileID: " + str(row["target_file_id"]) + ", guid: " + row["target_guid"] + ", type: 3}"
                line = "  m_Script: " + target
                script_changes += 1
            block = name if value is None and owner is not None and name in NESTED[owner] else None
            if owner is not None and name in SHADER_FIELDS.get(owner, ()):
                slots.append(len(output))
        elif block is not None and (line.startswith("    ") or line.startswith("  - ")):
            if block in SHADER_BLOCKS.get(owner, ()):
                slots.append(len(output))
        else:
            raise ValueError("Unsupported render pipeline document line")
        output.append(line)
    if owner is None or tuple(fields) != COMMON + PROFILES[owner]:
        raise ValueError("Unsupported render pipeline field set or order")
    for name in ("m_ObjectHideFlags", "m_EditorHideFlags"):
        if values[name] != "0":
            raise ValueError("Unsupported render pipeline hide flags")
    for name in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject"):
        if values[name] != "{fileID: 0}":
            raise ValueError("Render pipeline asset is not the qualified standalone shape")
    if values["m_Enabled"] != "1" or values["m_EditorClassIdentifier"] or not values["m_Name"]:
        raise ValueError("Unsupported render pipeline enabled flag, name or class identifier")
    # A mapped exported shader may only be named by a shader slot.
    slot_lines = set(slots)
    if any(guid in line for i, line in enumerate(output) if i not in slot_lines for guid in shaders):
        raise ValueError("Mapped exported shader outside a render pipeline shader slot")
    for index in slots:
        slot = SLOT.fullmatch(output[index])
        if slot is None:
            raise ValueError("Unsupported render pipeline shader slot")
        if slot["guid"] is None:
            if slot["id"] != "0":
                raise ValueError("Unsupported local render pipeline shader reference")
            continue
        if slot["id"] != SHADER_FILE_ID or slot["type"] != "2":
            raise ValueError("Unsupported render pipeline shader reference identity")
        package = shaders.get(slot["guid"])
        if shader_log is not None:
            shader_log.append({"slot": slot["head"].strip().rstrip(":"), "exported_guid": slot["guid"], "package_guid": package})
        if package is not None:
            output[index] = slot["head"] + "{fileID: " + SHADER_FILE_ID + ", guid: " + package + ", type: 3}"
    return (PREFIX + "\n".join(output) + "\n").encode("utf-8"), script_changes


SHADER_DECLARATION = re.compile(r'^[ \t]*Shader[ \t]+"([^"\r\n]+)"', re.MULTILINE)
COMPILED_SHADER = re.compile(r"\A%YAML 1\.1\r?\n%TAG !u! tag:unity3d\.com,2011:\r?\n--- !u!48 &4800000\r?\nShader:\r?\n")
COMPILED_NAME = re.compile(r"^  m_Name: (.+?)\r?$", re.MULTILINE)
URP_PACKAGE = "com.unity.render-pipelines.universal"


def package_shader_map(repo_root, exported_project, asset_map):
    """Map each exported compiled shader to the URP package shader of the same name.

    Fail-closed: the package resolves only through the pinned URP identity
    (manifest, lock entry and descriptor); a name maps only when exactly one
    package .shader declares it; every exported shader must be a quarantined
    compiled Shader document whose .meta GUID equals its asset-map row.
    Returns the map and the exported shaders that have no package shader.
    """
    from pathlib import Path
    from .scriptbindings import GUID as META_GUID, _owned_file, _script_file

    root, export = Path(repo_root), Path(exported_project)
    descriptor = _script_file(root, "Packages/" + URP_PACKAGE + "/package.json", {"package": URP_PACKAGE})
    package = descriptor.parent
    declared = {}
    for path in sorted(package.rglob("*.shader")):
        relative = path.relative_to(package).as_posix()
        # Unity never imports hidden or "~" folders (Samples~, Documentation~).
        if any(part.startswith(".") or part.endswith("~") for part in relative.split("/")):
            continue
        names = SHADER_DECLARATION.findall(_owned_file(package, relative).read_text(encoding="utf-8-sig"))
        guids = META_GUID.findall(_owned_file(package, relative + ".meta").read_text(encoding="utf-8"))
        if len(names) != 1 or len(guids) != 1:
            raise ValueError("Package shader identity is incomplete: " + relative)
        declared.setdefault(names[0], []).append(guids[0])
    mapping, unresolved = {}, []
    for row in asset_map.get("assets", []):
        if row.get("shader_quarantined") is not True:
            continue
        asset = _owned_file(export, row["path"])
        head = asset.read_bytes()[:65536].decode("utf-8", errors="replace")
        if COMPILED_SHADER.match(head) is None:
            raise ValueError("Quarantined shader is not a compiled Shader document: " + row["path"])
        names = COMPILED_NAME.findall(head.split("\n  m_ParsedForm:", 1)[0])
        guids = META_GUID.findall(_owned_file(export, row["path"] + ".meta").read_text(encoding="utf-8"))
        if len(names) != 1 or guids != [row.get("guid")]:
            raise ValueError("Exported shader name or GUID is ambiguous: " + row["path"])
        candidates = declared.get(names[0], [])
        if len(candidates) == 1:
            mapping[row["guid"]] = candidates[0]
        else:
            unresolved.append({"guid": row["guid"], "path": row["path"], "name": names[0],
                               "package_candidates": len(candidates)})
    return {"map": mapping, "unresolved": sorted(unresolved, key=lambda r: r["path"]),
            "package_shader_count": sum(len(v) for v in declared.values())}


# The pipeline the committed ProjectSettings select, as the shipped
# globalgamemanagers did: one URP asset per quality level (GraphicsSettings'
# default is the Very Low asset) and everything those assets serialize.
PIPELINE_LEVELS = (
    ("0a2a20ccd0239d640be1afb208b7bd57", "Assets/MonoBehaviour/URPAsset_VeryLow.asset"),
    ("e5b1197ffc340184c95eb179e206f2d9", "Assets/MonoBehaviour/URPAsset_Low.asset"),
    ("f2f82e53c0d735e47b8304d7c9e91790", "Assets/MonoBehaviour/URPAsset_Med.asset"),
    ("63c9496ad82634f4b99000083f0e262a", "Assets/MonoBehaviour/URPAsset_High.asset"),
    ("01667cd63d5f60842b5a0be953702bf2", "Assets/MonoBehaviour/URPAsset_VeryHigh.asset"),
)
PIPELINE_CLOSURE = dict(PIPELINE_LEVELS, **{
    "4cb362d946357224d958c848e03c0b4c": "Assets/MonoBehaviour/UniversalRenderPipelineAsset_Renderer_Default.asset",
    "1afd470391a9ec74e95aad89c05d0fa3": "Assets/MonoBehaviour/UniversalRenderPipelineAsset_Renderer_Default_A8.asset",
    "09c8622c5928f704792d9f76f21c3ab7": "Assets/MonoBehaviour/UniversalRenderPipelineAsset_Renderer_UI.asset",
    "26aadb1406c93464283186b17b536cc9": "Assets/MonoBehaviour/FullScreenPassRendererFeature_0.asset",
    "56b9be8d157319041b599d26e3fc9e79": "Assets/MonoBehaviour/DecalRendererFeature.asset",
    "8db6e0dc25d550446aff8adf52b60878": "Assets/MonoBehaviour/FullScreenPassRendererFeature.asset",
    "d6c254a6af559064cb3f0ef5e7c4a856": "Assets/MonoBehaviour/UIBlurRenderFeature.asset",
    "89f0b91142e119d48a90ea78dacf243d": "Assets/MonoBehaviour/PostProcessData.asset",
    "6ff277d191e4245429aa8435040a5e0a": "Assets/MonoBehaviour/XRSystemData.asset",
})
# GraphicsSettings selects the committed global settings asset, not the shipped
# one: URP's UniversalRenderPipelineGlobalSettingsPostprocessor calls Ensure()
# after every domain reload, and before prepare binds the shipped asset Ensure
# finds no instance and re-points GraphicsSettings at a default asset, dirtying
# the tracked file (the Editor import ahead of prepare, and CI's compile, both
# open the project unprepared). The committed asset carries the shipped data.
SELECTED_SETTINGS = ("c47d17c57ee26482b9be927eac28d91e", "Assets/UniversalRenderPipelineGlobalSettings.asset")
SHIPPED_SETTINGS = ("0c710afde549a354ca370f9cef075d2b", "Assets/MonoBehaviour/UniversalRenderPipelineGlobalSettings.asset")
SETTINGS_OWNER = (URP, "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings")
SRP_SETTINGS = re.compile(r"^  m_SRPDefaultSettings:\r?\n    UnityEngine\.Rendering\.Universal\.UniversalRenderPipeline: "
                          r"\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}\r?\n(?!    )", re.MULTILINE)
COLOR_SPACE = re.compile(r"^  m_ActiveColorSpace: ([0-9]+)\r?$", re.MULTILINE)
LEVEL_PIPELINE = re.compile(r"^    customRenderPipeline: \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}\r?$", re.MULTILINE)
DEFAULT_PIPELINE = re.compile(r"^  m_CustomRenderPipeline: \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}\r?$", re.MULTILINE)


def _settings_fields(text):
    """The global settings fields from k_AssetVersion on, as (name, value) lines."""
    start = text.find("\n  k_AssetVersion: ")
    if start < 0:
        raise ValueError("Global settings asset lacks its versioned fields")
    return [line.rstrip() for line in text[start + 1:].splitlines()]


def check_global_settings(repo_root, exported_project, bindings):
    """The selected global settings must carry the shipped data and the bound script."""
    from pathlib import Path
    from .scriptbindings import GUID as META_GUID, _owned_file

    root = Path(repo_root)
    graphics = _owned_file(root, "ProjectSettings/GraphicsSettings.asset").read_text(encoding="utf-8")
    selected = _owned_file(root, SELECTED_SETTINGS[1]).read_text(encoding="utf-8")
    meta = _owned_file(root, SELECTED_SETTINGS[1] + ".meta").read_text(encoding="utf-8")
    shipped = _owned_file(Path(exported_project), SHIPPED_SETTINGS[1]).read_text(encoding="utf-8")
    if SRP_SETTINGS.findall(graphics) != [SELECTED_SETTINGS[0]] or META_GUID.findall(meta) != [SELECTED_SETTINGS[0]]:
        raise ValueError("GraphicsSettings does not select the committed URP global settings")
    rows = [row for row in bindings if (row.get("assembly"), row.get("full_name")) == SETTINGS_OWNER]
    script = re.findall(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}$", selected, re.MULTILINE)
    if len(rows) != 1 or script != [rows[0]["target_guid"]] or rows[0]["target_file_id"] != 11500000:
        raise ValueError("Committed URP global settings do not use the verified package script")
    if _settings_fields(selected) != _settings_fields(shipped):
        raise ValueError("Committed URP global settings differ from the shipped data")
    return {"selected_guid": SELECTED_SETTINGS[0], "shipped_guid": SHIPPED_SETTINGS[0], "fields_equal": True}


def check_pipeline_selection(repo_root, asset_map, bindings, exported_project):
    """Fail unless the selected pipeline and its closure are mapped and rebound.

    The committed QualitySettings and GraphicsSettings must name exactly the
    pinned URP assets; each closure asset must be the pinned export path and
    every script it uses must be bound, or the pipeline would load as
    missing-script objects and the game would read a null pipeline.
    """
    from pathlib import Path
    from .scriptbindings import _owned_file

    root = Path(repo_root)
    # The shipped project is Linear. URP's pipeline constructor sets
    # GraphicsSettings.lightsUseLinearIntensity from the active color space, so
    # a Gamma project rewrites the shipped m_LightsUseLinearIntensity: 1 on disk.
    player = _owned_file(root, "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
    if COLOR_SPACE.findall(player) != ["1"]:
        raise ValueError("ProjectSettings do not select the shipped Linear color space")
    settings = check_global_settings(repo_root, exported_project, bindings)
    quality = _owned_file(root, "ProjectSettings/QualitySettings.asset").read_text(encoding="utf-8")
    graphics = _owned_file(root, "ProjectSettings/GraphicsSettings.asset").read_text(encoding="utf-8")
    levels = tuple(LEVEL_PIPELINE.findall(quality))
    if levels != tuple(guid for guid, _ in PIPELINE_LEVELS) or DEFAULT_PIPELINE.findall(graphics) != [PIPELINE_LEVELS[0][0]]:
        raise ValueError("ProjectSettings do not select the pinned render pipeline assets")
    rows = {row.get("guid"): row for row in asset_map.get("assets", [])}
    bound = {row["exported_guid"] for row in bindings}
    for guid, path in sorted(PIPELINE_CLOSURE.items()):
        row = rows.get(guid)
        if row is None or row.get("path") != path:
            raise ValueError("Pinned render pipeline asset is missing from the asset map: " + path)
        scripts = row.get("script_references") or []
        if not scripts or any(script not in bound for script in scripts):
            raise ValueError("Pinned render pipeline asset is unbound: " + path)
    return {"status": "bound", "levels": [path for _, path in PIPELINE_LEVELS], "closure_count": len(PIPELINE_CLOSURE),
            "global_settings": settings}


PASS_MATERIAL = re.compile(r"^  passMaterial: \{fileID: 2100000, guid: ([0-9a-f]{32}), type: 2\}\r?$", re.MULTILINE)
MATERIAL_SHADER = re.compile(r"^  m_Shader: \{fileID: (-?[0-9]+), guid: ([0-9a-f]{32}), type: [0-9]+\}\r?$", re.MULTILINE)


def pass_material_shader_gaps(exported_project, asset_map, shader_map):
    """The selected full-screen passes' material shaders that no package shader replaces.

    These are the game's own shaders (HardLight), still quarantined as compiled
    shaders; the pass material then loads without its shader. Report only.
    """
    from pathlib import Path
    from .scriptbindings import _owned_file

    export = Path(exported_project)
    rows = {row.get("guid"): row for row in asset_map.get("assets", [])}
    gaps = []
    for guid, path in sorted(PIPELINE_CLOSURE.items()):
        text = _owned_file(export, path).read_text(encoding="utf-8")
        for material in PASS_MATERIAL.findall(text):
            row = rows.get(material)
            if row is None:
                gaps.append({"feature": path, "material_guid": material, "reason": "material_missing"})
                continue
            shaders = MATERIAL_SHADER.findall(_owned_file(export, row["path"]).read_text(encoding="utf-8"))
            for _, shader in shaders:
                target = rows.get(shader)
                if target is not None and target.get("shader_quarantined") is True and shader not in shader_map:
                    head = _owned_file(export, target["path"]).read_bytes()[:65536].decode("utf-8", errors="replace")
                    names = COMPILED_NAME.findall(head.split("\n  m_ParsedForm:", 1)[0])
                    gaps.append({"feature": path, "material": row["path"], "shader_guid": shader,
                                 "shader_name": names[0] if len(names) == 1 else None, "reason": "quarantined_game_shader"})
    return gaps
