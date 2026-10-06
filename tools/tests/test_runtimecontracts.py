import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from lucidlib import bootstrap, runtimecontracts as rc, themecontracts as contracts


def fixture():
    # Synthetic metadata is only a guard fixture, never a generated receipt.
    contents='/Installed/Unity.app/Contents';directory=contents+'/MonoBleedingEdge/lib/mono/unityjit-macos'
    query={'target':'StandaloneOSX','unity_version':'2022.3.54f1','api_compatibility_value':6,
      'api_compatibility_name':'NET_Standard_2_0','scripting_backend_value':0,'scripting_backend_name':'Mono2x',
      'platform_profile_suffix':'macos','compatibility_profile_folder':'unityaot-macos',
      'runtime_selection_verified':False,'layout_approved':False,'gameplay_verified':False,
      'runtime_assemblies_loaded':False,'build_player_called':False,'methods':copy.deepcopy(contracts.QUERY_METHODS),
      'module_path':contents+'/Managed/UnityEngine/UnityEditor.CoreModule.dll',
      'module_sha256':'fb89c4764167359e3796b9a7e893f507d508f86eb8ad7daa55c78bf07abcd0b9',
      'module_mvid':'70a991b8-3ee9-4c31-8ecd-89fa65e3f64c',
      'module_assembly':'UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null',
      'mono_runtime_lib_directory':directory}
    suffix,facade,core,system=contracts.TARGET_PINS['macos']
    def module(path,pin,identity,role):
        return {'path':path,'sha256':pin[0],'mvid':pin[1],'size':100,'assembly':identity,'role':role,
                'defined_types':[],'forwarders':[],'reference_scopes':[]}
    reference=module(contents+'/NetStandard/ref/2.1.0/netstandard.dll',contracts.REFERENCE_PIN,contracts.NETSTANDARD,'compiler_reference')
    forwarded=module(directory+'/Facades/netstandard.dll',facade,contracts.NETSTANDARD,'runtime_facade')
    endpoints={contracts.MSCORLIB:module(directory+'/mscorlib.dll',core,contracts.MSCORLIB,'runtime_mscorlib'),
               contracts.SYSTEM:module(directory+'/System.dll',system,contracts.SYSTEM,'runtime_system')}
    original={'assemblies':[{'name':a,'types':[]} for a in ('mscorlib','System')]}
    for i,(name,scope) in enumerate(sorted(contracts.CONTRACTS.items())):
        record={'name':name,'token':'0x02'+format(i+1,'06x'),'attributes':257,'is_enum':name in contracts.ENUMS,'base_type':{},'fields':[]}
        if name in contracts.ENUMS:
            record['fields']=[{'name':'value__','attributes':1542,'type':{'full_name':'System.Int32'},'has_constant':False}]
            native={'assembly':scope.split(',')[0],'full_name':name,'is_enum':True,'fields':[{'name':'value__','attributes':1542,'field_type':{'reflection_full_name':'System.Int32'},'has_default_value':False}]}
            for n,v in contracts.ENUMS[name].items():
                record['fields'].append({'name':n,'attributes':32854,'has_constant':True,'constant':v})
                native['fields'].append({'name':n,'attributes':32854,'has_default_value':True,'default_value':v,'default_value_complete':True})
            next(a for a in original['assemblies'] if a['name']==native['assembly'])['types'].append(native)
        reference['defined_types'].append(copy.deepcopy(record));endpoints[scope]['defined_types'].append(copy.deepcopy(record))
        forwarded['forwarders'].append({'name':name,'token':'0x27'+format(i+1,'06x'),'scope':scope,'is_forwarder':True,'attributes':0x200000,'declaring_type':None})
    modules=[reference,forwarded,*endpoints.values()]
    for name in rc.RETURNED:
        m=module('/Owned/player-code/runs/'+('1'*32)+'/assemblies/'+name+'.dll',('a'*64,'11111111-1111-1111-1111-111111111111'),name+', Version=0.0.0.0, Culture=neutral, PublicKeyToken=null','returned_player_assembly');m['reference_scopes']=[contracts.NETSTANDARD];modules.append(m)
    seals=[{'path':m['path'],'sha256':m['sha256'],'size':m['size'],'mvid':m['mvid'],'assembly_name':m['assembly'].split(',')[0],'assembly_identity':m['assembly'],'kind':m['role']} for m in modules]
    manifest={'schema_version':1,'command':'runtime-contracts','profile':'zone-theme-v1','target':'macos','nonce':'2'*32,
      'source_fingerprint':'b'*64,'project_root':'/Project','work_root':'/Owned','run':'/Owned/runtime-contracts/runs/'+('2'*32),
      'player_receipt_sha256':'c'*64,'player_schema_sha256':'d'*64,'original_schema_sha256':rc.themebindings.ORIGINAL_SCHEMA,
      'loaded_inventory_sha256':'e'*64,'native_query':query,'modules':seals,'loaded_modules':[],'loaded_types':[],
      'engine':[{'name':'cecil','path':'/Installed/Unity.app/Contents/Managed/Unity.Cecil.dll','sha256':contracts.CECIL,'mvid':contracts.CECIL_MVID}]}
    raw=json.dumps(manifest,sort_keys=True).encode();reader={'binary':{'path':'/Owned/reader/ProjectLucid.ThemeContracts.dll'}}
    report={k:manifest[k] for k in ('target','nonce','source_fingerprint','project_root','work_root','run','player_receipt_sha256','player_schema_sha256','original_schema_sha256','loaded_inventory_sha256')}
    report.update(schema_version=1,command='runtime-contracts',profile='zone-theme-v1',status='pending-native-verification',identity_status='pending-wrapper',
      manifest_sha256=hashlib.sha256(raw).hexdigest(),errors=[],resolver_provenance='metadata-resolution-over-sealed-plan-candidates',
      reader_path='/Owned/reader/Unity.Cecil.dll',reader_sha256=contracts.CECIL,reader_mvid=contracts.CECIL_MVID,modules=modules,loaded_declarations={'fixture':'synthetic-only'})
    report.update({k:False for k in rc.FLAGS})
    layouts={'original_profile':original,'loaded_profile':copy.deepcopy(original),'loaded_modules':[],'loaded_types':[]}
    return report,manifest,raw,reader,layouts


class RuntimeContractReportTests(unittest.TestCase):
    def setUp(self):
        patch=mock.patch.object(rc,'_attest_loaded',return_value={'fixture':'synthetic-only'})
        patch.start();self.addCleanup(patch.stop)
    def reject(self,change):
        values=fixture();change(*values)
        with self.assertRaises((ValueError,KeyError,TypeError)):rc._report_check(*values)
    def test_exact_pending_metadata_has_twenty_rows(self):self.assertEqual(len(rc._report_check(*fixture())),20)
    def test_canonical_digest_cannot_replace_raw_artifact_receipt_field(self):
        self.reject(lambda r,m,b,reader,l:r.update(original_schema_sha256=rc.themebindings.ORIGINAL_SCHEMA_CONTENT))
    def test_loaded_attestation_report_must_match_actual_disk(self):
        self.reject(lambda r,m,b,reader,l:r['loaded_declarations'].update(token='0x02000022'))
    def test_loaded_attestation_context_cannot_substitute_types(self):
        self.reject(lambda r,m,b,reader,l:m['loaded_types'].append({'token':'0x02000022'}))
    def test_loaded_attestation_context_cannot_substitute_modules(self):
        self.reject(lambda r,m,b,reader,l:m['loaded_modules'].append({'assembly_identity':'forged'}))
    def test_native_loaded_attestation_failure_is_fatal(self):
        with mock.patch.object(rc,'_attest_loaded',side_effect=ValueError('actual TypeDef mismatch')):
            with self.assertRaises(ValueError):rc._report_check(*fixture())
    def test_pending_cannot_claim_complete(self):self.reject(lambda r,m,b,reader,l:r.update(identity_status='complete'))
    def test_stale_source_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(source_fingerprint='a'*64))
    def test_wrong_nonce_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(nonce='3'*32))
    def test_context_hash_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(manifest_sha256='0'*64))
    def test_wrong_reader_path_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(reader_path='/Installed/Unity.Cecil.dll'))
    def test_wrong_reader_mvid_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(reader_mvid='0'*36))
    def test_report_flag_types_and_claims_rejected(self):
        for k in rc.FLAGS:
            for value in (True,0,None):
                with self.subTest(key=k,value=value):self.reject(lambda r,m,b,reader,l:r.update({k:value}))
    def test_pending_unknown_owner_approval_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(owners_verified=True))
    def test_pending_unknown_false_flag_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(rules_modified=False))
    def test_module_unknown_approval_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][0].update(layout_verified=True))
    def test_module_size_float_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][0].update(size=100.0))
    def test_schema_receipt_substitution_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(player_schema_sha256='0'*64))
    def test_rsp_resolution_claim_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(resolver_provenance='actual-compiler-resolved'))
    def test_error_array_rejected(self):self.reject(lambda r,m,b,reader,l:r.update(errors=['failed']))
    def test_duplicate_module_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'].__setitem__(6,copy.deepcopy(r['modules'][0])))
    def test_missing_module_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'].pop())
    def test_full_scope_token_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][2].update(assembly=contracts.MSCORLIB.replace('b77a5c561934e089','7cec85d7bea7798e')))
    def test_module_hash_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][2].update(sha256='0'*64))
    def test_module_mvid_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][2].update(mvid='0'*36))
    def test_declared_player_scope_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][4].update(reference_scopes=[contracts.MSCORLIB]))
    def test_duplicate_declared_player_scope_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][4].update(reference_scopes=[contracts.NETSTANDARD]*2))
    def test_player_module_contract_definition_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][4].update(defined_types=[{}]))
    def test_extra_forwarder_rejected(self):self.reject(lambda r,m,b,reader,l:r['modules'][1]['forwarders'].append({'name':'System.Decimal'}))
    def test_native_jit_path_does_not_become_aot(self):self.reject(lambda r,m,b,reader,l:m['native_query'].update(mono_runtime_lib_directory='/Wrong/unityaot-macos'))


class RuntimeContractPublicationTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=os.environ.get('LUCID_TEST_TMP_ROOT'));self.root=Path(self.temp.name).resolve()/'project with spaces';self.root.mkdir();self.work=self.root/'.cache/project-lucid';self.work.mkdir(parents=True)
        (self.root/'ProjectSettings').mkdir();(self.root/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.54f1\n')
        self.editor=self.work/'editor';self.editor.write_bytes(b'editor');self.latest=self.work/'runtime-contracts/latest-macos-zone-theme-v1.json';self.latest.parent.mkdir();self.latest.write_bytes(b'previous-authoritative-receipt')
        self.before=self.latest.read_bytes();self.source='b'*64;self.report,self.manifest,self.mraw,self.reader,self.layouts=fixture()
        self.layouts.update(original_path=str(self.work/'original.json'),loaded_path=str(self.work/'loaded.json'),original_raw=b'original',loaded_raw=b'loaded',original_sha256=self.manifest['original_schema_sha256'],loaded_sha256='e'*64,file_seals=[])
        Path(self.layouts['original_path']).write_bytes(self.layouts['original_raw']);Path(self.layouts['loaded_path']).write_bytes(self.layouts['loaded_raw'])
        self.player={'receipt_sha256':'c'*64,'receipt_path':str(self.work/'player.json'),'receipt':{'identity_nonce':'1'*32},'staged':{'native_profile_queries_before':self.manifest['native_query']},'receipt_raw':b'player','staged_raw':b'pending','context_raw':b'context'}
        self.schema={'sha256':'d'*64,'path':str(self.work/'schema.json'),'raw':b'schema','manifest_raw':b'manifest','staged_raw':b'pending'}
        self.change=None;self.calls=0;self.sources=[];self.initial_input=self.work/'sentinel-input';self.initial_input.write_bytes(b'untouched')
        patches=[mock.patch.object(bootstrap,'REPO_ROOT',self.root),mock.patch.object(bootstrap,'CACHE_ROOT',self.work),
          mock.patch.object(rc,'find_editor',return_value=self.editor),mock.patch.object(rc,'artifact_fingerprint',side_effect=self.fingerprint),
          mock.patch.object(rc.playercode,'_engine_identity',return_value=self.manifest['engine']),mock.patch.object(rc.playerschema,'_player_evidence',return_value=self.player),
          mock.patch.object(rc,'_schema_evidence',return_value=self.schema),mock.patch.object(rc,'_layout_evidence',return_value=self.layouts),
          mock.patch.object(rc,'_contract_modules',return_value=self.manifest['modules']),mock.patch.object(rc,'_build_reader',side_effect=self.build),
          mock.patch.object(rc,'_report_check',return_value=[{'name':n} for n in contracts.CONTRACTS]),
          mock.patch.object(rc.playerschema,'_same_file'),mock.patch.object(rc.playerschema,'_tree_records',return_value=[]),
          mock.patch.object(rc.subprocess,'run',side_effect=self.process)]
        for patch in patches:patch.start();self.addCleanup(patch.stop)
        self.addCleanup(self.temp.cleanup)
    def fingerprint(self,root):
        self.calls+=1
        return 'a'*64 if self.change=='source' and self.calls>=4 else self.source
    def build(self,root,work,run,engine):
        path=run/'reader/ProjectLucid.ThemeContracts.dll';path.parent.mkdir();path.write_bytes(b'reader')
        return {'binary':{'path':str(path)},'host':{'path':'mock-host'},'sources':[],'output':[],'env':{},'log':str(run/'build.log')}
    def process(self,command,**kwargs):
        if self.change=='interruption':raise subprocess.TimeoutExpired(command,1)
        if self.change=='compilerfailure':return subprocess.CompletedProcess(command,1)
        pending=Path(command[-1]);current=json.loads(Path(command[-2]).read_bytes());report=copy.deepcopy(self.report)
        for k in ('target','nonce','source_fingerprint','project_root','work_root','run','player_receipt_sha256','player_schema_sha256','original_schema_sha256','loaded_inventory_sha256'):report[k]=current[k]
        report['manifest_sha256']=hashlib.sha256(Path(command[-2]).read_bytes()).hexdigest();pending.write_text(json.dumps(report))
        if self.change=='layout':Path(self.layouts['loaded_path']).write_bytes(b'changed')
        return subprocess.CompletedProcess(command,0)
    def invoke(self):return rc.run_runtime_contracts(self.root,self.work,'macos',self.layouts['original_path'],self.layouts['loaded_path'])
    def retained(self,kind):
        self.change=kind
        with self.assertRaises((ValueError,subprocess.SubprocessError)):self.invoke()
        self.assertEqual(self.latest.read_bytes(),self.before);self.assertEqual(self.initial_input.read_bytes(),b'untouched')
        self.assertFalse((self.work/'locks/runtime-contracts.lock').exists())
    def test_success_publishes_only_metadata_and_false_approvals(self):
        result=self.invoke();self.assertEqual(result['status'],'metadata-ready');receipt=json.loads(self.latest.read_bytes());self.assertEqual(receipt['identity_status'],'complete')
        self.assertEqual(receipt['generated_by'],'ProjectLucid.run_runtime_contracts');self.assertFalse(result['remap_approved']);self.assertFalse(result['runtime_verified'])
        self.assertTrue(all(receipt[k] is False for k in rc.FLAGS))
        self.assertEqual(self.initial_input.read_bytes(),b'untouched')
    def test_interrupted_reader_retains_previous_latest(self):self.retained('interruption')
    def test_reader_failure_retains_previous_latest(self):self.retained('compilerfailure')
    def test_source_change_after_native_rechecks_retains_latest(self):self.retained('source')
    def test_loaded_inventory_change_retains_latest(self):self.retained('layout')
    def test_stale_compiler_never_creates_run(self):
        with mock.patch.object(rc.playerschema,'_player_evidence',side_effect=ValueError('stale')):
            with self.assertRaises(ValueError):self.invoke()
        self.assertEqual(self.latest.read_bytes(),self.before);self.assertFalse((self.work/'runtime-contracts/runs').exists())
    def test_existing_nonce_run_retained(self):
        run=self.work/'runtime-contracts/runs'/('f'*32);run.mkdir(parents=True);(run/'sentinel').write_bytes(b'kept')
        with mock.patch.object(rc.uuid,'uuid4',return_value=type('Nonce',(),{'hex':'f'*32})()):
            with self.assertRaises(FileExistsError):self.invoke()
        self.assertEqual((run/'sentinel').read_bytes(),b'kept');self.assertEqual(self.latest.read_bytes(),self.before)
    def test_symlink_latest_rejected(self):
        self.latest.unlink();self.latest.symlink_to(self.initial_input)
        with self.assertRaises(ValueError):self.invoke()
        self.assertEqual(self.initial_input.read_bytes(),b'untouched')
    def test_existing_lock_does_not_remove_foreign_lock(self):
        lock=self.work/'locks/runtime-contracts.lock';lock.mkdir(parents=True)
        with self.assertRaises(ValueError):self.invoke()
        self.assertTrue(lock.is_dir());self.assertEqual(self.latest.read_bytes(),self.before)


class RuntimeContractConsumerTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=os.environ.get('LUCID_TEST_TMP_ROOT'));self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve()/'project with spaces';self.root.mkdir();self.work=self.root/'.cache/project-lucid';self.work.mkdir(parents=True)
        staged,manifest,_,reader,layouts=fixture();self.layouts=layouts;self.source=manifest['source_fingerprint']
        self.player={'receipt_sha256':manifest['player_receipt_sha256'],'receipt_path':str(self.work/'player.json'),
          'receipt':{'identity_nonce':'1'*32},'staged':{'native_profile_queries_before':manifest['native_query']}}
        self.schema={'sha256':manifest['player_schema_sha256'],'path':str(self.work/'schema.json')}
        self.layouts.update(original_path=str(self.work/'original.json'),loaded_path=str(self.work/'loaded.json'),
          original_sha256=manifest['original_schema_sha256'],loaded_sha256=manifest['loaded_inventory_sha256'])
        self.run=self.work/'runtime-contracts/runs'/manifest['nonce'];self.run.mkdir(parents=True)
        manifest.update(project_root=str(self.root),work_root=str(self.work),run=str(self.run),compiler_nonce='1'*32)
        self.modules=copy.deepcopy(manifest['modules']);self.engine=copy.deepcopy(manifest['engine']);self.manifest=manifest
        self.context=self.run/'manifest.json';self.pending=self.run/'pending.json';self.latest=self.work/'runtime-contracts/latest-macos-zone-theme-v1.json'
        self.reader={'binary':{'path':str(self.run/'reader/ProjectLucid.ThemeContracts.dll')}}
        for k in ('project_root','work_root','run'):staged[k]=manifest[k]
        staged['reader_path']=str(self.run/'reader/Unity.Cecil.dll');self.staged=staged
        for patch in [mock.patch.object(bootstrap,'REPO_ROOT',self.root),mock.patch.object(bootstrap,'CACHE_ROOT',self.work),
          mock.patch.object(rc,'_contract_modules',return_value=self.modules),mock.patch.object(rc,'_reader_check',return_value=self.reader),
          mock.patch.object(rc,'_attest_loaded',return_value={'fixture':'synthetic-only'})]:
            patch.start();self.addCleanup(patch.stop)
        self.write()
    def write(self):
        self.context.write_text(json.dumps(self.manifest,sort_keys=True));mraw=self.context.read_bytes()
        self.staged['manifest_sha256']=hashlib.sha256(mraw).hexdigest();self.pending.write_text(json.dumps(self.staged,sort_keys=True))
        self.report=copy.deepcopy(self.staged);self.report.update(status='metadata-ready',identity_status='complete',
          generated_by='ProjectLucid.run_runtime_contracts',identity_verified_by='native-python-prepost-player-schema-module-cecil-v1',
          contract_rows=rc._report_check(self.staged,self.manifest,mraw,self.reader,self.layouts),
          staged_report_path=str(self.pending),staged_report_sha256=hashlib.sha256(self.pending.read_bytes()).hexdigest(),
          player_receipt_path=self.player['receipt_path'],player_schema_path=self.schema['path'],original_schema_path=self.layouts['original_path'],
          loaded_inventory_path=self.layouts['loaded_path'],reader_build=self.reader,log=str(self.run/'reader.log'))
        self.latest.write_text(json.dumps(self.report,sort_keys=True))
    def invoke(self):return rc.verify_runtime_contract_receipt(self.root,self.work,'macos',self.source,self.engine,self.player,self.schema,self.layouts)
    def reject(self,change):
        before_pending=self.pending.read_bytes();before_context=self.context.read_bytes();change();self.latest.write_text(json.dumps(self.report))
        with self.assertRaises((ValueError,KeyError,TypeError)):self.invoke()
        self.assertEqual(self.pending.read_bytes(),before_pending);self.assertEqual(self.context.read_bytes(),before_context)
    def test_authoritative_current_receipt_consumes_exact_twenty_rows(self):self.assertEqual(len(self.invoke()['contract_rows']),20)
    def test_latest_unknown_rules_approval_rejected(self):self.reject(lambda:self.report.update(rules_modified=True))
    def test_latest_unknown_false_owner_flag_rejected(self):self.reject(lambda:self.report.update(owners_verified=False))
    def test_latest_log_path_substitution_rejected(self):self.reject(lambda:self.report.update(log='/outside/reader.log'))
    def test_pending_status_is_never_authoritative(self):self.reject(lambda:self.report.update(status='pending-native-verification'))
    def test_schema_version_bool_is_rejected(self):self.reject(lambda:self.report.update(schema_version=True))
    def test_stale_source_is_rejected(self):self.reject(lambda:self.report.update(source_fingerprint='0'*64))
    def test_player_receipt_substitution_is_rejected(self):self.reject(lambda:self.report.update(player_receipt_sha256='0'*64))
    def test_loaded_inventory_substitution_is_rejected(self):self.reject(lambda:self.report.update(loaded_inventory_sha256='0'*64))
    def test_nonce_path_traversal_is_rejected(self):self.reject(lambda:self.report.update(nonce='../outside'))
    def test_staged_hash_substitution_is_rejected(self):self.reject(lambda:self.report.update(staged_report_sha256='0'*64))
    def test_false_implicit_resolution_claim_is_required(self):self.reject(lambda:self.report.update(implicit_dependency_resolution=True))
    def test_report_cannot_change_contract_rows(self):self.reject(lambda:self.report['contract_rows'][0].update(forwarder_token='0x2700ffff'))
    def test_published_report_cannot_change_staged_module_graph(self):self.reject(lambda:self.report['modules'][2].update(defined_types=[]))
    def test_reader_scope_substitution_is_rejected(self):self.reject(lambda:self.report.update(reader_path='/Wrong/Unity.Cecil.dll'))
    def test_context_bytes_change_is_rejected(self):
        self.context.write_text(self.context.read_text()+' ')
        with self.assertRaises(ValueError):self.invoke()
    def test_duplicate_latest_keys_rejected(self):
        self.latest.write_text('{"schema_version":1,'+self.latest.read_text()[1:])
        with self.assertRaises(ValueError):self.invoke()
    def test_symlink_latest_is_rejected(self):
        prior=self.latest.read_bytes();actual=self.work/'actual.json';actual.write_bytes(prior);self.latest.unlink();self.latest.symlink_to(actual)
        with self.assertRaises(ValueError):self.invoke()
        self.assertEqual(actual.read_bytes(),prior)
    def test_rewritten_manifest_compiler_nonce_is_rejected(self):
        self.manifest['compiler_nonce']='f'*32;self.write()
        with self.assertRaises(ValueError):self.invoke()
    def test_manifest_bool_cannot_compare_equal_to_integer(self):
        self.manifest['schema_version']=True;self.write()
        with self.assertRaises(ValueError):self.invoke()
    def test_contract_row_bool_cannot_compare_equal_to_integer(self):
        row=next(r for r in self.report['contract_rows'] if r['enum_constants_verified']);row['enum_constants_verified']=1
        self.latest.write_text(json.dumps(self.report))
        with self.assertRaises(ValueError):self.invoke()
    def test_reader_current_sources_are_rechecked(self):
        with mock.patch.object(rc,'_reader_check',side_effect=ValueError('current source changed')):
            with self.assertRaises(ValueError):self.invoke()


class RuntimeContractMergeTests(unittest.TestCase):
    def setUp(self):
        self.source='b'*64;self.evidence={}
        for target in rc.playercode.TARGETS:
            modules=[{'path':'/reference/netstandard.dll','sha256':'a'*64}]
            modules.extend({'path':'/'+target+'/'+name,'sha256':'a'*64} for name in ('facade/netstandard.dll','mscorlib.dll','System.dll','Game.Runtime.dll','HLUnityCore.Runtime.dll','Unity.Addressables.dll'))
            report={'source_fingerprint':self.source,'modules':modules};report.update({k:False for k in rc.FLAGS})
            self.evidence[target]={'report':report,'native_query':{'target':target}}
    def test_exact_three_profile_nineteen_path_closure(self):self.assertEqual(len(rc._merge_contract_metadata(self.evidence,self.source)['metadata']['modules']),19)
    def test_missing_target_rejected(self):
        del self.evidence['linux']
        with self.assertRaises(ValueError):rc._merge_contract_metadata(self.evidence,self.source)
    def test_equal_paths_with_different_metadata_rejected(self):
        self.evidence['linux']['report']['modules'][0]['sha256']='0'*64
        with self.assertRaises(ValueError):rc._merge_contract_metadata(self.evidence,self.source)
    def test_target_paths_cannot_be_collapsed_by_equal_assembly_identity(self):
        self.evidence['linux']['report']['modules'][1]=copy.deepcopy(self.evidence['macos']['report']['modules'][1])
        with self.assertRaises(ValueError):rc._merge_contract_metadata(self.evidence,self.source)
    def test_bool_false_and_current_source_are_mandatory(self):
        self.evidence['linux']['report']['runtime_verified']=0
        with self.assertRaises(ValueError):rc._merge_contract_metadata(self.evidence,self.source)
    def test_shared_metadata_deep_boolean_identity_is_preserved(self):
        self.evidence['macos']['report']['modules'][0]['marker']=1
        self.evidence['windows']['report']['modules'][0]['marker']=True
        self.evidence['linux']['report']['modules'][0]['marker']=1
        with self.assertRaises(ValueError):rc._merge_contract_metadata(self.evidence,self.source)


class RuntimeThemeOuterComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=os.environ.get('LUCID_TEST_TMP_ROOT'));self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve();self.source='b'*64
        package=self.root/'Library/PackageCache/com.unity.addressables@1.22.3/Runtime/AssetReference.cs';package.parent.mkdir(parents=True);package.write_bytes(b'guard fixture only')
        self.proof={'source_fingerprint':self.source,'players':{},'contracts':{},'receipts':{},
          'layouts':{'original_raw':b'{}','loaded_raw':b'{}','original_sha256':rc.themebindings.ORIGINAL_SCHEMA,'file_seals':[]}}
        pairs=[('original','editor')]+[pair for t in rc.themebindings.TARGETS for pair in (('original',t+'-player'),(t+'-player','editor'))]
        self.result={'issues':[{'reason':'runtime_contract_wrapper_freshness_not_integrated'}],
          'pairs':[{'left_plane':a,'right_plane':b,'issues':[],'checked_types':[list(k) for k in sorted(rc.themebindings.PROFILE_TYPES)]} for a,b in pairs],
          'target_contract_rows':{t:[{}]*20 for t in rc.themebindings.TARGETS},'references_modified':False,
          'player_schema_verified':False,'remap_approved':False,'gameplay_verified':False}
        for patch in [mock.patch.object(rc,'collect_theme_contract_evidence',return_value=self.proof),
          mock.patch.object(rc.themebindings,'compare_theme_planes',side_effect=lambda *a,**k:copy.deepcopy(self.result)),
          mock.patch.object(rc,'artifact_fingerprint',return_value=self.source)]:patch.start();self.addCleanup(patch.stop)
    def invoke(self):return rc.compare_verified_theme_planes(self.root,self.root/'cache','original','loaded')
    def test_only_outer_freshness_blocker_removed_and_approvals_stay_false(self):
        result=self.invoke();self.assertEqual(result['status'],'evidence-ready-for-root-review')
        for key in ('owners_verified','rules_modified','runtime_verified','references_modified','player_schema_verified','remap_approved','gameplay_verified'):self.assertIs(result[key],False)
    def test_missing_genuine_receipt_never_calls_pure_comparison(self):
        with mock.patch.object(rc,'collect_theme_contract_evidence',side_effect=ValueError('pending-only')):
            with self.assertRaises(ValueError):self.invoke()
    def test_any_other_issue_retains_blocker(self):
        self.result['issues'].append({'reason':'target_runtime_contract_projection_unverified'})
        with self.assertRaises(ValueError):self.invoke()
    def test_pair_missing_exact_type_closure_rejected(self):
        self.result['pairs'][0]['checked_types'].pop()
        with self.assertRaises(ValueError):self.invoke()
    def test_pair_duplicate_routes_rejected(self):
        self.result['pairs'][1]=copy.deepcopy(self.result['pairs'][0])
        with self.assertRaises(ValueError):self.invoke()
    def test_pair_structural_mismatch_rejected(self):
        self.result['pairs'][0]['issues']=[{'reason':'field_type_mismatch'}]
        with self.assertRaises(ValueError):self.invoke()
    def test_missing_target_contract_rejected(self):
        self.result['target_contract_rows']['linux'].pop()
        with self.assertRaises(ValueError):self.invoke()
    def test_source_change_after_comparison_rejected(self):
        with mock.patch.object(rc,'artifact_fingerprint',return_value='0'*64):
            with self.assertRaises(ValueError):self.invoke()
    def test_receipt_changes_during_comparison_rejected(self):
        after=copy.deepcopy(self.proof);after['receipts']={'macos':{'sha256':'0'*64}}
        with mock.patch.object(rc,'collect_theme_contract_evidence',side_effect=[self.proof,after]):
            with self.assertRaises(ValueError):self.invoke()
    def test_loaded_inventory_changes_during_comparison_rejected(self):
        after=copy.deepcopy(self.proof);after['layouts']['loaded_raw']=b'{"changed":true}'
        with mock.patch.object(rc,'collect_theme_contract_evidence',side_effect=[self.proof,after]):
            with self.assertRaises(ValueError):self.invoke()


class OriginalSchemaIOTests(unittest.TestCase):
    def setUp(self):
        # Synthetic small input files isolate actual IO, not original authority.
        self.temp = tempfile.TemporaryDirectory(dir=os.environ.get('LUCID_TEST_TMP_ROOT'))
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.binary, self.metadata = self.root / 'code.bin', self.root / 'metadata.dat'
        self.binary.write_bytes(b'synthetic code input')
        self.metadata.write_bytes(b'synthetic metadata input')
        self.inputs = {'native_binary': hashlib.sha256(self.binary.read_bytes()).hexdigest(),
                       'metadata': hashlib.sha256(self.metadata.read_bytes()).hexdigest()}
        self.original = {
            'schema_version': 1, 'status': 'ready', 'unity_version': '2022.3.54f1',
            'native_binary': str(self.binary), 'metadata': str(self.metadata),
            'output_dir': str(self.root / 'old-output'),
            'native_binary_sha256': self.inputs['native_binary'],
            'metadata_sha256': self.inputs['metadata'], 'errors': [], 'assemblies': [],
            'metadata_interpreted_version': 31.1, 'native_addresses_verified': False,
        }
        self.pin = rc.themebindings.original_schema_content_sha256(self.original)
        for patch in (mock.patch.object(rc.themebindings, 'ORIGINAL_SCHEMA_CONTENT', self.pin),
                      mock.patch.object(rc.themebindings, 'ORIGINAL_INPUT_SHA256', self.inputs)):
            patch.start(); self.addCleanup(patch.stop)
        self.path = self.root / 'original.json'
        self.write(self.original)

    def write(self, value):
        self.path.write_text(json.dumps(value, indent=2) + '\n')

    def test_actual_raw_identity_and_independent_input_hashes_preserved(self):
        evidence = rc._original_schema_evidence(self.path)
        self.assertEqual(evidence['original_raw'], self.path.read_bytes())
        self.assertEqual(evidence['original_sha256'], hashlib.sha256(self.path.read_bytes()).hexdigest())
        self.assertNotEqual(evidence['original_sha256'], self.pin)
        self.assertEqual(evidence['original_schema_content_sha256'], self.pin)
        self.assertEqual([row['sha256'] for row in evidence['input_seals']], list(self.inputs.values()))

    def test_relocated_same_content_inputs_accept_with_new_raw_report(self):
        before = rc._original_schema_evidence(self.path)
        moved_code, moved_metadata = self.root / 'other-code.bin', self.root / 'other-metadata.dat'
        moved_code.write_bytes(self.binary.read_bytes())
        moved_metadata.write_bytes(self.metadata.read_bytes())
        changed = copy.deepcopy(self.original)
        changed.update(native_binary=str(moved_code), metadata=str(moved_metadata),
                       output_dir=str(self.root / 'new-output'))
        self.write(changed)
        after = rc._original_schema_evidence(self.path)
        self.assertEqual(after['original_schema_content_sha256'], before['original_schema_content_sha256'])
        self.assertNotEqual(after['original_sha256'], before['original_sha256'])
        self.assertEqual(after['input_seals'][0]['path'], str(moved_code))

    def test_binary_and_metadata_mutation_reject_independently(self):
        for path in (self.binary, self.metadata):
            with self.subTest(path=path):
                before = path.read_bytes()
                path.write_bytes(b'changed input')
                try:
                    with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)
                finally:
                    path.write_bytes(before)

    def test_supplied_hash_cannot_redefine_reviewed_input(self):
        self.binary.write_bytes(b'self-consistent wrong input')
        changed = copy.deepcopy(self.original)
        changed['native_binary_sha256'] = hashlib.sha256(self.binary.read_bytes()).hexdigest()
        self.write(changed)
        # Isolate the independent input pin even if a test accepts this graph.
        changed_pin = rc.themebindings.original_schema_content_sha256(changed)
        with mock.patch.object(rc.themebindings, 'ORIGINAL_SCHEMA_CONTENT', changed_pin):
            with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)

    def test_missing_input_file_rejects(self):
        self.binary.unlink()
        with self.assertRaises(OSError): rc._original_schema_evidence(self.path)

    def test_symlink_input_rejects(self):
        link = self.root / 'linked.bin'
        link.symlink_to(self.binary)
        changed = copy.deepcopy(self.original); changed['native_binary'] = str(link)
        self.write(changed)
        with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)

    def test_raw_duplicate_top_level_and_nested_keys_rejected(self):
        raw = self.path.read_text()
        for key in ('schema_version', 'assemblies'):
            value = '1' if key == 'schema_version' else '[{"name":"A","name":"B"}]'
            changed = raw[:-2] + ',"' + key + '":' + value + '}\n'
            self.path.write_text(changed)
            with self.subTest(key=key):
                with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)
        nested = copy.deepcopy(self.original)
        nested['assemblies'] = [{'name': 'A'}]
        self.path.write_text(json.dumps(nested).replace('"name": "A"', '"name": "A", "name": "B"'))
        with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)

    def test_raw_nonfinite_numbers_rejected(self):
        raw = json.dumps(self.original)
        for value in ('NaN', 'Infinity', '-Infinity', '1e9999'):
            self.path.write_text(raw.replace('31.1', value))
            with self.subTest(value=value):
                with self.assertRaises(ValueError): rc._original_schema_evidence(self.path)

    def test_stale_raw_loaded_binding_rejected_even_with_same_content(self):
        old_raw_sha = hashlib.sha256(self.path.read_bytes()).hexdigest()
        changed = copy.deepcopy(self.original); changed['output_dir'] = '/new/nonce'
        self.write(changed)
        loaded = self.root / 'loaded.json'
        loaded.write_text(json.dumps({'status': 'ready', 'unity_version': '2022.3.54f1',
            'original_schema_sha256': old_raw_sha, 'source_fingerprint': 'a' * 64,
            'errors': [], 'references_modified': False}))
        with mock.patch.object(rc, '_subset', side_effect=AssertionError('must not reach metadata')):
            with self.assertRaises(ValueError):
                rc._layout_evidence(self.root, self.path, loaded, 'a' * 64)

    def test_current_raw_loaded_binding_reaches_metadata_context_checks(self):
        raw_sha = hashlib.sha256(self.path.read_bytes()).hexdigest()
        loaded = self.root / 'loaded.json'
        loaded.write_text(json.dumps({'status': 'ready', 'unity_version': '2022.3.54f1',
            'original_schema_sha256': raw_sha, 'source_fingerprint': 'a' * 64,
            'errors': [], 'references_modified': False}))
        with mock.patch.object(rc, '_subset', side_effect=AssertionError('metadata context phase')):
            with self.assertRaisesRegex(AssertionError, 'metadata context phase'):
                rc._layout_evidence(self.root, self.path, loaded, 'a' * 64)


class RuntimeContractReaderBuildTests(unittest.TestCase):
    """Hash actual temporary bytes, with explicit synthetic PE identity for guards."""
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=os.environ.get('LUCID_TEST_TMP_ROOT'))
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.work = self.root / '.cache/project-lucid'
        self.run = self.work / 'runtime-contracts/runs' / ('1' * 32)
        self.binary_path = self.run / 'reader/ProjectLucid.ThemeContracts.dll'
        self.binary_path.parent.mkdir(parents=True)
        self.binary_path.write_bytes(b'explicit synthetic metadata bytes, never acceptance')
        self.host = self.work / 'tools/dotnet/dotnet'
        self.host.parent.mkdir(parents=True); self.host.write_bytes(b'not invoked')
        self.sources = [self.root/'tools/theme-contracts/ThemeContracts.csproj',
                        self.root/'tools/theme-contracts/Program.cs',self.root/'tools/theme-contracts/LoadedDeclarations.cs']
        for source in self.sources:
            source.parent.mkdir(parents=True, exist_ok=True); source.write_bytes(b'guard fixture only')
        self.module = dict(rc.playercode._file_record(self.binary_path),
            assembly_name='ProjectLucid.ThemeContracts', mvid='11111111-1111-1111-1111-111111111111')
        self.output = [{'relative_path': self.binary_path.name, **rc.playercode._file_record(self.binary_path)}]
        self.reader = {
            'host': rc.playercode._file_record(self.host), 'sdk_version': '10.0.401',
            'sdk_archive_sha512': 'a' * 128,
            'sources': [rc.playercode._file_record(source) for source in self.sources],
            'binary': copy.deepcopy(self.module), 'output': copy.deepcopy(self.output),
            'log': str(self.run/'build.log'),
        }
        for patch in [mock.patch.object(bootstrap, 'REPO_ROOT', self.root),
                      mock.patch.object(bootstrap, 'CACHE_ROOT', self.work),
                      mock.patch.object(rc, 'load_lock', return_value={'artifacts': {'dotnet': {
                          'version': '10.0.401', 'sha512': 'a' * 128}}}),
                      mock.patch.object(rc, '_seal', return_value=self.module),
                      mock.patch.object(rc.playerschema, '_tree_records', return_value=self.output)]:
            patch.start(); self.addCleanup(patch.stop)

    def invoke(self):
        return rc._reader_check(self.root, self.work, self.run, self.reader)

    def test_exact_current_build_seals_consumed(self):
        self.assertEqual(self.invoke(), self.reader)

    def test_unknown_build_approval_rejected(self):
        self.reader['owners_verified'] = True
        with self.assertRaises(ValueError): self.invoke()

    def test_compiled_module_float_size_rejected(self):
        self.reader['binary']['size'] = float(self.reader['binary']['size'])
        with self.assertRaises(ValueError): self.invoke()

    def test_host_float_size_rejected(self):
        self.reader['host']['size'] = float(self.reader['host']['size'])
        with self.assertRaises(ValueError): self.invoke()

    def test_current_reader_source_change_rejected(self):
        self.sources[1].write_bytes(b'changed actual temporary bytes')
        with self.assertRaises(ValueError): self.invoke()

    def test_missing_build_provenance_rejected(self):
        del self.reader['sdk_archive_sha512']
        with self.assertRaises(ValueError): self.invoke()


if __name__=='__main__':unittest.main()


class RuntimeLoadedAttestationInvocationTests(unittest.TestCase):
    def test_failure_is_not_a_boolean_claim(self):
        reader={'host':{'path':'host'},'binary':{'path':'reader'},'env':{'guard':'fixture'}}
        manifest={'run':'/owned/run'}
        with mock.patch.object(rc.subprocess,'run',return_value=subprocess.CompletedProcess([],1,b'',b'actual TypeDef token/name mismatch')):
            with self.assertRaisesRegex(ValueError,'TypeDef token/name mismatch'):rc._attest_loaded(reader,manifest)
    def test_nonobject_native_output_rejected(self):
        reader={'host':{'path':'host'},'binary':{'path':'reader'},'env':{'guard':'fixture'}}
        with mock.patch.object(rc.subprocess,'run',return_value=subprocess.CompletedProcess([],0,b'[]',b'')):
            with self.assertRaises(ValueError):rc._attest_loaded(reader,{'run':'/owned/run'})
    def test_invocation_is_readonly_mode_with_owned_manifest(self):
        reader={'host':{'path':'host'},'binary':{'path':'reader'},'env':{'guard':'fixture'}}
        with mock.patch.object(rc.subprocess,'run',return_value=subprocess.CompletedProcess([],0,b'{"fixture":"synthetic-only"}',b'')) as run:
            self.assertEqual(rc._attest_loaded(reader,{'run':'/owned/run'}),{'fixture':'synthetic-only'})
            self.assertEqual(run.call_args.args[0],['host','reader','--attest-loaded','/owned/run/manifest.json'])
