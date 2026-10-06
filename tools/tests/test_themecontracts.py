import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from lucidlib import bindings, themecontracts as contract


def fixture(target='macos'):
    suffix,facade,core,system=contract.TARGET_PINS[target]
    contents='/Installed/Editor/Unity.app/Contents'
    directory=contents+'/MonoBleedingEdge/lib/mono/unityjit-'+suffix
    query={'target':contract.TARGET_NAMES[target],'unity_version':'2022.3.54f1',
      'api_compatibility_value':6,'api_compatibility_name':'NET_Standard_2_0',
      'scripting_backend_value':0,'scripting_backend_name':'Mono2x',
      'platform_profile_suffix':suffix,'compatibility_profile_folder':'unityaot-'+suffix,
      'runtime_selection_verified':False,'layout_approved':False,'gameplay_verified':False,
      'runtime_assemblies_loaded':False,'build_player_called':False,
      'module_sha256':'fb89c4764167359e3796b9a7e893f507d508f86eb8ad7daa55c78bf07abcd0b9',
      'module_mvid':'70a991b8-3ee9-4c31-8ecd-89fa65e3f64c',
      'module_assembly':'UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null',
      'module_path':contents+'/Managed/UnityEngine/UnityEditor.CoreModule.dll',
      'mono_runtime_lib_directory':directory,'methods':copy.deepcopy(contract.QUERY_METHODS)}
    def module(path,pin,identity):
        return {'path':path,'sha256':pin[0],'mvid':pin[1],'assembly':identity,'defined_types':[],'forwarders':[]}
    reference=module(contents+'/NetStandard/ref/2.1.0/netstandard.dll',contract.REFERENCE_PIN,contract.NETSTANDARD)
    forwarder=module(directory+'/Facades/netstandard.dll',facade,contract.NETSTANDARD)
    endpoints={contract.MSCORLIB:module(directory+'/mscorlib.dll',core,contract.MSCORLIB),
               contract.SYSTEM:module(directory+'/System.dll',system,contract.SYSTEM)}
    original={'assemblies':[{'name':a,'types':[]} for a in ('mscorlib','System')]}
    for index,(name,scope) in enumerate(sorted(contract.CONTRACTS.items())):
        t={'name':name,'token':'0x02'+format(index+1,'06x'),'attributes':257,'is_enum':name in contract.ENUMS,
           'base_type':{},'fields':[]}
        r={'assembly':scope.split(',')[0],'full_name':name,'is_enum':True,'fields':[]}
        if name in contract.ENUMS:
            t['fields']=[{'name':'value__','attributes':1542,'has_constant':False,'type':{'full_name':'System.Int32'}}]
            r['fields']=[{'name':'value__','attributes':1542,'has_default_value':False,'field_type':{'reflection_full_name':'System.Int32'}}]
            for n,v in contract.ENUMS[name].items():
                t['fields'].append({'name':n,'attributes':32854,'has_constant':True,'constant':v})
                r['fields'].append({'name':n,'attributes':32854,'has_default_value':True,'default_value':v,'default_value_complete':True})
            next(a for a in original['assemblies'] if a['name']==r['assembly'])['types'].append(r)
        reference['defined_types'].append(copy.deepcopy(t));endpoints[scope]['defined_types'].append(copy.deepcopy(t))
        forwarder['forwarders'].append({'name':name,'token':'0x27'+format(index+1,'06x'),
            'scope':scope,'is_forwarder':True,'attributes':0x200000,'declaring_type':None})
    metadata={'reader_sha256':contract.CECIL,'reader_mvid':contract.CECIL_MVID,
        'player_clr_loads':False,'editor_invoked':False,'runtime_verified':False,'layout_approved':False,
        'modules':[reference,forwarder,*endpoints.values()]}
    return metadata,query,original,copy.deepcopy(original)


class ThemeContractGraphTests(unittest.TestCase):
    def check(self,m,q,o,e,target='macos'):
        return contract.graph_projection(m,q,target,o,e)
    def rejected(self,alter,target='macos'):
        m,q,o,e=fixture(target);alter(m,q,o,e)
        with self.assertRaises((bindings.LayoutError,KeyError,TypeError,ValueError)):self.check(m,q,o,e,target)
    def test_all_targets_have_twenty_distinct_forwarder_rows(self):
        for target in contract.TARGET_PINS:
            m,q,o,e=fixture(target);p=self.check(m,q,o,e,target)
            self.assertEqual(set(p.rows),set(contract.CONTRACTS));self.assertEqual(p.target,target)
            for n,r in p.rows.items():
                self.assertEqual(r['forwarder_target_scope'],contract.CONTRACTS[n]);self.assertIn('unityjit-',r['endpoint_path'])
    def test_wrong_runtime_directory_never_falls_back_to_aot(self):
        self.rejected(lambda m,q,o,e:q.update(mono_runtime_lib_directory=q['mono_runtime_lib_directory'].replace('unityjit-','unityaot-')))
    def test_wrong_compatibility_folder_rejected(self):self.rejected(lambda m,q,o,e:q.update(compatibility_profile_folder='unityjit-macos'))
    def test_wrong_target_query_rejected(self):self.rejected(lambda m,q,o,e:q.update(target='StandaloneWindows64'))
    def test_alias_for_actual_enum_name_rejected(self):self.rejected(lambda m,q,o,e:q.update(api_compatibility_name='NET_Standard'))
    def test_bool_for_enum_integer_rejected(self):self.rejected(lambda m,q,o,e:q.update(scripting_backend_value=False))
    def test_runtime_approval_flag_rejected(self):self.rejected(lambda m,q,o,e:q.update(runtime_selection_verified=True))
    def test_missing_native_method_rejected(self):self.rejected(lambda m,q,o,e:q['methods'].pop())
    def test_changed_native_method_token_rejected(self):self.rejected(lambda m,q,o,e:q['methods'][0].update(token='0x06000001'))
    def test_changed_loaded_engine_mvid_rejected(self):self.rejected(lambda m,q,o,e:q.update(module_mvid='0'*36))
    def test_same_assembly_identity_different_file_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2].update(sha256='a'*64))
    def test_cross_target_mvid_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2].update(mvid=contract.TARGET_PINS['windows'][2][1]))
    def test_wrong_endpoint_public_key_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2].update(assembly=contract.MSCORLIB.replace('b77a5c561934e089','7cec85d7bea7798e')))
    def test_changed_reader_rejected(self):self.rejected(lambda m,q,o,e:m.update(reader_sha256='b'*64))
    def test_player_clr_load_flag_rejected(self):self.rejected(lambda m,q,o,e:m.update(player_clr_loads=True))
    def test_duplicate_module_path_rejected(self):self.rejected(lambda m,q,o,e:m['modules'].append(copy.deepcopy(m['modules'][1])))
    def test_missing_forwarder_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'].pop())
    def test_duplicate_forwarder_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'].append(copy.deepcopy(m['modules'][1]['forwarders'][0])))
    def test_additional_contract_rejected(self):
        self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'].append({'name':'System.TimeSpan'}))
    def test_type_name_cannot_select_wrong_endpoint(self):self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'][0].update(scope=contract.SYSTEM))
    def test_nonforwarder_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'][0].update(is_forwarder=False))
    def test_nested_forwarder_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][1]['forwarders'][0].update(declaring_type='Outer'))
    def test_reference_cannot_be_a_facade(self):self.rejected(lambda m,q,o,e:m['modules'][0]['forwarders'].append({}))
    def test_facade_cannot_contain_implementation(self):self.rejected(lambda m,q,o,e:m['modules'][1]['defined_types'].append({}))
    def test_missing_endpoint_definition_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2]['defined_types'].pop())
    def test_missing_reference_definition_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][0]['defined_types'].pop())
    def test_changed_endpoint_type_classification_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2]['defined_types'][0].update(attributes=0))
    def test_changed_endpoint_base_rejected(self):self.rejected(lambda m,q,o,e:m['modules'][2]['defined_types'][0].update(base_type={'full_name':'Different.Base'}))
    def test_endpoint_enum_constants_rejected(self):
        def change(m,q,o,e):
            t=next(t for t in m['modules'][2]['defined_types'] if t['name']=='System.AttributeTargets');t['fields'][1]['constant']=17
        self.rejected(change)
    def test_original_enum_constants_rejected(self):self.rejected(lambda m,q,o,e:o['assemblies'][0]['types'][0]['fields'][1].update(default_value=17))
    def test_loaded_enum_constants_rejected(self):self.rejected(lambda m,q,o,e:e['assemblies'][0]['types'][0]['fields'][1].update(default_value=17))
    def test_endpoint_enum_storage_rejected(self):
        def change(m,q,o,e):
            t=next(t for t in m['modules'][2]['defined_types'] if t['name']=='System.AttributeTargets');t['fields'][0]['type']['full_name']='System.UInt32'
        self.rejected(change)


class ThemeContractProjectionTests(unittest.TestCase):
    def setUp(self):self.projection=contract.graph_projection(*fixture()[:2],'macos',*fixture()[2:])
    def test_exact_string_scope_keeps_name(self):
        self.assertEqual(self.projection.identity(('named','netstandard','System.String')),('named','mscorlib','System.String'))
    def test_editor_browsable_goes_only_to_system(self):
        self.assertEqual(self.projection.identity(('named','netstandard','System.ComponentModel.EditorBrowsableState')),('named','System','System.ComponentModel.EditorBrowsableState'))
    def test_same_name_fake_assembly_is_not_aliased(self):
        v=('named','Fake','System.String');self.assertEqual(self.projection.identity(v),v)
    def test_unknown_netstandard_type_is_not_aliased(self):
        v=('named','netstandard','System.Decimal');self.assertEqual(self.projection.identity(v),v)
    def test_mscorlib_editor_browsable_is_not_assumed(self):
        v=('named','mscorlib','System.ComponentModel.EditorBrowsableState');self.assertEqual(self.projection.identity(v),v)
    def test_original_wrong_system_string_is_not_aliased(self):
        v=('named','System','System.String');self.assertEqual(self.projection.identity(v),v)
    def test_typed_null_remains_exact(self):
        v=('primitive','IL2CPP_TYPE_STRING',None);self.assertEqual(self.projection.value(v),v)
        other=('null',None);self.assertEqual(self.projection.value(other),other);self.assertNotEqual(other,v)
    def test_attribute_enum_value_keeps_integer(self):
        v=('enum',('named','netstandard','System.AttributeTargets'),('primitive','IL2CPP_TYPE_I4',256))
        expected=('enum',('named','mscorlib','System.AttributeTargets'),('primitive','IL2CPP_TYPE_I4',256))
        self.assertEqual(self.projection.value(v),expected)
    def test_attribute_identity_projection_does_not_hide_extra_value(self):
        a=(('netstandard','System.AttributeUsageAttribute',(('primitive','IL2CPP_TYPE_I4',1),),()),)
        b=(('mscorlib','System.AttributeUsageAttribute',(('primitive','IL2CPP_TYPE_I4',2),),()),)
        self.assertNotEqual(self.projection.attributes(a),self.projection.attributes(b))


if __name__=='__main__':unittest.main()
