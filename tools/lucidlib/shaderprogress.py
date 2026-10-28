"""Shader port progress by shipped GUID, kept apart from the method table.

Three distinct claims are reported and never inferred from each other: maintained
source presence (a shader whose .meta GUID is an original game shader's GUID),
compilation for one named target at one exact source revision (only from sealed
evidence), and fidelity (never measured here). Presence is not visual parity.
The original inventory is derived from the immutable asset map; third-party and
engine shaders are classified but never counted as game shaders.
"""
from __future__ import annotations

from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re

from . import playercode

INVENTORY_PATH = "tools/code-progress/shader-inventory.json"
SHADER_ROOT = "Assets/Shaders"
GUID = re.compile(r"[0-9a-f]{32}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
CLASSES = ("game_folder", "game_outside_folder", "third_party", "engine")
FOLDER_PREFIX = "Assets/Data/Art/Shaders/Shaders/"
# The folder's categories and their original shader counts (59 in total).
FOLDER_CATEGORIES = {"Character": 9, "Environment": 20, "Gameplay": 11, "Hub": 2, "Particle": 11,
                     "RenderFeatures": 1, "Sky": 3, "UI": 2}
# Game-authored shaders the export places beside engine shaders carry the studio's shader-name prefix.
OUTSIDE_PREFIXES = ("HardLight_", "Hidden_HardLight_")
THIRD_PARTY_RESOURCES = {
    "base", "blend", "blendcache", "depthmask", "depthmaskblend", "mask", "maskblend", "processonly", "rendermask",  # AmplifyColor
    "softmask", "softmasketc1", "softmasketc1premultipliedalpha", "softmaskpremultipliedalpha"}  # SoftMask
ENGINE_DIRECTORIES = ("Assets/Packages/", "Assets/TextMesh Pro/")
ENGINE_SHADER_PREFIXES = ("Hidden_Core_", "Hidden_ProBuilder_", "Hidden_Shader Graph_", "Hidden_TerrainEngine_",
                          "Hidden_Universal", "ProBuilder_", "TextMeshPro_", "Universal Render Pipeline_")
COMPILATION_STATUSES = {"not_measured", "sealed"}


def classify(path):
    """Return (class, category) for one quarantined shader's exported path; unknown paths are errors."""
    if not isinstance(path, str) or not path.startswith("Assets/") or ".." in path.split("/"):
        raise ValueError("Invalid shader path")
    name = path.rsplit("/", 1)[-1].rsplit(".", 1)[0]
    if path.startswith(FOLDER_PREFIX):
        category = path[len(FOLDER_PREFIX):].split("/", 1)[0]
        if category not in FOLDER_CATEGORIES or "/" not in path[len(FOLDER_PREFIX):]:
            raise ValueError("Unknown game shader category")
        return "game_folder", category
    parent = path.rsplit("/", 1)[0]
    if parent == "Assets/Resources" and name in THIRD_PARTY_RESOURCES:
        return "third_party", None
    if parent == "Assets/Shader" and name.startswith(OUTSIDE_PREFIXES):
        return "game_outside_folder", None
    if path.startswith(ENGINE_DIRECTORIES) or (parent == "Assets/Shader" and name.startswith(ENGINE_SHADER_PREFIXES)):
        return "engine", None
    raise ValueError("Unclassified quarantined shader")


def build_inventory(asset_map, origin):
    """Derive the original shader inventory from an asset map; every quarantined shader is classified."""
    if not isinstance(asset_map, dict) or not isinstance(asset_map.get("assets"), list):
        raise ValueError("Invalid asset map")
    if (not isinstance(origin, dict) or set(origin) != {"input_fingerprint_sha256", "asset_map_catalog_sha256"} or
            any(not isinstance(v, str) or HASH.fullmatch(v) is None for v in origin.values())):
        raise ValueError("Invalid inventory origin")
    rows = []
    for asset in asset_map["assets"]:
        if not asset.get("shader_quarantined"): continue
        kind, category = classify(asset.get("path"))
        rows.append({"guid": asset.get("guid"), "name": asset["path"].rsplit("/", 1)[-1].rsplit(".", 1)[0], "class": kind, "category": category})
    rows.sort(key=lambda row: row["guid"] if isinstance(row["guid"], str) else "")
    inventory = {"schema_version": 1, "origin": dict(origin), "shaders": rows}
    return validate_inventory(inventory)


def validate_inventory(inventory):
    if (not isinstance(inventory, dict) or set(inventory) != {"schema_version", "origin", "shaders"} or inventory["schema_version"] != 1 or
            not isinstance(inventory["origin"], dict) or set(inventory["origin"]) != {"input_fingerprint_sha256", "asset_map_catalog_sha256"} or
            any(not isinstance(v, str) or HASH.fullmatch(v) is None for v in inventory["origin"].values()) or
            not isinstance(inventory["shaders"], list) or not inventory["shaders"]):
        raise ValueError("Invalid original shader inventory")
    guids = []
    for row in inventory["shaders"]:
        if (not isinstance(row, dict) or set(row) != {"guid", "name", "class", "category"} or not isinstance(row["guid"], str) or
                GUID.fullmatch(row["guid"]) is None or not isinstance(row["name"], str) or not row["name"] or row["class"] not in CLASSES):
            raise ValueError("Invalid original shader entry")
        if (row["class"] == "game_folder") != (row["category"] in FOLDER_CATEGORIES) or (row["class"] != "game_folder" and row["category"] is not None):
            raise ValueError("Shader category does not belong to its class")
        guids.append(row["guid"])
    if len(set(guids)) != len(guids): raise ValueError("Duplicate original shader GUID")
    if guids != sorted(guids): raise ValueError("Original shader inventory is not in canonical order")
    folder = Counter(row["category"] for row in inventory["shaders"] if row["class"] == "game_folder")
    if dict(folder) != FOLDER_CATEGORIES: raise ValueError("Original game shader folder counts differ")
    return inventory


def load_inventory(root):
    path = playercode._regular(Path(root) / INVENTORY_PATH, 1024 * 1024)
    raw = path.read_bytes()
    value = playercode._strict_json(raw)
    return validate_inventory(value), hashlib.sha256(raw).hexdigest()


def maintained_shader_guids(root):
    """GUIDs of the maintained shaders that claim to be original game shaders (the shader tree only)."""
    base = Path(root) / SHADER_ROOT
    if base.is_symlink(): raise ValueError("Symlinked maintained shader directory")
    guids = []
    for parent, directories, files in os.walk(base):
        directories[:] = sorted(directories)
        if any((Path(parent) / name).is_symlink() for name in directories): raise ValueError("Symlinked maintained shader directory")
        for name in sorted(files):
            if not name.endswith(".shader"): continue
            if (Path(parent) / name).is_symlink(): raise ValueError("Symlinked maintained shader")
            meta = Path(parent) / (name + ".meta")
            if not meta.is_file() or meta.is_symlink(): raise ValueError("Maintained shader has no regular .meta file")
            matches = re.findall(r"^guid: ([0-9a-f]{32})$", playercode._regular(meta, 65536).read_text(), re.MULTILINE)
            if len(matches) != 1: raise ValueError("Maintained shader has no unique GUID")
            guids.append(matches[0])
    return guids


def measure(inventory, maintained_guids, source_fingerprint=None, compilation=None):
    """Presence by shipped GUID, with compilation only from sealed evidence for this exact source."""
    validate_inventory(inventory)
    if not isinstance(maintained_guids, list) or any(not isinstance(g, str) for g in maintained_guids):
        raise ValueError("Invalid maintained shader GUIDs")
    if len(set(maintained_guids)) != len(maintained_guids): raise ValueError("Duplicate maintained shader GUID")
    by_guid = {row["guid"]: row for row in inventory["shaders"]}
    if any(g not in by_guid or by_guid[g]["class"] not in ("game_folder", "game_outside_folder") for g in maintained_guids):
        raise ValueError("Maintained shader GUID is not an original game shader")
    present = set(maintained_guids)
    categories = {name: {"original": count, "source_present": sum(1 for g in present if by_guid[g]["category"] == name)}
                  for name, count in sorted(FOLDER_CATEGORIES.items())}
    folder = {"original": sum(FOLDER_CATEGORIES.values()), "source_present": sum(v["source_present"] for v in categories.values()),
              "categories": categories}
    outside_total = sum(1 for row in inventory["shaders"] if row["class"] == "game_outside_folder")
    outside = {"original": outside_total, "source_present": sum(1 for g in present if by_guid[g]["class"] == "game_outside_folder")}
    result = {"inventory_sha256": hashlib.sha256((json.dumps(inventory, sort_keys=True, separators=(",", ":")) + "\n").encode()).hexdigest(),
              "game_authored": {"original": folder["original"] + outside_total, "source_present": len(present),
                                "folder": folder, "outside_folder": outside},
              "not_game_scope": {"third_party": sum(1 for r in inventory["shaders"] if r["class"] == "third_party"),
                                 "engine": sum(1 for r in inventory["shaders"] if r["class"] == "engine")},
              "compilation": {"status": "not_measured"}, "fidelity": {"status": "not_measured"}}
    if compilation is not None:
        result["compilation"] = _compilation(compilation, present, source_fingerprint)
    return result


def _compilation(evidence, present, source_fingerprint):
    """Sealed compile evidence: one target, this exact source, present shaders only, no fallback or error shader."""
    if (not isinstance(evidence, dict) or set(evidence) != {"target", "source_fingerprint", "compiled_guids", "fallback_or_error_guids"} or
            evidence["target"] not in playercode.TARGETS or not isinstance(source_fingerprint, str) or HASH.fullmatch(source_fingerprint) is None or
            evidence["source_fingerprint"] != source_fingerprint):
        raise ValueError("Shader compilation evidence is unsupported or stale for this source")
    compiled, failed = evidence["compiled_guids"], evidence["fallback_or_error_guids"]
    if (not isinstance(compiled, list) or not isinstance(failed, list) or compiled != sorted(set(compiled)) or failed != sorted(set(failed)) or
            any(not isinstance(g, str) for g in compiled + failed)):
        raise ValueError("Invalid shader compilation GUID lists")
    if failed or not set(compiled) <= present:
        raise ValueError("A compiled shader needs maintained source and no fallback or error shader")
    return {"status": "sealed", "target": evidence["target"], "source_fingerprint": source_fingerprint, "compiled": len(compiled)}


def validate_public(shaders):
    """Shape and arithmetic of the public section; the inventory identity is checked against the repository elsewhere."""
    keys = {"inventory_sha256", "game_authored", "not_game_scope", "compilation", "fidelity"}
    if not isinstance(shaders, dict) or set(shaders) != keys or not isinstance(shaders["inventory_sha256"], str) or HASH.fullmatch(shaders["inventory_sha256"]) is None:
        raise ValueError("Unsupported public shader section")
    def count(value, upper=100000):
        if type(value) is not int or not 0 <= value <= upper: raise ValueError("Invalid public shader count")
        return value
    game = shaders["game_authored"]
    if not isinstance(game, dict) or set(game) != {"original", "source_present", "folder", "outside_folder"}:
        raise ValueError("Invalid public game shader section")
    folder, outside = game["folder"], game["outside_folder"]
    if (not isinstance(folder, dict) or set(folder) != {"original", "source_present", "categories"} or not isinstance(folder["categories"], dict) or
            list(folder["categories"]) != sorted(FOLDER_CATEGORIES) or not isinstance(outside, dict) or set(outside) != {"original", "source_present"}):
        raise ValueError("Invalid public shader categories")
    total = 0
    for name, row in folder["categories"].items():
        if not isinstance(row, dict) or set(row) != {"original", "source_present"} or count(row["original"]) != FOLDER_CATEGORIES[name] or count(row["source_present"]) > row["original"]:
            raise ValueError("Public shader category differs from the original inventory")
        total += row["source_present"]
    if (folder["original"] != sum(FOLDER_CATEGORIES.values()) or folder["source_present"] != total or count(outside["original"]) < 1 or
            count(outside["source_present"]) > outside["original"] or game["original"] != folder["original"] + outside["original"] or
            game["source_present"] != total + outside["source_present"]):
        raise ValueError("Public shader counts do not reconcile")
    excluded = shaders["not_game_scope"]
    if not isinstance(excluded, dict) or set(excluded) != {"third_party", "engine"}: raise ValueError("Invalid excluded shader counts")
    count(excluded["third_party"]); count(excluded["engine"])
    if shaders["fidelity"] != {"status": "not_measured"}: raise ValueError("Shader fidelity cannot be claimed by this report")
    compilation = shaders["compilation"]
    if compilation == {"status": "not_measured"}: return shaders
    if (not isinstance(compilation, dict) or set(compilation) != {"status", "target", "source_fingerprint", "compiled"} or compilation["status"] != "sealed" or
            compilation["target"] not in playercode.TARGETS or not isinstance(compilation["source_fingerprint"], str) or
            HASH.fullmatch(compilation["source_fingerprint"]) is None or count(compilation["compiled"]) > game["source_present"]):
        raise ValueError("Unsupported shader compilation claim")
    return shaders
