import copy
import hashlib
import json
from pathlib import Path
import sys
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib import bindings, themebindings as theme


def named(assembly, name, token='0x02000001'):
    return {'kind':'named','assembly':assembly,'reflection_full_name':name,'canonical_name':name,'token':token}


def attr(assembly, name):
    return {'assembly':assembly,'full_name':name,'arguments':[],'fields':[],'properties':[]}


def field(name, field_type, flags=1, serialize=True):
    attrs=[attr('UnityEngine.CoreModule','UnityEngine.SerializeField')] if serialize else []
    result={'name':name,'token':'0x04000001','field_type':field_type,'attributes':flags,'schema_complete':True,
        'custom_attributes_complete':True,'custom_attributes':attrs,'is_public':flags&7==6,'is_static':bool(flags&16),
        'is_const':bool(flags&64),'is_readonly':bool(flags&32),'non_serialized':bool(flags&128),
        'serialize_field':serialize,'serialize_reference':False,'has_default_value':False,'default_value_complete':True}
    result['unity_serialization_candidate']=not any(result[n] for n in ['is_static','is_const','is_readonly','non_serialized']) and (result['is_public'] or serialize)
    return result


def record(key, fields=(), flags=0x2001, base=None, params=()):
    return {'assembly':key[0],'full_name':key[1],'schema_complete':True,'fields':list(fields),'attributes':flags,
        'base_type':base,'generic_parameters':list(params),'custom_attributes_complete':True,'custom_attributes':[],
        'is_value_type':key==theme.COLOR,'is_enum':False,'is_abstract':False,'declaring_type':None,
        'unity_component':False,'unity_scriptable_object':False}


def bridge():
    constructed={'kind':'generic_instance','assembly':theme.ADDRESSABLE,'definition':theme.ASSET_GENERIC[1],'arguments':[named(*theme.SPRITE)]}
    return {theme.ATLASED:record(theme.ATLASED,base=constructed),theme.ASSET_GENERIC:record(theme.ASSET_GENERIC,
        base=named(*theme.ASSET_REFERENCE),params=[{'name':'TObject','index':0,'attributes':0,'constraints_complete':True,'constraints':[named(*theme.OBJECT)]}])}


def reference(editor=False):
    fs=[field('m_AssetGUID',named('mscorlib','System.String'),flags=5),field('m_SubObjectName',named('mscorlib','System.String')),field('m_SubObjectType',named('mscorlib','System.String')),
        field('m_Operation',named('Unity.ResourceManager','UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle'),serialize=False)]
    fs[0]['custom_attributes'].append({'assembly':'UnityEngine.CoreModule','full_name':'UnityEngine.Serialization.FormerlySerializedAsAttribute','arguments':[{'kind':'primitive','type':'IL2CPP_TYPE_STRING','value':'m_assetGUID'}],'fields':[],'properties':[]})
    if editor:
        fs.append(field('<DerivedClassType>k__BackingField',named('mscorlib','System.Type'),33,False))
        fs[-1]['custom_attributes']=[attr('mscorlib','System.Runtime.CompilerServices.CompilerGeneratedAttribute')]
        fs.append(field('m_ActiveAssetReferences',{'kind':'generic_instance','assembly':'System.Core','definition':'System.Collections.Generic.HashSet`1','arguments':[named(*theme.ASSET_REFERENCE)]},17,False))
        fs.append(field('m_CachedAsset',named(*theme.OBJECT),1,False));fs.append(field('m_CachedGUID',named('mscorlib','System.String'),1,False))
        fs.append(field('m_EditorAssetChanged',named('mscorlib','System.Boolean')))
    return {theme.ASSET_REFERENCE:record(theme.ASSET_REFERENCE,fields=fs)}


def color():
    return record(theme.COLOR,fields=[field(n,named('mscorlib','System.Single'),6,False) for n in 'rgba'],flags=0x100109,base=named('mscorlib','System.ValueType'))


def report(index):
    return {'assemblies':[{'name':a,'types':[r for (aa,n),r in index.items() if aa==a]} for a in sorted({a for a,n in index})]}


YAML = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
 "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
 "  m_Script: {fileID: 11500000, guid: "+theme.OWNER_SCRIPT+", type: 3}\n  m_Name: TestTheme\n  m_EditorClassIdentifier:\n"
 "  m_zoneAccentOverride:\n    m_AssetGUID: "+'a'*32+"\n    m_SubObjectName: test_icon\n    m_SubObjectType: UnityEngine.Sprite, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n"
 "  m_zoneGradientOverride: 1\n  m_backgroundStartColour: {r: 0, g: 0, b: 0, a: 1}\n  m_backgroundEndColour: {r: 0, g: 0, b: 0, a: 1}\n").encode()


class ThemeBridgeTests(unittest.TestCase):
    def test_complete_fieldless_sprite_bridge(self):theme._exact_bridge(bridge())
    def test_metadata_tokens_do_not_change_constraint_shape(self):
        a=bridge()[theme.ASSET_GENERIC];b=copy.deepcopy(a);b['generic_parameters'][0]['constraints'][0]['token']='0x02000abc';self.assertEqual(theme._params(a),theme._params(b))
    def test_incomplete_constraint_rejected(self):
        b=bridge();b[theme.ASSET_GENERIC]['generic_parameters'][0]['constraints_complete']=False
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_bool_generic_index_rejected(self):
        b=bridge();b[theme.ASSET_GENERIC]['generic_parameters'][0]['index']=False
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_constraint_change_rejected(self):
        b=bridge();b[theme.ASSET_GENERIC]['generic_parameters'][0]['constraints']=[named(*theme.SPRITE)]
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_non_sprite_argument_rejected(self):
        b=bridge();b[theme.ATLASED]['base_type']['arguments']=[named('UnityEngine.CoreModule','UnityEngine.Texture')]
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_nested_argument_rejected(self):
        b=bridge();b[theme.ATLASED]['base_type']['arguments']=[copy.deepcopy(b[theme.ATLASED]['base_type'])]
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_generic_own_nonserialized_field_rejected(self):
        b=bridge();b[theme.ASSET_GENERIC]['fields']=[field('extra',named('mscorlib','System.String'),serialize=False)]
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_wrong_named_base_rejected(self):
        b=bridge();b[theme.ASSET_GENERIC]['base_type']=named(*theme.ATLASED)
        with self.assertRaises(bindings.LayoutError):theme._exact_bridge(b)
    def test_missing_generic_definition_fails_closed(self):
        b=bridge();del b[theme.ASSET_GENERIC]
        with self.assertRaises(KeyError):theme._exact_bridge(b)


class ThemeConditionalTests(unittest.TestCase):
    def test_exact_player_and_editor_projection(self):
        a=theme._exact_asset_reference(reference(),False);b=theme._exact_asset_reference(reference(True),True)
        self.assertEqual([f['name'] for f in a],[f['name'] for f in b])
    def test_editor_field_in_player_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(reference(True),False)
    def test_missing_editor_field_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(reference(),True)
    def test_new_editor_field_rejected(self):
        a=reference(True);a[theme.ASSET_REFERENCE]['fields'].append(field('new',named('mscorlib','System.Boolean')))
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,True)
    def test_changed_editor_type_rejected(self):
        a=reference(True);a[theme.ASSET_REFERENCE]['fields'][-1]['field_type']=named('mscorlib','System.Int32')
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,True)
    def test_changed_editor_attributes_rejected(self):
        a=reference(True);a[theme.ASSET_REFERENCE]['fields'][-1]['custom_attributes'].append(attr('UnityEngine.CoreModule','UnityEngine.HideInInspector'))
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,True)
    def test_additional_candidate_cache_field_rejected(self):
        a=reference(True);a[theme.ASSET_REFERENCE]['fields'][6]=field('m_CachedAsset',named('UnityEngine.CoreModule','UnityEngine.Object'),6,False)
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,True)
    def test_nonserialized_cache_type_change_rejected(self):
        a=reference(True);a[theme.ASSET_REFERENCE]['fields'][6]['field_type']=named('mscorlib','System.Object')
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,True)
    def test_nonserialized_operation_field_change_rejected(self):
        a=reference();a[theme.ASSET_REFERENCE]['fields'][3]['attributes']=33
        with self.assertRaises(bindings.LayoutError):theme._exact_asset_reference(a,False)


class ThemeColorTests(unittest.TestCase):
    def test_color_exact_four_components(self):theme._exact_color(color())
    def test_color_order_rejected(self):
        a=color();a['fields'].reverse()
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_color_component_flags_rejected(self):
        a=color();a['fields'][0]=field('r',named('mscorlib','System.Single'),5,True)
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_color_component_type_rejected(self):
        a=color();a['fields'][0]['field_type']=named('mscorlib','System.Double')
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_color_flags_rejected(self):
        a=color();a['attributes']|=0x2000
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_color_base_rejected(self):
        a=color();a['base_type']=named('mscorlib','System.Object')
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_fake_color_assembly_rejected(self):
        a=color();a['fields'][0]['field_type']=named('Fake','System.Single')
        with self.assertRaises(bindings.LayoutError):theme._exact_color(a)
    def test_scope_difference_remains_blocked(self):
        a=color();b=copy.deepcopy(a);b['fields'][0]['field_type']=named('netstandard','System.Single')
        pair=theme._Pair(report({theme.COLOR:a}),report({theme.COLOR:b}),'original','player',color_verified=True);pair.visit(theme.COLOR)
        self.assertTrue(any(i['reason']=='target_contract_scope_unverified' for i in pair.issues))
    def test_builtin_predicate_required_even_matching_layout(self):
        a=color();p=theme._Pair(report({theme.COLOR:a}),report({theme.COLOR:a}),'original','editor',color_verified=False);p.visit(theme.COLOR)
        self.assertTrue(any(i['reason']=='color_builtin_predicate_unverified' for i in p.issues))


class ThemeOwnerTests(unittest.TestCase):
    def test_exact_nested_shape_and_float_storage(self):
        r=theme._theme_document_shape(YAML);self.assertEqual(r['object_id'],11400000);self.assertEqual(r['color_float32_bits']['s'],['00000000']*3+['0000803f'])
    def test_unreviewed_owner_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner('Assets/Any.asset',YAML,b'')
    def test_synthetic_asset_cannot_get_pinned_owner_approval(self):
        for path in theme.OWNERS:
            with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner(path,YAML,b'')
    def test_nested_additional_field_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'  m_zoneGradientOverride:',b'    extra: 0\n  m_zoneGradientOverride:'))
    def test_editor_field_in_raw_original_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'  m_zoneGradientOverride:',b'    m_EditorAssetChanged: 0\n  m_zoneGradientOverride:'))
    def test_extra_unity_document_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML+b'--- !u!114 &2\nMonoBehaviour:\n')
    def test_yaml_multiline_or_anchor_rejected(self):
        for value in [b'|',b'>',b'&anchor',b'"quoted"']:
            with self.subTest(value=value),self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'test_icon',value))
    def test_duplicate_nested_key_rejected(self):
        with self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'    m_SubObjectName:',b'    m_AssetGUID: '+b'b'*32+b'\n    m_SubObjectName:'))
    def test_signed64_boundary_and_overflow(self):
        for value in [-(1<<63),(1<<63)-1]:self.assertEqual(theme._theme_document_shape(YAML.replace(b'&11400000',('&'+str(value)).encode()))['object_id'],value)
        for value in [0,-(1<<63)-1,1<<63]:
            with self.subTest(value=value),self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'&11400000',('&'+str(value)).encode()))
    def test_invalid_color_number_rejected(self):
        for value in [b'.nan',b'.inf',b'1e999',b'1e40',b'&anchor',b'"1"']:
            with self.subTest(value=value),self.assertRaises(bindings.LayoutError):theme._theme_document_shape(YAML.replace(b'{r: 0',b'{r: '+value))
    def test_changed_script_id_or_type_rejected(self):
        for altered in [YAML.replace(b'fileID: 11500000',b'fileID: 11400000'),YAML.replace(b'type: 3}',b'type: 2}')]:
            with self.assertRaises(bindings.LayoutError):theme._theme_document_shape(altered)


def blob_index(plane='original'):
    fields=[]
    module={'path':'/tmp/module.dll','sha256':'a'*64,'mvid':'11111111-1111-1111-1111-111111111111','assembly_identity':'Game.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'}
    ctor={**module,'path':'/tmp/ctor.dll','assembly_identity':'HLUnityCore.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'}
    reader={**module,'path':'/tmp/reader.dll','sha256':theme.CECIL,'mvid':theme.CECIL_MVID,'assembly_identity':'Unity.Cecil, Version=0.10.0.0, Culture=neutral, PublicKeyToken=fc15b93552389f74'}
    for i,n in enumerate(['m_backgroundStartColour','m_backgroundEndColour']):
        f=field(n,named(*theme.COLOR));f['token']='0x0400000'+str(i+3)
        value={'kind':'primitive','type':'IL2CPP_TYPE_STRING','value':None,'value_complete':True,'string_length':-1,'raw_encoding_sha256':'4bf5122f344554c53bde2ebb8cd2b7e3d1600ad631c385a5d7cce23c7785459a'}
        attribute={'assembly':'HLUnityCore.Runtime','full_name':'Hardlight.ShowIfAttribute','constructor_token':'0x06000f2b','arguments':[{'kind':'primitive','type':'IL2CPP_TYPE_STRING','value':'m_zoneGradientOverride'},value],'fields':[],'properties':[]}
        if plane=='editor':value.update(boxed_element_tag=14,raw_encoding='ecma335-custom-attribute',raw_encoding_hex=theme.BLOB_HEX,raw_encoding_sha256=hashlib.sha256(bytes.fromhex(theme.BLOB_HEX)).hexdigest(),member_token=f['token'],constructor_token=attribute['constructor_token'],owner_module=module,constructor_module=ctor,reader_module=reader)
        if plane=='player':
            value.update(ecma_tag=14,ecma_boxed_tag=14,ecma_boxed=True)
            attribute.update(ecma_blob_hex=theme.BLOB_HEX,ecma_blob_sha256=hashlib.sha256(bytes.fromhex(theme.BLOB_HEX)).hexdigest(),ecma_decoding_complete=True,constructor_parameter_types=[named('netstandard','System.String'),named('netstandard','System.Object')])
        f['custom_attributes'].append(attribute);fields.append(f)
    result={theme.THEME:record(theme.THEME,fields=fields),('HLUnityCore.Runtime','Hardlight.ShowIfAttribute'):record(('HLUnityCore.Runtime','Hardlight.ShowIfAttribute'))}
    result[theme.THEME].update(loaded_module=copy.deepcopy(module),loaded_module_reader=copy.deepcopy(reader))
    result['HLUnityCore.Runtime','Hardlight.ShowIfAttribute']['loaded_module']=copy.deepcopy(ctor)
    return result


class ThemeOwnerClosureTests(unittest.TestCase):
    def rows(self):return [{'path':p,'guid':g,'code_quarantined':False,'script_references':[theme.OWNER_SCRIPT]} for p,(g,m) in theme.OWNERS.items()]
    def test_third_occurrence_blocks_before_asset_reads(self):
        rows=self.rows();rows.append({**rows[0],'path':'Assets/OtherTheme.asset'})
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':rows},lambda p:self.fail('must reject closure before reads'))
    def test_missing_owner_blocks_before_reads(self):
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':self.rows()[:1]},lambda p:self.fail('must reject closure before reads'))
    def test_duplicate_guid_blocks_before_reads(self):
        rows=self.rows();rows.append({'path':'Assets/Other.asset','guid':rows[0]['guid'],'script_references':[]})
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':rows},lambda p:self.fail('must reject GUID ambiguity before reads'))
    def test_owner_path_guid_swap_rejected(self):
        rows=self.rows();rows[0]['guid'],rows[1]['guid']=rows[1]['guid'],rows[0]['guid']
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':rows},lambda p:self.fail('must reject swapped GUIDs before reads'))
    def test_malformed_unrelated_inventory_row_rejected_before_reads(self):
        rows=self.rows();rows.append({'script_references':'ambiguous'})
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':rows},lambda p:self.fail('must reject malformed inventory before reads'))
    def test_nonobject_inventory_row_rejected_before_reads(self):
        rows=self.rows();rows.append(None)
        with self.assertRaises(bindings.LayoutError):theme.validate_theme_owner_set({'assets':rows},lambda p:self.fail('must reject malformed inventory before reads'))


class ThemeBlobTests(unittest.TestCase):
    def test_three_distinct_exact_representations(self):
        for plane in ['original','editor','player']:theme._boxed_null(blob_index(plane),plane)
    def test_missing_color_attribute_fields_rejected(self):
        a=blob_index();a[theme.THEME]['fields']=[]
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'original')
    def test_plain_untyped_null_never_coerced(self):
        for plane in ['original','editor','player']:
            a=blob_index(plane);a[theme.THEME]['fields'][0]['custom_attributes'][-1]['arguments'][1]={'kind':'null','value':None}
            with self.subTest(plane=plane),self.assertRaises(bindings.LayoutError):theme._boxed_null(a,plane)
    def test_empty_string_not_null(self):
        a=blob_index();v=a[theme.THEME]['fields'][0]['custom_attributes'][-1]['arguments'][1];v.update(value='',string_length=0)
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'original')
    def test_native_wrong_string_evidence_rejected(self):
        for key,value in [('value_complete',False),('string_length',0),('raw_encoding_sha256','b'*64)]:
            a=blob_index();a[theme.THEME]['fields'][0]['custom_attributes'][-1]['arguments'][1][key]=value
            with self.subTest(key=key),self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'original')
    def test_native_wrong_constructor_rejected(self):
        a=blob_index();a[theme.THEME]['fields'][0]['custom_attributes'][-1]['constructor_token']='0x06000001'
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'original')
    def test_named_attribute_arguments_rejected(self):
        a=blob_index();a[theme.THEME]['fields'][0]['custom_attributes'][-1]['fields']=[{'name':'extra','value':{'kind':'primitive','type':'IL2CPP_TYPE_BOOLEAN','value':False}}]
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'original')
    def test_player_boxed_type_not_normalized(self):
        a=blob_index('player');a[theme.THEME]['fields'][0]['custom_attributes'][-1]['arguments'][1]['ecma_boxed_tag']=80
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'player')
    def test_player_constructor_signature_changed(self):
        a=blob_index('player');a[theme.THEME]['fields'][0]['custom_attributes'][-1]['constructor_parameter_types'][1]=named('netstandard','System.String')
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'player')
    def test_player_blob_digest_changed(self):
        a=blob_index('player');a[theme.THEME]['fields'][0]['custom_attributes'][-1]['ecma_blob_sha256']='b'*64
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'player')
    def test_loaded_blob_context_module_changed(self):
        a=copy.deepcopy(blob_index('editor'));a[theme.THEME]['loaded_module']['mvid']='22222222-2222-2222-2222-222222222222'
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'editor')
    def test_loaded_blob_reader_changed(self):
        a=copy.deepcopy(blob_index('editor'));a[theme.THEME]['loaded_module_reader']['sha256']='b'*64
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'editor')
    def test_loaded_member_token_changed(self):
        a=blob_index('editor');a[theme.THEME]['fields'][0]['custom_attributes'][-1]['arguments'][1]['member_token']='0x04000002'
        with self.assertRaises(bindings.LayoutError):theme._boxed_null(a,'editor')


class ThemeFrontierTests(unittest.TestCase):
    def test_alias_input_cannot_grant_approval(self):
        original={'status':'ready','unity_version':'2022.3.54f1','errors':[],'assemblies':[]}
        editor={**original,'source_fingerprint':'a'*64,'original_schema_sha256':theme.ORIGINAL_SCHEMA}
        result=theme.compare_theme_planes(original,{},editor,schema_sha256=theme.ORIGINAL_SCHEMA,source_fingerprint='a'*64,package_source_bytes=b'',runtime_contract_evidence={'netstandard':'mscorlib','approved':True})
        self.assertEqual(result['status'],'blocked');self.assertFalse(result['remap_approved']);self.assertFalse(result['references_modified'])
        self.assertTrue(any(i['reason']=='invalid_target_contract_graph' for i in result['issues']))
        self.assertTrue(any(i['reason']=='runtime_contract_wrapper_freshness_not_integrated' for i in result['issues']))
        self.assertTrue(any(i['reason']=='target_runtime_contract_projection_unverified' for i in result['issues']))
    def test_stale_source_and_original_schema_pin_explicit(self):
        original={'status':'ready','unity_version':'2022.3.54f1','errors':[],'assemblies':[]}
        editor={**original,'source_fingerprint':'b'*64,'original_schema_sha256':'c'*64}
        result=theme.compare_theme_planes(original,{},editor,schema_sha256=theme.ORIGINAL_SCHEMA,source_fingerprint='a'*64,package_source_bytes=b'')
        reasons={i['reason'] for i in result['issues']};self.assertIn('loaded_inventory_original_schema_stale',reasons);self.assertIn('loaded_inventory_source_or_engine_stale',reasons);self.assertIn('addressables_conditional_source_stale',reasons)


class ThemePairProjectionTests(unittest.TestCase):
    def projection(self):
        from test_themecontracts import fixture
        metadata,query,original,editor=fixture()
        return theme.themecontracts.graph_projection(metadata,query,'macos',original,editor)
    def test_alias_dict_is_not_a_checked_graph(self):
        with self.assertRaises(bindings.LayoutError):theme._Pair(report({}),report({}),'original','player',color_verified=True,contracts={'netstandard':'mscorlib'})
    def test_resolved_color_scope_records_both_declarations(self):
        a=color();b=copy.deepcopy(a)
        for f in b['fields']:f['field_type']['assembly']='netstandard'
        b['base_type']['assembly']='netstandard'
        p=theme._Pair(report({theme.COLOR:a}),report({theme.COLOR:b}),'original','player',color_verified=True,contracts=self.projection());p.visit(theme.COLOR)
        self.assertFalse(p.issues);self.assertEqual(len(p.contract_matches),5)
        self.assertEqual(p.contract_matches[0]['left_declared'][1],'mscorlib');self.assertEqual(p.contract_matches[0]['right_declared'][1],'netstandard')
    def test_resolved_scope_does_not_hide_component_change(self):
        a=color();b=copy.deepcopy(a);b['fields'][0]['field_type']=named('netstandard','System.Int32')
        p=theme._Pair(report({theme.COLOR:a}),report({theme.COLOR:b}),'original','player',color_verified=True,contracts=self.projection());p.visit(theme.COLOR)
        self.assertTrue(p.issues)
    def test_unknown_contract_remains_unverified(self):
        p=theme._Pair(report({}),report({}),'original','player',color_verified=True,contracts=self.projection())
        self.assertFalse(p.identity(named('mscorlib','System.Double'),named('netstandard','System.Double'),theme.COLOR,'extra'))
        self.assertEqual(p.issues[0]['reason'],'target_contract_scope_unverified')
    def test_fake_same_name_remains_unverified(self):
        p=theme._Pair(report({}),report({}),'original','player',color_verified=True,contracts=self.projection())
        self.assertFalse(p.identity(named('Fake','System.String'),named('netstandard','System.String'),theme.ASSET_REFERENCE,'extra'))
    def test_full_type_flags_are_compared(self):
        a=color();b=copy.deepcopy(a);b['attributes']|=0x80
        p=theme._Pair(report({theme.COLOR:a}),report({theme.COLOR:b}),'original','player',color_verified=True);p.visit(theme.COLOR)
        self.assertTrue(any(i['reason']=='type_flags_mismatch' for i in p.issues))
    def test_nonserialized_declared_field_change_is_detected(self):
        a=record(('Game.Runtime','Example'),fields=[field('hidden',named('mscorlib','System.String'),serialize=False)]);b=copy.deepcopy(a);b['fields'][0]['field_type']=named('mscorlib','System.Int32')
        p=theme._Pair(report({('Game.Runtime','Example'):a}),report({('Game.Runtime','Example'):b}),'original','player',color_verified=True);p.visit(('Game.Runtime','Example'))
        self.assertTrue(any(i['reason']=='type_identity_mismatch' for i in p.issues))


class ThemeObjectDeclarationTests(unittest.TestCase):
    def object(self,editor):
        fs=[field('m_CachedPtr',named('mscorlib','System.IntPtr'),1,False),
            field('OffsetOfInstanceIDInCPlusPlusObject',named('mscorlib','System.Int32'),19,False),
            field('objectIsNullMessage',named('mscorlib','System.String'),32849,False),
            field('cloneDestroyedMessage',named('mscorlib','System.String'),32849,False)]
        if editor:fs[1:1]=[field('m_InstanceID',named('mscorlib','System.Int32'),1,False),field('m_UnityRuntimeErrorString',named('mscorlib','System.String'),1,False)]
        r=record(theme.OBJECT,fields=fs);r['loaded_module']={'sha256':theme.EDITOR_CORE[0],'mvid':theme.EDITOR_CORE[1]};return r
    def test_only_exact_nonserialized_engine_delta_is_projected(self):
        a=self.object(False);b=self.object(True)
        self.assertEqual([f['name'] for f in theme._declared_fields(theme.OBJECT,a,'original',{})],
                         [f['name'] for f in theme._declared_fields(theme.OBJECT,b,'editor',{})])
    def test_unpinned_loaded_core_cannot_project_fields(self):
        b=self.object(True);b['loaded_module']['sha256']='a'*64
        with self.assertRaises(bindings.LayoutError):theme._declared_fields(theme.OBJECT,b,'editor',{})
    def test_serialized_instance_id_is_not_ignored(self):
        b=self.object(True);b['fields'][1]=field('m_InstanceID',named('mscorlib','System.Int32'))
        with self.assertRaises(bindings.LayoutError):theme._declared_fields(theme.OBJECT,b,'editor',{})
    def test_added_private_engine_field_is_not_ignored(self):
        b=self.object(True);b['fields'].append(field('unknown',named('mscorlib','System.String'),1,False))
        with self.assertRaises(bindings.LayoutError):theme._declared_fields(theme.OBJECT,b,'editor',{})
    def test_player_has_no_editor_field_projection(self):
        b=self.object(True)
        with self.assertRaises(bindings.LayoutError):theme._declared_fields(theme.OBJECT,b,'macos-player',{})


class OriginalSchemaContentTests(unittest.TestCase):
    def setUp(self):
        # Small synthetic graph tests the seal mechanics, never original authority.
        self.original = {
            'schema_version': 1, 'status': 'ready', 'metadata_header_version': 33,
            'metadata_interpreted_version': 31.1, 'errors': [],
            'native_binary': '/input/code', 'metadata': '/input/metadata',
            'output_dir': '/output/first',
            'native_binary_sha256': theme.ORIGINAL_INPUT_SHA256['native_binary'],
            'metadata_sha256': theme.ORIGINAL_INPUT_SHA256['metadata'],
            'managed_semantics_recovered': False,
            'assemblies': [
                {'name': 'Example', 'types': [{
                    'full_name': 'Example.Type', 'assembly': 'Example', 'attributes': 257,
                    'native_binary': 'nested context-named field is retained',
                    'fields': [{'name': 'First'}, {'name': 'Second'}],
                    'methods': [{'token': '0x06000001'}, {'token': '0x06000002'}],
                    'generic_parameters': [{
                        'name': 'T', 'constraints_complete': True,
                        'constraints': [{'name': 'Base'}, {'name': 'Interface'}],
                    }],
                }]},
                {'name': 'Other', 'types': []},
            ],
        }
        self.pin = theme.original_schema_content_sha256(self.original)
        patch = mock.patch.object(theme, 'ORIGINAL_SCHEMA_CONTENT', self.pin)
        patch.start(); self.addCleanup(patch.stop)

    def reject(self, mutate):
        changed = copy.deepcopy(self.original)
        mutate(changed)
        with self.assertRaises(bindings.LayoutError):
            theme.require_reviewed_original_schema(changed)

    def test_only_three_top_level_context_paths_can_change(self):
        changed = copy.deepcopy(self.original)
        changed.update(native_binary='/relocated/code', metadata='/relocated/metadata',
                       output_dir='/output/new-nonce')
        self.assertEqual(theme.require_reviewed_original_schema(changed), self.pin)
        self.assertNotEqual(hashlib.sha256(json.dumps(changed).encode()).hexdigest(),
                            hashlib.sha256(json.dumps(self.original).encode()).hexdigest())

    def test_missing_context_path_is_rejected(self):
        for key in theme.ORIGINAL_CONTEXT_PATHS:
            with self.subTest(key=key): self.reject(lambda r: r.pop(key))

    def test_context_paths_keep_string_shape(self):
        for key in theme.ORIGINAL_CONTEXT_PATHS:
            for value in (None, False, 1, 1.0, [], {}, '', 'bad\0path'):
                with self.subTest(key=key, value=value):
                    self.reject(lambda r: r.update({key: value}))

    def test_nested_context_named_field_is_not_excluded(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0].update(native_binary='changed'))

    def test_object_key_order_and_whitespace_are_serialization_context(self):
        changed = json.loads(json.dumps(self.original, indent=3))
        changed = dict(reversed(list(changed.items())))
        self.assertEqual(theme.require_reviewed_original_schema(changed), self.pin)

    def test_assembly_order_is_sealed(self):
        self.reject(lambda r: r['assemblies'].reverse())

    def test_field_order_is_sealed(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0]['fields'].reverse())

    def test_method_order_is_sealed(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0]['methods'].reverse())

    def test_constraint_order_is_sealed(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0]['generic_parameters'][0]['constraints'].reverse())

    def test_constraint_edge_is_sealed(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0]['generic_parameters'][0]['constraints'][0].update(name='Wrong'))

    def test_member_name_and_type_flags_are_sealed(self):
        self.reject(lambda r: r['assemblies'][0]['types'][0]['fields'][0].update(name='Wrong'))
        self.reject(lambda r: r['assemblies'][0]['types'][0].update(attributes=258))

    def test_unknown_false_flag_is_sealed(self):
        self.reject(lambda r: r.update(layout_approved=False))
        self.reject(lambda r: r['assemblies'][0]['types'][0].update(unknown_approval=False))

    def test_boolean_integer_and_float_types_are_distinct(self):
        for key, values in (('schema_version', (True, 1.0)),
                            ('metadata_header_version', (33.0,)),
                            ('managed_semantics_recovered', (0, 0.0))):
            for value in values:
                with self.subTest(key=key, value=value):
                    self.reject(lambda r: r.update({key: value}))
        self.reject(lambda r: r['assemblies'][0]['types'][0]['generic_parameters'][0].update(constraints_complete=1))

    def test_genuine_interpreted_version_float_is_retained(self):
        self.assertIs(type(self.original['metadata_interpreted_version']), float)
        self.assertEqual(theme.require_reviewed_original_schema(self.original), self.pin)
        self.reject(lambda r: r.update(metadata_interpreted_version=31))

    def test_declared_input_hashes_remain_in_content(self):
        self.reject(lambda r: r.update(native_binary_sha256='0' * 64))
        self.reject(lambda r: r.update(metadata_sha256='0' * 64))

    def test_nonfinite_and_non_json_values_rejected(self):
        for value in (float('nan'), float('inf'), float('-inf'), (), set()):
            with self.subTest(value=value): self.reject(lambda r: r.update(extra=value))
        self.reject(lambda r: r.update({1: 'non-string object key'}))

    def test_comparison_reports_both_content_and_actual_raw_identity(self):
        raw = json.dumps(self.original).encode()
        raw_sha = hashlib.sha256(raw).hexdigest()
        editor = {'status': 'ready', 'unity_version': '2022.3.54f1', 'errors': [],
                  'assemblies': [], 'source_fingerprint': 'a' * 64,
                  'original_schema_sha256': raw_sha}
        result = theme.compare_theme_planes(
            self.original, {}, editor, schema_sha256=raw_sha,
            source_fingerprint='a' * 64, package_source_bytes=b'')
        self.assertEqual(result['original_schema_sha256'], raw_sha)
        self.assertEqual(result['original_schema_content_sha256'], self.pin)
        self.assertNotIn('unreviewed_original_constraint_schema',
                         {row['reason'] for row in result['issues']})
        self.assertIs(result['remap_approved'], False)


if __name__=='__main__':unittest.main()
