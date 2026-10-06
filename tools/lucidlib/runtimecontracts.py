"""Seal exact Theme metadata forwards from genuine native-selected Mono libraries."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import subprocess
import uuid

from . import bindings, playercode, playerschema, themebindings, themecontracts
from .bootstrap import dotnet_environment, load_lock, managed_path, validate_work_dir, write_json
from .verification import UNITY_VERSION, artifact_fingerprint, find_editor

PROFILE = "zone-theme-v1"
MAX_INVENTORY = 256 * 1024 * 1024
MAX_REPORT = 4 * 1024 * 1024
FLAGS = ("player_clr_loads", "editor_invoked", "runtime_verified", "layout_approved",
         "runtime_selection_verified", "player_schema_verified", "remap_approved",
         "gameplay_verified", "references_modified", "compiler_response_files_verified",
         "implicit_dependency_resolution")
RETURNED = ("Game.Runtime", "HLUnityCore.Runtime", "Unity.Addressables")


def _equal(left,right):
    """JSON identity preserves integer/boolean/null distinctions recursively."""
    if type(left) is not type(right):return False
    if isinstance(left,dict):return left.keys()==right.keys() and all(_equal(left[k],right[k]) for k in left)
    if isinstance(left,list):return len(left)==len(right) and all(_equal(a,b) for a,b in zip(left,right))
    return left==right


def _keys(value, expected, description):
    if type(value) is not dict or set(value) != set(expected):
        raise ValueError(description + " object shape differs")


def _json(path, maximum=MAX_REPORT):
    raw = playercode._regular(path, maximum).read_bytes()
    value = playercode._strict_json(raw)
    if not isinstance(value, dict): raise ValueError("Contract evidence must be an object")
    return raw, value


def _seal(path, *, maximum=playercode.MAX_FILE):
    return dict(playercode._file_record(path, maximum), **playercode._pe_identity(path))


def _schema_evidence(root, work, target, source, engine, player):
    path = managed_path(work, "player-schema", "latest-" + target + "-" + PROFILE + ".json")
    raw, report = _json(path, playerschema.MAX_SCHEMA)
    expected = {"status":"metadata-ready", "identity_status":"complete", "target":target,
                "profile":PROFILE, "source_fingerprint":source,
                "generated_by":"ProjectLucid.run_player_schema",
                "player_receipt_sha256":player["receipt_sha256"]}
    if any(type(report.get(k)) is not type(v) or report.get(k) != v for k,v in expected.items()):
        raise ValueError("A source-fresh genuine player schema is required")
    nonce = report.get("nonce")
    if not isinstance(nonce,str) or re.fullmatch(r"[a-f0-9]{32}",nonce) is None:
        raise ValueError("Invalid player schema nonce")
    run = managed_path(work,"player-schema","runs",nonce)
    manifest_path = managed_path(work,"player-schema","runs",nonce,"manifest.json")
    pending_path = managed_path(work,"player-schema","runs",nonce,"pending.json")
    mraw, manifest = _json(manifest_path)
    staged_raw, staged = _json(pending_path, playerschema.MAX_SCHEMA)
    if (report.get("run") != str(run) or report.get("staged_report_path") != str(pending_path) or
        report.get("staged_report_sha256") != hashlib.sha256(staged_raw).hexdigest() or
        any(not _equal(report.get(k),v) for k,v in staged.items() if k not in ("status","identity_status")) or
        not _equal(manifest.get("engine"),engine) or not _equal(manifest.get("modules"),playerschema._modules(player))):
        raise ValueError("Published player schema differs from genuine context or output")
    reader = report.get("reader_build")
    if not isinstance(reader,dict): raise ValueError("Player schema reader provenance absent")
    playerschema._schema_check(staged,manifest,mraw,reader)
    for seal in [reader["host"], *reader["sources"], *reader["output"], *manifest["modules"]]:
        playerschema._same_file(seal["path"],seal)
    if playerschema._tree_records(Path(reader["binary"]["path"]).parent,work) != reader["output"]:
        raise ValueError("Player schema reader directory changed")
    return {"path":str(path), "raw":raw, "sha256":hashlib.sha256(raw).hexdigest(), "report":report,
            "manifest_raw":mraw,"staged_raw":staged_raw}


def _profile_keys():
    keys=set()
    for assembly,name in playerschema.ALLOWED:
        if assembly=="netstandard":
            assembly="System" if name in ("System.ComponentModel.EditorBrowsableAttribute","System.ComponentModel.EditorBrowsableState") else "mscorlib"
        keys.add((assembly,name))
    return keys


def _subset(report):
    index=bindings._index(report); keys=_profile_keys()
    if not keys<=index.keys():raise ValueError("Original or loaded Theme graph is incomplete")
    return {"assemblies":[{"name":a,"types":[index[k] for k in sorted(keys) if k[0]==a]}
                          for a in sorted({k[0] for k in keys})]}


def _original_schema_evidence(original_path):
    """Verify portable content and immutable inputs; retain exact raw provenance."""
    original_raw, original = _json(original_path,MAX_INVENTORY)
    original_digest=hashlib.sha256(original_raw).hexdigest()
    content_digest=themebindings.require_reviewed_original_schema(original)
    input_seals=[]
    for name,digest in themebindings.ORIGINAL_INPUT_SHA256.items():
        seal=playercode._file_record(original[name],2*1024**3)
        if original.get(name+"_sha256")!=digest or seal["sha256"]!=digest:
            raise ValueError("Original code/metadata input changed")
        input_seals.append(seal)
    return {"original_path":str(Path(original_path).absolute()),"original_raw":original_raw,
            "original_sha256":original_digest,"original_schema_content_sha256":content_digest,
            "original":original,"input_seals":input_seals}


def _layout_evidence(root, original_path, loaded_path, source):
    """Seal inventory context; native disk declaration attestation remains mandatory."""
    evidence=_original_schema_evidence(original_path)
    original_raw,original=evidence["original_raw"],evidence["original"]
    original_digest=evidence["original_sha256"]
    input_seals=evidence["input_seals"]
    loaded_raw, loaded = _json(loaded_path,MAX_INVENTORY)
    if (loaded.get("status")!="ready" or loaded.get("unity_version")!=UNITY_VERSION or
        loaded.get("original_schema_sha256")!=original_digest or loaded.get("source_fingerprint")!=source or
        loaded.get("errors")!=[] or loaded.get("references_modified") is not False):
        raise ValueError("A current genuine loaded inventory against the original schema is required")
    op,ep=_subset(original),_subset(loaded)
    loaded_seals={}
    for key,record in bindings._index(ep).items():
        if record.get("loaded_module_identity_verified") is not True or not re.fullmatch(r"0x02[a-f0-9]{6}",record.get("token","")):
            raise ValueError("Loaded type token/module proof absent")
        for field in ("loaded_module","loaded_module_reader"):
            module=record.get(field);themebindings._module(module)
            actual=_seal(module["path"])
            if (actual["sha256"]!=module["sha256"] or actual["mvid"]!=module["mvid"] or
                actual["assembly_name"]!=module["assembly_identity"].split(",")[0]):
                raise ValueError("Loaded module disk/MVID identity changed")
            if field=="loaded_module_reader" and (module["sha256"]!=themebindings.CECIL or module["mvid"]!=themebindings.CECIL_MVID):
                raise ValueError("Loaded inventory used an unreviewed reader")
            loaded_seals[actual["path"]]=actual
    oi,ei=bindings._index(op),bindings._index(ep)
    themebindings._boxed_null(oi,"original");themebindings._boxed_null(ei,"editor")
    themebindings._exact_bridge(oi);themebindings._exact_bridge(ei)
    themebindings._exact_asset_reference(oi,False);themebindings._exact_asset_reference(ei,True)
    dependencies=[]
    for key in (("Unity.ResourceManager","UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`1"),
                ("System.Core","System.Collections.Generic.HashSet`1")):
        dependency=bindings._index(loaded).get(key)
        if not isinstance(dependency,dict) or dependency.get("loaded_module_identity_verified") is not True:
            raise ValueError("Actual loaded AssetReference dependency is absent")
        dependencies.append(dependency)
    loaded_modules=[]
    for module in {r["loaded_module"]["path"]:r["loaded_module"] for r in [*bindings._index(ep).values(),*dependencies]}.values():
        actual=_seal(module["path"]);actual.update(assembly_identity=module["assembly_identity"],kind="loaded_editor_module")
        loaded_modules.append(actual)
        loaded_seals[actual["path"]]=actual
    mscorlib=next(r for r in loaded_modules if r["assembly_name"]=="mscorlib")
    facade=_seal(Path(mscorlib["path"]).parent/"Facades/netstandard.dll")
    facade.update(assembly_identity=themecontracts.NETSTANDARD,kind="loaded_editor_facade")
    loaded_modules.append(facade)
    loaded_seals[facade["path"]]=facade
    package=root/"Library/PackageCache/com.unity.addressables@1.22.3/Runtime/AssetReference.cs"
    package_seal=playercode._file_record(package)
    if package_seal["sha256"]!=themebindings.AA_SOURCE:raise ValueError("Pinned Addressables conditional source changed")
    return {"original_path":evidence["original_path"],"original_raw":original_raw,"original_sha256":original_digest,
            "original_schema_content_sha256":evidence["original_schema_content_sha256"],
            "loaded_path":str(Path(loaded_path).absolute()),"loaded_raw":loaded_raw,"loaded_sha256":hashlib.sha256(loaded_raw).hexdigest(),
            "original_profile":op,"loaded_profile":ep,"loaded_modules":loaded_modules,
            "loaded_types":[record for _,record in sorted(bindings._index(ep).items())],"file_seals":[*input_seals,*loaded_seals.values(),package_seal]}


def _contract_modules(player,engine):
    staged=player["staged"];query=staged["native_profile_queries_before"]
    if not _equal(query,staged["native_profile_queries_after"]):raise ValueError("Native profile query changed")
    target=staged.get("target")
    if target not in playercode.TARGETS:raise ValueError("Genuine compiler target is absent")
    playercode._check_native_queries(staged,target,engine)
    core=Path(next(r["path"] for r in engine if r["name"]=="editor_core"))
    suffix=query["platform_profile_suffix"]
    directory=Path(query["mono_runtime_lib_directory"])
    if core.parent.name!="UnityEngine" or core.parent.parent.name!="Managed":raise ValueError("Native engine location unsupported")
    contents=core.parent.parent.parent
    if directory!=contents/"MonoBleedingEdge/lib/mono"/("unityjit-"+suffix):raise ValueError("Native-selected runtime directory differs")
    reference=contents/"NetStandard/ref/2.1.0/netstandard.dll"
    actual_refs=[r for r in staged["inputs"] if r["kind"]=="precompiled_reference" and Path(r["path"]).name=="netstandard.dll"]
    if len(actual_refs)!=1 or actual_refs[0]["path"]!=str(reference):raise ValueError("Compiler contract reference is missing or ambiguous")
    paths=[("compiler_reference",reference,themecontracts.NETSTANDARD),
           ("runtime_facade",directory/"Facades/netstandard.dll",themecontracts.NETSTANDARD),
           ("runtime_mscorlib",directory/"mscorlib.dll",themecontracts.MSCORLIB),
           ("runtime_system",directory/"System.dll",themecontracts.SYSTEM)]
    for name in RETURNED:
        rows=[r for r in staged["modules"] if r["assembly_name"]==name]
        if len(rows)!=1:raise ValueError("Genuine returned contract-bearing assembly absent")
        paths.append(("returned_player_assembly",Path(rows[0]["path"]),name+", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"))
    modules=[]
    for kind,path,identity in paths:
        module=_seal(path);module.update(kind=kind,assembly_identity=identity)
        if module["assembly_name"]!=identity.split(",")[0]:raise ValueError("Contract file assembly name differs")
        modules.append(module)
    if modules[0]["sha256"]!=actual_refs[0]["sha256"] or modules[0]["size"]!=actual_refs[0]["size"]:
        raise ValueError("Compiler contract reference changed")
    return modules


def _build_reader(root,work,run,engine):
    project=playercode._regular(root/"tools/theme-contracts/ThemeContracts.csproj",65536)
    code=playercode._regular(project.with_name("Program.cs"),1024*1024)
    declarations=playercode._regular(project.with_name("LoadedDeclarations.cs"),1024*1024)
    sources=[playercode._file_record(project),playercode._file_record(code),playercode._file_record(declarations)]
    sdk=load_lock().get("artifacts",{}).get("dotnet",{})
    if sdk.get("version")!="10.0.401" or re.fullmatch(r"[a-f0-9]{128}",sdk.get("sha512","")) is None:
        raise ValueError("Contract reader requires the pinned SDK")
    host=managed_path(work,"tools","dotnet","dotnet");host_seal=playercode._file_record(host)
    env=dotnet_environment(work,host.parent)
    version=subprocess.run([str(host),"--version"],capture_output=True,text=True,timeout=30,env=env)
    if version.returncode or version.stdout.strip()!=sdk["version"]:raise ValueError("Contract SDK version changed")
    output,intermediate=run/"reader",run/"obj";output.mkdir(exist_ok=False);intermediate.mkdir(exist_ok=False)
    managed=Path(next(r["path"] for r in engine if r["name"]=="cecil")).parent
    log=run/"build.log"
    command=[str(host),"build",str(project),"--nologo","-c","Release","-p:UnityManagedRoot="+str(managed),
             "-p:BaseIntermediateOutputPath="+str(intermediate)+os.sep,"-p:OutputPath="+str(output)+os.sep,"-p:GeneratePackageOnBuild=false"]
    with log.open("xb") as stream:result=subprocess.run(command,stdout=stream,stderr=subprocess.STDOUT,timeout=180,env=env)
    if result.returncode:raise ValueError("Contract reader compilation failed; inspect its build log")
    binary=playercode._regular(output/"ProjectLucid.ThemeContracts.dll")
    for seal in [host_seal,*sources]:playerschema._same_file(seal["path"],seal)
    return {"host":host_seal,"sdk_version":sdk["version"],"sdk_archive_sha512":sdk["sha512"],"sources":sources,
            "binary":_seal(binary),"output":playerschema._tree_records(output,work),"log":str(log),"env":env}


def _attest_loaded(reader,manifest):
    """Read exact disk declarations again; caller JSON flags are insufficient."""
    path=Path(manifest["run"])/"manifest.json"
    result=subprocess.run([reader["host"]["path"],reader["binary"]["path"],"--attest-loaded",str(path)],
      capture_output=True,timeout=180,env=reader.get("env") or dotnet_environment(Path(manifest["work_root"]),Path(reader["host"]["path"]).parent))
    if result.returncode:
        raise ValueError("Loaded disk declaration attestation failed: "+result.stderr.decode("utf-8",errors="replace")[:2048])
    if len(result.stdout)>MAX_REPORT:raise ValueError("Loaded declaration evidence exceeds bound")
    actual=playercode._strict_json(result.stdout)
    if type(actual) is not dict:raise ValueError("Loaded declaration evidence must be an object")
    return actual


def _report_check(report,manifest,mraw,reader,layouts):
    expected={"schema_version":1,"command":"runtime-contracts","profile":PROFILE,"status":"pending-native-verification",
      "identity_status":"pending-wrapper","manifest_sha256":hashlib.sha256(mraw).hexdigest(),"errors":[],
      "resolver_provenance":"metadata-resolution-over-sealed-plan-candidates"}
    for key in ("target","nonce","source_fingerprint","project_root","work_root","run","player_receipt_sha256",
                "player_schema_sha256","original_schema_sha256","loaded_inventory_sha256"):expected[key]=manifest[key]
    expected.update({k:False for k in FLAGS})
    cecil=next(r for r in manifest["engine"] if r["name"]=="cecil")
    expected.update(reader_path=str(Path(reader["binary"]["path"]).parent/Path(cecil["path"]).name),
                    reader_sha256=cecil["sha256"],reader_mvid=cecil["mvid"])
    if any(type(report.get(k)) is not type(v) or report.get(k)!=v for k,v in expected.items()):
        raise ValueError("Contract pending identity, scope, reader or approval flags differ")
    _keys(report, set(expected) | {"modules","loaded_declarations"}, "Contract pending report")
    if (not _equal(manifest.get("loaded_modules"),layouts["loaded_modules"]) or
        not _equal(manifest.get("loaded_types"),layouts["loaded_types"])):
        raise ValueError("Loaded attestation context differs from fresh inventory")
    actual=_attest_loaded(reader,manifest)
    if not _equal(actual,report.get("loaded_declarations")):
        raise ValueError("Loaded declaration evidence differs from current disk metadata")
    rows=report.get("modules")
    if not isinstance(rows,list) or len(rows)!=7:raise ValueError("Contract metadata requires exactly seven modules")
    if any(not isinstance(r,dict) for r in rows) or len({r.get("path") for r in rows})!=7:
        raise ValueError("Contract metadata module identities are duplicated")
    for seal in manifest["modules"]:
        row=next((r for r in rows if r.get("path")==seal["path"]),None)
        _keys(row, {"path","sha256","size","mvid","assembly","role","reference_scopes","defined_types","forwarders"},
              "Contract metadata module")
        expected_module={"sha256":seal["sha256"],"size":seal["size"],"mvid":seal["mvid"],"assembly":seal["assembly_identity"],"role":seal["kind"]}
        if row is None or any(type(row.get(k)) is not type(v) or row.get(k)!=v for k,v in expected_module.items()):
            raise ValueError("Contract reader module bytes/MVID/full identity differ")
        if seal["kind"]=="returned_player_assembly":
            refs=row.get("reference_scopes")
            if not isinstance(refs,list) or refs.count(themecontracts.NETSTANDARD)!=1 or row.get("defined_types")!=[] or row.get("forwarders")!=[]:
                raise ValueError("Genuine player module declared contract scope differs")
    projection=themecontracts.graph_projection(report,manifest["native_query"],manifest["target"],
                                               layouts["original_profile"],layouts["loaded_profile"])
    return list(projection.rows.values())


def _reader_check(root, work, run, reader):
    """Recheck the exact current tool build, not only its installed Cecil copy."""
    _keys(reader, {"host","sdk_version","sdk_archive_sha512","sources","binary","output","log"},
          "Contract reader build")
    sdk=load_lock().get("artifacts",{}).get("dotnet",{})
    if (sdk.get("version")!="10.0.401" or reader.get("sdk_version")!=sdk.get("version") or
        reader.get("sdk_archive_sha512")!=sdk.get("sha512") or
        re.fullmatch(r"[a-f0-9]{128}",sdk.get("sha512","")) is None):
        raise ValueError("Contract reader SDK provenance differs")
    host=reader.get("host");sources=reader.get("sources");binary=reader.get("binary");output=reader.get("output")
    expected_host=managed_path(work,"tools","dotnet","dotnet")
    expected_sources=[root/"tools/theme-contracts/ThemeContracts.csproj",root/"tools/theme-contracts/Program.cs",root/"tools/theme-contracts/LoadedDeclarations.cs"]
    expected_binary=run/"reader/ProjectLucid.ThemeContracts.dll"
    if (not isinstance(host,dict) or host.get("path")!=str(expected_host) or
        not isinstance(sources,list) or len(sources)!=3 or any(not isinstance(s,dict) for s in sources) or
        [s.get("path") for s in sources]!=list(map(str,expected_sources)) or
        not isinstance(binary,dict) or binary.get("path")!=str(expected_binary) or
        not isinstance(output,list) or any(not isinstance(s,dict) for s in output)):
        raise ValueError("Contract reader source/output ownership differs")
    for seal in [host,*sources,*output]:playerschema._same_file(seal.get("path",""),seal)
    if not _equal(_seal(expected_binary),binary):raise ValueError("Contract reader compiled module changed")
    if not _equal(playerschema._tree_records(expected_binary.parent,work),output):
        raise ValueError("Contract reader output directory changed")
    return reader


def verify_runtime_contract_receipt(root,work,target,source,engine,player,schema,layouts):
    """Consume one authoritative current receipt; pending files never suffice."""
    path=managed_path(work,"runtime-contracts","latest-"+target+"-"+PROFILE+".json")
    raw,report=_json(path)
    expected={"schema_version":1,"command":"runtime-contracts","profile":PROFILE,"target":target,
      "status":"metadata-ready","identity_status":"complete","source_fingerprint":source,
      "generated_by":"ProjectLucid.run_runtime_contracts",
      "identity_verified_by":"native-python-prepost-player-schema-module-cecil-v1",
      "player_receipt_sha256":player["receipt_sha256"],"player_schema_sha256":schema["sha256"],
      "original_schema_sha256":layouts["original_sha256"],"loaded_inventory_sha256":layouts["loaded_sha256"],
      "project_root":str(root),"work_root":str(work),
      "player_receipt_path":player["receipt_path"],"player_schema_path":schema["path"],
      "original_schema_path":layouts["original_path"],"loaded_inventory_path":layouts["loaded_path"]}
    expected.update({k:False for k in FLAGS})
    if any(type(report.get(k)) is not type(v) or report.get(k)!=v for k,v in expected.items()):
        raise ValueError("Contract latest receipt is stale, pending or incorrectly approved")
    nonce=report.get("nonce")
    if not isinstance(nonce,str) or re.fullmatch(r"[a-f0-9]{32}",nonce) is None:
        raise ValueError("Contract receipt nonce is invalid")
    run=managed_path(work,"runtime-contracts","runs",nonce);pending=run/"pending.json";context=run/"manifest.json"
    mraw,manifest=_json(context);pending_raw,staged=_json(pending)
    if (report.get("run")!=str(run) or report.get("staged_report_path")!=str(pending) or
        report.get("staged_report_sha256")!=hashlib.sha256(pending_raw).hexdigest()):
        raise ValueError("Contract receipt is outside its owned staged context")
    manifest_expected={"schema_version":1,"command":"runtime-contracts","profile":PROFILE,"target":target,
      "nonce":nonce,"source_fingerprint":source,"project_root":str(root),"work_root":str(work),"run":str(run),
      "player_receipt_sha256":player["receipt_sha256"],"player_schema_sha256":schema["sha256"],
      "original_schema_sha256":layouts["original_sha256"],"loaded_inventory_sha256":layouts["loaded_sha256"],
      "compiler_nonce":player["receipt"]["identity_nonce"],"native_query":player["staged"]["native_profile_queries_before"],
      "modules":_contract_modules(player,engine),"engine":engine,"loaded_modules":layouts["loaded_modules"],"loaded_types":layouts["loaded_types"]}
    if not _equal(manifest,manifest_expected):raise ValueError("Contract manifest differs from genuine current compilation/layout context")
    reader=_reader_check(root,work,run,report.get("reader_build"))
    rows=_report_check(staged,manifest,mraw,reader,layouts)
    _keys(report, set(staged) | {"generated_by","identity_verified_by","contract_rows","staged_report_path",
        "staged_report_sha256","player_receipt_path","player_schema_path","original_schema_path",
        "loaded_inventory_path","reader_build","log"}, "Contract authoritative report")
    if report.get("log") != str(run/"reader.log"):
        raise ValueError("Contract reader log is outside its owned run")
    if (not _equal(report.get("contract_rows"),rows) or
        any(not _equal(report.get(k),v) for k,v in staged.items() if k not in ("status","identity_status"))):
        raise ValueError("Contract authoritative receipt differs from exact staged metadata")
    if (playercode._regular(path,MAX_REPORT).read_bytes()!=raw or
        playercode._regular(pending,MAX_REPORT).read_bytes()!=pending_raw or
        playercode._regular(context,MAX_REPORT).read_bytes()!=mraw):
        raise ValueError("Contract evidence changed during consumption")
    return {"path":str(path),"raw":raw,"sha256":hashlib.sha256(raw).hexdigest(),
      "manifest_raw":mraw,"staged_raw":pending_raw,"report":report,"contract_rows":rows,
      "native_query":manifest["native_query"]}


def _merge_contract_metadata(evidence,source):
    """Keep target-specific paths even when bytes/full assembly names coincide."""
    if not isinstance(evidence,dict) or set(evidence)!=set(playercode.TARGETS):
        raise ValueError("Exactly three target contract receipts are required")
    modules={};queries={}
    for target in sorted(evidence):
        proof=evidence[target];report=proof["report"]
        if report.get("source_fingerprint")!=source or any(report.get(k) is not False for k in FLAGS):
            raise ValueError("Target contract receipt source/approval differs")
        queries[target]=proof["native_query"]
        for module in report["modules"]:
            path=module["path"]
            if path in modules and not _equal(modules[path],module):raise ValueError("Shared contract module metadata disagrees")
            modules[path]=module
    if len(modules)!=19:raise ValueError("Contract closure must contain exactly nineteen distinct module paths")
    metadata={"reader_sha256":themecontracts.CECIL,"reader_mvid":themecontracts.CECIL_MVID,
              "modules":[modules[p] for p in sorted(modules)]}
    metadata.update({k:False for k in FLAGS})
    return {"source_fingerprint":source,"queries":queries,"metadata":metadata}


def collect_theme_contract_evidence(repo_root,work_dir,original_schema,loaded_inventory):
    """Recheck three genuine compiles, schemas and contract receipts without writes."""
    root,work=Path(repo_root).resolve(strict=True),validate_work_dir(Path(work_dir))
    source=artifact_fingerprint(root);editor=playercode._regular(find_editor(),2*1024**3);engine=playercode._engine_identity(editor)
    initial=None;players=None;layouts=None
    # Repeat the entire dependency chain; this is freshness checking, not a
    # transactional filesystem snapshot or proof of packaged player execution.
    for _ in range(2):
        layouts=_layout_evidence(root,original_schema,loaded_inventory,source);contracts={};players={}
        for target in sorted(playercode.TARGETS):
            player=playerschema._player_evidence(root,work,target,source,engine)
            schema=_schema_evidence(root,work,target,source,engine,player)
            contracts[target]=verify_runtime_contract_receipt(root,work,target,source,engine,player,schema,layouts)
            players[target]=schema["report"]
        sealed={t:{k:e[k] for k in ("raw","manifest_raw","staged_raw")} for t,e in contracts.items()}
        sealed.update(original=layouts["original_raw"],loaded=layouts["loaded_raw"])
        if initial is not None and initial!=sealed:raise ValueError("Theme contract dependency changed between checks")
        initial=sealed
        for seal in layouts["file_seals"]:playerschema._same_file(seal["path"],seal)
        if artifact_fingerprint(root)!=source or not _equal(playercode._engine_identity(editor),engine):
            raise ValueError("Theme comparison source/installed engine changed")
        if artifact_fingerprint(root)!=source:raise ValueError("Source changed during final Theme engine check")
    return {"source_fingerprint":source,"contracts":_merge_contract_metadata(contracts,source),"players":players,
            "layouts":layouts,"receipts":{t:{"path":e["path"],"sha256":e["sha256"]} for t,e in contracts.items()}}


def compare_verified_theme_planes(repo_root,work_dir,original_schema,loaded_inventory):
    """Evidence-ready only; root still owns schema/rule/owner and Unity acceptance."""
    proof=collect_theme_contract_evidence(repo_root,work_dir,original_schema,loaded_inventory)
    layouts=proof["layouts"];original=playercode._strict_json(layouts["original_raw"]);loaded=playercode._strict_json(layouts["loaded_raw"])
    package=Path(repo_root)/"Library/PackageCache/com.unity.addressables@1.22.3/Runtime/AssetReference.cs"
    package_raw=playercode._regular(package).read_bytes()
    result=themebindings.compare_theme_planes(original,proof["players"],loaded,
      schema_sha256=layouts["original_sha256"],source_fingerprint=proof["source_fingerprint"],
      package_source_bytes=package_raw,runtime_contract_evidence=proof["contracts"])
    remaining=[r for r in result["issues"] if r.get("reason")!="runtime_contract_wrapper_freshness_not_integrated"]
    expected_pairs={("original","editor")}|{pair for t in themebindings.TARGETS for pair in (("original",t+"-player"),(t+"-player","editor"))}
    expected_types=[list(k) for k in sorted(themebindings.PROFILE_TYPES)]
    if (remaining or len(result["issues"])!=1 or len(result["pairs"])!=7 or
        {(p.get("left_plane"),p.get("right_plane")) for p in result["pairs"]}!=expected_pairs or
        any(p.get("issues") or p.get("checked_types")!=expected_types for p in result["pairs"]) or
        set(result.get("target_contract_rows",{}))!=set(themebindings.TARGETS) or
        any(len(r)!=20 for r in result["target_contract_rows"].values())):
        raise ValueError("Theme three-plane comparison still has unresolved structural evidence")
    # The frozen pure comparator deliberately cannot make this outer decision.
    # It is made only here after genuine authoritative receipt revalidation.
    result.update(status="evidence-ready-for-root-review",issues=[],contract_receipts=proof["receipts"],
      freshness_verified_by="native-python-current-three-target-compilation-schema-contract-loaded-module-v1",
      owners_verified=False,rules_modified=False,runtime_verified=False)
    after=collect_theme_contract_evidence(repo_root,work_dir,original_schema,loaded_inventory)
    if (not _equal(after["receipts"],proof["receipts"]) or not _equal(after["contracts"],proof["contracts"]) or
        not _equal(after["players"],proof["players"]) or after["source_fingerprint"]!=proof["source_fingerprint"] or
        any(after["layouts"][k]!=layouts[k] for k in ("original_raw","loaded_raw"))):
        raise ValueError("Theme evidence changed during three-plane comparison")
    for seal in layouts["file_seals"]:playerschema._same_file(seal["path"],seal)
    if artifact_fingerprint(Path(repo_root).resolve(strict=True))!=proof["source_fingerprint"]:
        raise ValueError("Source changed before final Theme comparison return")
    return result


def run_runtime_contracts(repo_root,work_dir,target,original_schema,loaded_inventory):
    """Publish metadata evidence only after exact current native pre/post identity."""
    if target not in playercode.TARGETS:raise ValueError("Unsupported contract target")
    root,work=Path(repo_root).resolve(strict=True),validate_work_dir(Path(work_dir))
    if not root.is_dir():raise ValueError("Contract project root is not a directory")
    version=playercode._regular(root/"ProjectSettings/ProjectVersion.txt",65536).read_text()
    if re.search(r"^m_EditorVersion: "+re.escape(UNITY_VERSION)+"$",version,re.M) is None:
        raise ValueError("Matching Unity project version required")
    latest=managed_path(work,"runtime-contracts","latest-"+target+"-"+PROFILE+".json")
    if latest.exists():playercode._regular(latest,MAX_REPORT)
    lock=managed_path(work,"locks","runtime-contracts.lock");lock.parent.mkdir(parents=True,exist_ok=True)
    try:lock.mkdir()
    except FileExistsError as error:raise ValueError("Another contract metadata run owns the lock") from error
    run=None;created=False
    try:
        source=artifact_fingerprint(root);editor=playercode._regular(find_editor(),2*1024**3);engine=playercode._engine_identity(editor)
        player=playerschema._player_evidence(root,work,target,source,engine)
        schema=_schema_evidence(root,work,target,source,engine,player)
        layouts=_layout_evidence(root,original_schema,loaded_inventory,source);modules=_contract_modules(player,engine)
        nonce=uuid.uuid4().hex;run=managed_path(work,"runtime-contracts","runs",nonce);run.mkdir(parents=True,exist_ok=False);created=True
        reader=_build_reader(root,work,run,engine)
        manifest={"schema_version":1,"command":"runtime-contracts","profile":PROFILE,"target":target,"nonce":nonce,
          "source_fingerprint":source,"project_root":str(root),"work_root":str(work),"run":str(run),
          "player_receipt_sha256":player["receipt_sha256"],"player_schema_sha256":schema["sha256"],
          "original_schema_sha256":layouts["original_sha256"],"loaded_inventory_sha256":layouts["loaded_sha256"],
          "compiler_nonce":player["receipt"]["identity_nonce"],"native_query":player["staged"]["native_profile_queries_before"],
          "modules":modules,"engine":engine,"loaded_modules":layouts["loaded_modules"],"loaded_types":layouts["loaded_types"]}
        context=run/"manifest.json";pending=run/"pending.json";write_json(context,manifest);mraw=playercode._regular(context,MAX_REPORT).read_bytes()
        if artifact_fingerprint(root)!=source:raise ValueError("Source changed before contract metadata reading")
        log=run/"reader.log"
        with log.open("xb") as stream:
            result=subprocess.run([reader["host"]["path"],reader["binary"]["path"],str(context),str(pending)],
                                  stdout=stream,stderr=subprocess.STDOUT,timeout=180,env=reader["env"])
        if result.returncode:raise ValueError("Contract metadata reader failed; inspect its log")
        pending_raw,report=_json(pending);contract_rows=_report_check(report,manifest,mraw,reader,layouts)
        for _ in range(2):
            if playercode._regular(context,MAX_REPORT).read_bytes()!=mraw or playercode._regular(pending,MAX_REPORT).read_bytes()!=pending_raw:
                raise ValueError("Contract context or pending output changed")
            again=playerschema._player_evidence(root,work,target,source,engine)
            if any(again[k]!=player[k] for k in ("receipt_raw","staged_raw","context_raw")) or not _equal(_contract_modules(again,engine),modules):
                raise ValueError("Genuine compiler or selected contract modules changed")
            fresh_schema=_schema_evidence(root,work,target,source,engine,again)
            if any(fresh_schema[k]!=schema[k] for k in ("raw","manifest_raw","staged_raw")):raise ValueError("Player schema changed")
            if playercode._regular(layouts["original_path"],MAX_INVENTORY).read_bytes()!=layouts["original_raw"] or playercode._regular(layouts["loaded_path"],MAX_INVENTORY).read_bytes()!=layouts["loaded_raw"]:
                raise ValueError("Original or loaded inventory changed")
            for seal in [*layouts["file_seals"],reader["host"],*reader["sources"],*reader["output"]]:playerschema._same_file(seal["path"],seal)
            if playerschema._tree_records(Path(reader["binary"]["path"]).parent,work)!=reader["output"]:raise ValueError("Contract reader output changed")
            if artifact_fingerprint(root)!=source or not _equal(playercode._engine_identity(editor),engine):raise ValueError("Source or engine changed")
            if artifact_fingerprint(root)!=source:raise ValueError("Source changed during final engine check")
        if _report_check(report,manifest,mraw,reader,layouts)!=contract_rows:raise ValueError("Contract graph changed")
        report.update(status="metadata-ready",identity_status="complete",generated_by="ProjectLucid.run_runtime_contracts",
          identity_verified_by="native-python-prepost-player-schema-module-cecil-v1",contract_rows=contract_rows,
          staged_report_path=str(pending),staged_report_sha256=hashlib.sha256(pending_raw).hexdigest(),
          player_receipt_path=player["receipt_path"],player_schema_path=schema["path"],
          original_schema_path=layouts["original_path"],loaded_inventory_path=layouts["loaded_path"],
          reader_build={k:v for k,v in reader.items() if k!="env"},log=str(log))
        if latest.exists():playercode._regular(latest,MAX_REPORT)
        if (artifact_fingerprint(root)!=source or playercode._regular(pending,MAX_REPORT).read_bytes()!=pending_raw or
            playercode._regular(context,MAX_REPORT).read_bytes()!=mraw):raise ValueError("Source or staged context changed before contract publication")
        write_json(latest,report)
        return {"status":"metadata-ready","target":target,"profile":PROFILE,"contract_count":len(contract_rows),"report_path":str(latest),
                "runtime_verified":False,"player_schema_verified":False,"remap_approved":False,"gameplay_verified":False}
    except (OSError,ValueError,subprocess.SubprocessError,bindings.LayoutError) as error:
        if created and run is not None and run.is_dir():
            write_json(run/"wrapper-failure.json",{"status":"failed","target":target,"profile":PROFILE,"error":str(error),"generated_by":"ProjectLucid.run_runtime_contracts"})
        raise
    finally:lock.rmdir()
