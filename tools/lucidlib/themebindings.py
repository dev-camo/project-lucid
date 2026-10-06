"""Read-only evidence for one exact Theme serialization profile.

This candidate preserves declared contract scopes. Target Mono contract resolution
has a separate evidence frontier; this module grants no script/asset approval.
"""

import hashlib
import json
import math
import re
import struct
from . import bindings, themecontracts

THEME = ('Game.Runtime', 'HardlightProject.ZoneThemeOverride')
ADDRESSABLE = 'Unity.Addressables'
ASSET_REFERENCE = (ADDRESSABLE, 'UnityEngine.AddressableAssets.AssetReference')
ASSET_GENERIC = (ADDRESSABLE, 'UnityEngine.AddressableAssets.AssetReferenceT`1')
ATLASED = (ADDRESSABLE, 'UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite')
COLOR = ('UnityEngine.CoreModule', 'UnityEngine.Color')
SPRITE = ('UnityEngine.CoreModule', 'UnityEngine.Sprite')
OBJECT = ('UnityEngine.CoreModule', 'UnityEngine.Object')
OPTION = ('HLUnityCore.Runtime', 'Unity.IL2CPP.CompilerServices.Option')
INSPECTOR_ENUM = ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalAttribute+ComparisonType')
PROFILE_TYPES = frozenset((THEME, ASSET_REFERENCE, ASSET_GENERIC, ATLASED, COLOR, SPRITE, OBJECT,
    ('UnityEngine.CoreModule', 'UnityEngine.ScriptableObject'),
    ('UnityEngine.CoreModule', 'UnityEngine.PropertyAttribute'), OPTION, INSPECTOR_ENUM,
    ('HLUnityCore.Runtime', 'Hardlight.ShowIfAttribute'),
    ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalAttribute'),
    ('HLUnityCore.Runtime', 'Hardlight.InspectorConditionalField')))
PROFILE_TYPES |= frozenset((
    ('HLUnityCore.Runtime', 'Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.CreateAssetMenuAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExcludeFromPresetAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.ExtensionOfNativeClassAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.Serialization.FormerlySerializedAsAttribute'),
    ('UnityEngine.CoreModule', 'UnityEngine.SerializeField'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeHeaderAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.NativeTypeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Bindings.VisibleToOtherModulesAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.NativeClassAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.RequiredByNativeCodeAttribute'),
    ('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.UsedByNativeCodeAttribute')))
TARGETS = ('macos', 'windows', 'linux')
ORIGINAL_SCHEMA = '74fe03b9f86733de89ff90ad39d5e27c1d40d2606ccdd23d61b40ac0129de205'
# ORIGINAL_SCHEMA is the historical reviewed raw artifact, not a portable
# acceptance condition. Every current inventory/receipt keeps its own raw SHA.
ORIGINAL_SCHEMA_CONTENT = '670f363a2ae89114dd3681430f636ccaee03809a509c5a06e38d5b24aaba62d7'
ORIGINAL_CONTEXT_PATHS = ('native_binary', 'metadata', 'output_dir')
ORIGINAL_INPUT_SHA256 = {
    'native_binary': '797ef6ac359e8dbfc83d97a6148fe9aa7ac85113217e3013e00c3c5b30502c94',
    'metadata': 'acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b',
}
AA_SOURCE = '964dc8d67a9d9d4d0a62c67706b3e52963a708a586238a1090e18ccbcef2e5d1'
SERIALIZER = '87bc9d79cc791c4c9f898bb6949895b8f02077f886dd9c23867b22fd4202fb55'
CECIL = '98ae4d35b3f91f017b1babab876d2bf4594af3d65e7e777fbd37fd1e4124f95e'
CECIL_MVID = 'a6860a9f-6366-4373-87eb-dc1f225b7fd4'
SERIALIZER_MVID = '2468545d-e10f-4afa-8c3c-d5b7b3a68fa8'
EDITOR_CORE = ('47a0cd2578df712c2cff5c7785ac5c95ca98985f57392572fe68dff9c967f2bc','30c530d7-db63-4410-97c8-dced8b6e46f0')
PLAYER_CORES = {
    'macos':('ac3424f0f9ec3c7aeae88c8e2fa146d4bbce001a4a333220d50709757f154c33','5de7ccdc-b02f-4ba2-a82e-3c4a1175a8c9'),
    'windows':('3089246485db0ac0611475420632165cb85d738d1ae1f52fc49cea6d9791cf87','be2cce08-ca77-4b96-8409-9a81093ecac0'),
    'linux':('f289eb0e7b4a69858c2e0791d868ba05f51a664195084cfb6f44d6ce8d4c956b','bbfcdb8e-91a4-4ae7-9d53-e845fd06b4b5'),
}
BLOB_HEX = '0100166d5f7a6f6e654772616469656e744f766572726964650eff0000'
OWNER_ASSET_SHA = '25fadbbb247d4eabd5030600abe066bb49babf07ea1ca827c8fe82d4ccc94549'
OWNER_SCRIPT = 'e9c664a182e4d5737be3043a627af8d6'
OWNERS = {
    'Assets/MonoBehaviour/ShadowUITheme.asset': ('5085c968e9fb71f4e9b5cea3b407a47c', '22d411c9eb8cc49e68e0d3344ebe541c17ce2180baded1ddc671ca747d18bca4'),
    'Assets/Data/Definitions/Levels/UI/ShadowUITheme.asset': ('befa2d71ecd21074f81a1e04680ca7fc', 'c6d714b90e02ad3514a9bc7b96409fabc9f2eeec0533c52c1be0cc1535657018'),
}


def original_schema_content_sha256(report):
    """Hash strict JSON content, excluding only three top-level context paths.

    Object keys are sorted; every array retains its order. Scalar types, flags,
    native addresses, input hashes and all declaration/constraint graphs remain
    in the seal. This content identity never substitutes for a raw report SHA.
    """
    if type(report) is not dict:
        raise bindings.LayoutError('Original schema must be a JSON object')
    for key in ORIGINAL_CONTEXT_PATHS:
        path = report.get(key)
        if type(path) is not str or not path or '\0' in path:
            raise bindings.LayoutError('Original schema context path is absent or invalid: ' + key)

    def validate(value):
        kind = type(value)
        if kind is dict:
            if any(type(key) is not str for key in value):
                raise bindings.LayoutError('Original schema JSON object key is not a string')
            for child in value.values():
                validate(child)
        elif kind is list:
            for child in value:
                validate(child)
        elif kind is float:
            if not math.isfinite(value):
                raise bindings.LayoutError('Original schema JSON number is not finite')
        elif kind not in (str, int, bool, type(None)):
            raise bindings.LayoutError('Original schema contains a non-JSON value')

    validate(report)
    content = {key: value for key, value in report.items() if key not in ORIGINAL_CONTEXT_PATHS}
    try:
        raw = json.dumps(content, sort_keys=True, separators=(',', ':'),
                         ensure_ascii=False, allow_nan=False).encode('utf-8')
    except (TypeError, ValueError, UnicodeError) as error:
        raise bindings.LayoutError('Original schema cannot be canonically encoded') from error
    return hashlib.sha256(raw).hexdigest()


def require_reviewed_original_schema(report):
    """Require the complete reviewed content, independently of context paths."""
    digest = original_schema_content_sha256(report)
    if digest != ORIGINAL_SCHEMA_CONTENT:
        raise bindings.LayoutError('The reviewed complete original constrained schema content is required')
    return digest


def _params(record):
    values = record.get('generic_parameters')
    if not isinstance(values, list) or len(values) > 1:
        raise bindings.LayoutError('Theme generic parameter graph is unsupported')
    result = []
    for i, p in enumerate(values):
        if (not isinstance(p,dict) or not isinstance(p.get('name'),str) or not p['name'] or
                type(p.get('index')) is not int or p['index'] != i or type(p.get('attributes')) is not int or
                p.get('constraints_complete') is not True or not isinstance(p.get('constraints'), list)):
            raise bindings.LayoutError('Theme generic constraints are incomplete')
        result.append((p.get('name'), i, p['attributes'], tuple(bindings.type_identity(c) for c in p['constraints'])))
    return tuple(result)


def _exact_bridge(index):
    atlas, generic = index[ATLASED], index[ASSET_GENERIC]
    for record in (atlas, generic):
        if record['fields'] or not record['attributes'] & 0x2000:
            raise bindings.LayoutError('Theme generic bridge must be fieldless and Serializable')
        bindings._candidate_fields(record, require_string_evidence=True)
    base = bindings.type_identity(atlas['base_type'])
    expected = ('generic_instance', ADDRESSABLE, ASSET_GENERIC[1], (('named', *SPRITE),))
    if base != expected or _params(atlas) != () or _params(generic) != (
            ('TObject', 0, 0, (('named', *OBJECT),)),):
        raise bindings.LayoutError('Theme generic argument or Object constraint differs')
    if bindings.type_identity(generic['base_type']) != ('named', *ASSET_REFERENCE):
        raise bindings.LayoutError('Theme generic bridge base differs')


def _exact_asset_reference(index, editor):
    record = index[ASSET_REFERENCE]
    expected = ['m_AssetGUID', 'm_SubObjectName', 'm_SubObjectType', 'm_Operation']
    if editor:
        expected += ['<DerivedClassType>k__BackingField', 'm_ActiveAssetReferences', 'm_CachedAsset', 'm_CachedGUID', 'm_EditorAssetChanged']
    if [f['name'] for f in record['fields']] != expected:
        raise bindings.LayoutError('Unexpected declared AssetReference field inventory')
    operation=record['fields'][3]
    if (operation['attributes']!=1 or bindings.type_identity(operation['field_type'])!=(
            'named','Unity.ResourceManager','UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle') or
            bindings._attributes(operation,require_string_evidence=True)!=()):
        raise bindings.LayoutError('AssetReference operation field shape differs')
    if editor:
        # These four fields are not serialized, but their exact declaration proof
        # bounds the pinned Editor-only delta. No unknown cache field is ignored.
        shapes=[(33,('named','mscorlib','System.Type'),(('mscorlib','System.Runtime.CompilerServices.CompilerGeneratedAttribute',(),()),)),
            (17,('generic_instance','System.Core','System.Collections.Generic.HashSet`1',(('named',*ASSET_REFERENCE),)),()),
            (1,('named',*OBJECT),()),(1,('named','mscorlib','System.String'),())]
        for field,(flags,identity,attrs) in zip(record['fields'][4:8],shapes):
            if (field['attributes']!=flags or bindings.type_identity(field['field_type'])!=identity or
                    bindings._attributes(field,require_string_evidence=True)!=attrs):
                raise bindings.LayoutError('AssetReference Editor nonserialized delta differs')
    fields = bindings._candidate_fields(record, require_string_evidence=True)
    if [f['name'] for f in fields] != ['m_AssetGUID', 'm_SubObjectName', 'm_SubObjectType'] + (['m_EditorAssetChanged'] if editor else []):
        raise bindings.LayoutError('Unexpected serialized AssetReference fields')
    for i, field in enumerate(fields[:3]):
        value=bindings.type_identity(field['field_type'])
        expected=(('UnityEngine.CoreModule','UnityEngine.SerializeField',(),()),)
        if i==0:expected += (('UnityEngine.CoreModule','UnityEngine.Serialization.FormerlySerializedAsAttribute',(('primitive','IL2CPP_TYPE_STRING','m_assetGUID'),),()),)
        if field['attributes'] != (5 if i==0 else 1) or value[0]!='named' or value[1] not in ('mscorlib','netstandard') or value[2]!='System.String' or bindings._attributes(field,require_string_evidence=True)!=tuple(sorted(expected,key=repr)):
            raise bindings.LayoutError('Authored AssetReference string/flags/attributes differ')
    if editor:
        changed = fields[-1]
        attrs = bindings._attributes(changed, require_string_evidence=True)
        if changed['attributes'] != 1 or bindings.type_identity(changed['field_type']) != ('named', 'mscorlib', 'System.Boolean') or attrs != (
                ('UnityEngine.CoreModule', 'UnityEngine.SerializeField', (), ()),):
            raise bindings.LayoutError('Editor conditional field shape differs')
    return fields[:-1] if editor else fields


def _exact_color(record):
    if (record.get('assembly'),record.get('full_name')) != COLOR:
        raise bindings.LayoutError('Theme Color assembly/type identity differs')
    if record['attributes'] != 0x100109 or record.get('is_value_type') is not True or record.get('is_enum') is not False:
        raise bindings.LayoutError('Theme Color type flags differ')
    fields = bindings._candidate_fields(record, require_string_evidence=True)
    if len(record['fields']) != 4 or [f['name'] for f in fields] != ['r', 'g', 'b', 'a']:
        raise bindings.LayoutError('Theme Color order/layout differs')
    for field in fields:
        identity = bindings.type_identity(field['field_type'])
        if field['attributes'] != 6 or identity[0] != 'named' or identity[1] not in ('mscorlib', 'netstandard') or identity[2] != 'System.Single':
            raise bindings.LayoutError('Theme Color component storage differs')
    if record['base_type'].get('reflection_full_name') != 'System.ValueType':
        raise bindings.LayoutError('Theme Color base differs')


def _boxed_null(index, plane):
    fields=[f for f in index[THEME]['fields'] if f['name'] in ('m_backgroundStartColour','m_backgroundEndColour')]
    if [f['name'] for f in fields] != ['m_backgroundStartColour','m_backgroundEndColour']:
        raise bindings.LayoutError('Theme boxed-null fields missing/duplicated/out of order')
    for field in fields:
        matches = [a for a in field['custom_attributes'] if a.get('full_name') == 'Hardlight.ShowIfAttribute']
        if len(matches) != 1:
            raise bindings.LayoutError('Theme ShowIf attribute is missing or ambiguous')
        a = matches[0]
        if a.get('assembly') != 'HLUnityCore.Runtime' or a.get('fields') != [] or a.get('properties') != [] or len(a.get('arguments', [])) != 2:
            raise bindings.LayoutError('Theme ShowIf constructor shape differs')
        first, null = a['arguments']
        if (bindings._attribute_value(first) != ('primitive', 'IL2CPP_TYPE_STRING', 'm_zoneGradientOverride') or
                bindings._attribute_value(null, require_string_evidence=True) != ('primitive', 'IL2CPP_TYPE_STRING', None)):
            raise bindings.LayoutError('Theme boxed null-string evidence differs')
        if plane == 'original':
            if a.get('constructor_token') != '0x06000f2b' or null.get('raw_encoding_sha256') != '4bf5122f344554c53bde2ebb8cd2b7e3d1600ad631c385a5d7cce23c7785459a':
                raise bindings.LayoutError('Theme native null-string evidence differs')
        elif plane == 'editor':
            if (null.get('boxed_element_tag') != 14 or null.get('raw_encoding') != 'ecma335-custom-attribute' or
                    null.get('raw_encoding_hex') != BLOB_HEX or null.get('raw_encoding_sha256') != hashlib.sha256(bytes.fromhex(BLOB_HEX)).hexdigest() or
                    null.get('member_token') != field['token'] or null.get('constructor_token') != a.get('constructor_token')):
                raise bindings.LayoutError('Theme loaded ECMA null-string evidence differs')
            for name in ('owner_module', 'constructor_module', 'reader_module'):
                _module(null.get(name))
            for name,key in [('owner_module',THEME),('constructor_module',('HLUnityCore.Runtime','Hardlight.ShowIfAttribute'))]:
                if null[name] != index[key].get('loaded_module'):
                    raise bindings.LayoutError('Theme ECMA blob module differs from loaded type evidence')
            if null['reader_module'] != index[THEME].get('loaded_module_reader'):
                raise bindings.LayoutError('Theme ECMA blob reader differs from loaded type evidence')
        else:
            if (a.get('ecma_blob_hex') != BLOB_HEX or a.get('ecma_decoding_complete') is not True or
                    null.get('ecma_tag') != 14 or null.get('ecma_boxed_tag') != 14 or null.get('ecma_boxed') is not True):
                raise bindings.LayoutError('Theme genuine player ECMA boxed type differs')
            if a.get('ecma_blob_sha256') != hashlib.sha256(bytes.fromhex(BLOB_HEX)).hexdigest():
                raise bindings.LayoutError('Theme player ECMA blob digest differs')
            params = a.get('constructor_parameter_types', [])
            if [p.get('canonical_name') for p in params] != ['System.String', 'System.Object']:
                raise bindings.LayoutError('Theme player constructor signature differs')


def _module(record):
    if (not isinstance(record, dict) or not isinstance(record.get('path'), str) or
            not re.fullmatch(r'[a-f0-9]{64}', record.get('sha256', '')) or
            not re.fullmatch(r'[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}', record.get('mvid', '')) or
            not isinstance(record.get('assembly_identity'), str)):
        raise bindings.LayoutError('Theme loaded module identity is incomplete')


def _declared_fields(key,record,plane,index):
    """Bound all declared deltas, including fields excluded from serialization."""
    if key==ASSET_REFERENCE:
        _exact_asset_reference(index,plane=='editor')
        return record['fields'][:4]
    if key!=OBJECT:return record['fields']
    expected=['m_CachedPtr','OffsetOfInstanceIDInCPlusPlusObject','objectIsNullMessage','cloneDestroyedMessage']
    if plane=='editor':expected[1:1]=['m_InstanceID','m_UnityRuntimeErrorString']
    if [f['name'] for f in record['fields']]!=expected:
        raise bindings.LayoutError('Unity Object declared field inventory differs')
    if plane!='editor':return record['fields']
    module=record.get('loaded_module',{})
    if (module.get('sha256'),module.get('mvid'))!=EDITOR_CORE:
        raise bindings.LayoutError('Unity Object Editor conditional module differs')
    for field,name in zip(record['fields'][1:3],['System.Int32','System.String']):
        if (field['attributes']!=1 or bindings.type_identity(field['field_type'])!=('named','mscorlib',name) or
                bindings._attributes(field,require_string_evidence=False)!=() or
                field.get('unity_serialization_candidate') is not False):
            raise bindings.LayoutError('Unity Object Editor conditional field differs')
    return [record['fields'][0],*record['fields'][3:]]


class _Pair:
    """One profile only: no aliases, general generic substitutions or builtin list."""
    def __init__(self, left, right, left_plane, right_plane, *, color_verified, contracts=None):
        self.left, self.right = bindings._index(left), bindings._index(right)
        self.left_plane, self.right_plane = left_plane, right_plane
        self.color_verified = color_verified
        if contracts is not None and not isinstance(contracts, themecontracts.ContractProjection):
            raise bindings.LayoutError('A checked per-type contract graph is required')
        self.contracts = contracts
        self.contract_matches = []
        self.issues, self.checked, self.active = [], set(), set()

    def issue(self, reason, owner, detail=''):
        self.issues.append({'reason': reason, 'type': list(owner), 'detail': detail})

    def identity(self, left, right, owner, label):
        a, b = bindings.type_identity(left), bindings.type_identity(right)
        if a == b: return True
        if self.contracts is not None and self.contracts.identity(a) == self.contracts.identity(b):
            match={'type':list(owner),'member':label,'left_declared':a,'right_declared':b,'resolved':self.contracts.identity(a)}
            if match not in self.contract_matches:self.contract_matches.append(match)
            return True
        reason = 'type_identity_mismatch'
        if a[0] == b[0] == 'named' and a[2] == b[2] and {a[1], b[1]} in ({'mscorlib', 'netstandard'}, {'System', 'netstandard'}):
            reason = 'target_contract_scope_unverified'
        self.issue(reason, owner, label + ': ' + repr(a) + ' != ' + repr(b))
        return False

    def attributes(self, record, require_string_evidence):
        values=bindings._attributes(record, require_string_evidence=require_string_evidence)
        return values if self.contracts is None else self.contracts.attributes(values)

    def visit(self, key, as_field=False):
        if key in self.active:
            self.issue('cyclic_theme_type_graph', key); return
        if key in self.checked: return
        if key not in self.left or key not in self.right:
            self.issue('missing_theme_dependency', key); return
        self.active.add(key)
        try:
            left, right = self.left[key], self.right[key]
            # Direct loaded Reflection String arguments preserve typed nulls. The
            # boxed Object/null ambiguity is validated separately by _boxed_null;
            # retain the existing comparator's original-string evidence rule.
            lstring = self.left_plane != 'editor'
            rstring = self.right_plane != 'editor'
            lf = bindings._candidate_fields(left, require_string_evidence=lstring)
            rf = bindings._candidate_fields(right, require_string_evidence=rstring)
            for name in ('is_value_type', 'is_enum', 'is_abstract', 'declaring_type', 'unity_component', 'unity_scriptable_object'):
                if left.get(name) != right.get(name):self.issue('type_shape_mismatch', key, name)
            if _params(left) != _params(right):self.issue('generic_constraint_mismatch', key)
            if left['attributes'] != right['attributes']:self.issue('type_flags_mismatch', key)
            if self.attributes(left, lstring) != self.attributes(right, rstring):self.issue('type_attributes_mismatch', key)
            ld=_declared_fields(key,left,self.left_plane,self.left)
            rd=_declared_fields(key,right,self.right_plane,self.right)
            if [f['name'] for f in ld]!=[f['name'] for f in rd]:self.issue('declared_field_inventory_mismatch',key)
            for a,b in zip(ld,rd):
                if a['name']!=b['name']:continue
                if a['attributes']!=b['attributes'] or self.attributes(a,lstring)!=self.attributes(b,rstring):self.issue('declared_field_flags_or_attributes_mismatch',key,a['name'])
                self.identity(a['field_type'],b['field_type'],key,a['name'])
                if type(a.get('has_default_value')) is not bool or type(b.get('has_default_value')) is not bool or a['has_default_value']!=b['has_default_value']:
                    self.issue('declared_constant_presence_mismatch',key,a['name'])
                elif a['has_default_value']:
                    if a.get('default_value_complete') is not True or b.get('default_value_complete') is not True:
                        self.issue('declared_constant_incomplete',key,a['name'])
                    elif type(a.get('default_value')) is not type(b.get('default_value')) or a.get('default_value')!=b.get('default_value'):
                        self.issue('declared_constant_value_mismatch',key,a['name'])
                    self.identity(a['default_value_type'],b['default_value_type'],key,a['name']+' constant')
            if key == ASSET_REFERENCE:
                lf = _exact_asset_reference(self.left, self.left_plane == 'editor')
                rf = _exact_asset_reference(self.right, self.right_plane == 'editor')
            if key == COLOR:
                _exact_color(left); _exact_color(right)
                if not self.color_verified:self.issue('color_builtin_predicate_unverified', key)
            lb, rb = left.get('base_type'), right.get('base_type')
            if lb is None or rb is None:
                if lb != rb:self.issue('base_type_mismatch', key)
            elif lb.get('kind') == 'generic_instance' or rb.get('kind') == 'generic_instance':
                if key != ATLASED:self.issue('unsupported_theme_generic_base', key)
                else:
                    _exact_bridge(self.left); _exact_bridge(self.right)
                    self.identity(lb, rb, key, 'generic base')
                    self.visit(ASSET_GENERIC); self.visit(SPRITE, as_field=True)
            else:
                self.identity(lb, rb, key, 'base')
                if lb.get('kind') != 'named':self.issue('unsupported_theme_base', key)
                elif lb['reflection_full_name'] not in ('System.Object', 'System.ValueType', 'System.Enum', 'System.Attribute'):
                    self.visit((lb['assembly'], lb['reflection_full_name']))
            if left.get('is_enum'):
                self.identity(left['enum_underlying_type'], right['enum_underlying_type'], key, 'enum storage')
                def constants(r):
                    values=[]
                    for f in r['fields']:
                        if not f['is_const']:continue
                        if f.get('has_default_value') is not True or f.get('default_value_complete') is not True or type(f.get('default_value')) is not int:
                            raise bindings.LayoutError('Theme enum constants incomplete')
                        values.append((f['name'], f['default_value']))
                    return values
                if constants(left)!=constants(right):self.issue('enum_constants_mismatch', key)
                return
            # Sprite references store native object identity; its actual base and
            # annotations are checked, without serializing engine instance fields.
            if as_field and key == SPRITE:return
            if as_field and key not in (SPRITE, COLOR) and not left['attributes'] & 0x2000:
                self.issue('field_type_eligibility_unverified', key)
            if [f['name'] for f in lf] != [f['name'] for f in rf]:self.issue('serialized_field_order_or_names_mismatch', key)
            for a,b in zip(lf,rf):
                if a['name']!=b['name']:continue
                if a['attributes']!=b['attributes'] or self.attributes(a,lstring)!=self.attributes(b,rstring):self.issue('field_flags_or_attributes_mismatch',key,a['name'])
                if a['serialize_reference'] or b['serialize_reference']:self.issue('unsupported_theme_managed_reference',key,a['name'])
                if not self.identity(a['field_type'],b['field_type'],key,a['name']):continue
                ref=a['field_type']
                if ref['kind']!='named':self.issue('unsupported_theme_serialized_type',key,a['name']);continue
                dep=ref['assembly'],ref['reflection_full_name']
                if dep[0] in ('mscorlib','netstandard') and dep[1] in ('System.Boolean','System.Single','System.String'):continue
                self.visit(dep,as_field=True)
        except (bindings.LayoutError,KeyError,TypeError,ValueError) as error:self.issue('invalid_theme_layout',key,str(error))
        finally:self.active.remove(key);self.checked.add(key)

    def run(self):
        for key in sorted(PROFILE_TYPES):self.visit(key)
        return {'left_plane':self.left_plane,'right_plane':self.right_plane,
                'issues':sorted(self.issues,key=lambda x:json.dumps(x,sort_keys=True)),
                'checked_types':[list(k) for k in sorted(self.checked)],
                'contract_matches':self.contract_matches}


def compare_theme_planes(original, players, editor, *, schema_sha256, source_fingerprint, package_source_bytes, runtime_contract_evidence=None):
    """Evidence only. Fresh guards and target runtime resolution remain mandatory."""
    issues=[];pairs=[]
    def issue(reason, detail=''):issues.append({'reason':reason,'detail':detail})
    content_digest = None
    try:
        content_digest = require_reviewed_original_schema(original)
    except bindings.LayoutError as error:
        issue('unreviewed_original_constraint_schema', str(error))
    if type(schema_sha256) is not str or re.fullmatch(r'[a-f0-9]{64}', schema_sha256) is None:
        issue('original_schema_artifact_identity_invalid')
    if original.get('status')!='ready' or original.get('unity_version')!='2022.3.54f1' or original.get('errors')!=[]:issue('original_schema_incomplete')
    if editor.get('original_schema_sha256')!=schema_sha256:issue('loaded_inventory_original_schema_stale')
    if editor.get('status')!='ready' or editor.get('unity_version')!='2022.3.54f1' or editor.get('source_fingerprint')!=source_fingerprint or editor.get('errors')!=[]:issue('loaded_inventory_source_or_engine_stale')
    if hashlib.sha256(package_source_bytes).hexdigest()!=AA_SOURCE:issue('addressables_conditional_source_stale')
    if not isinstance(players,dict) or set(players)!=set(TARGETS):issue('missing_desktop_player_metadata');players={} if not isinstance(players,dict) else players
    color_verified=set(players)==set(TARGETS)
    for target,player in players.items():
        if (player.get('status')!='metadata-ready' or player.get('identity_status')!='complete' or player.get('source_fingerprint')!=source_fingerprint or
                player.get('target')!=target or player.get('profile')!='zone-theme-v1' or
                player.get('metadata_graph_complete') is not True or player.get('errors')!=[] or
                any(player.get(k) is not False for k in ('player_schema_verified','remap_approved','gameplay_verified','compiler_response_files_verified'))):issue('player_schema_stale_or_approved',target)
        try:
            index=bindings._index(player);color=index[COLOR]
            _exact_color(color);_boxed_null(index,'player');_exact_bridge(index);_exact_asset_reference(index,False)
            serializer=player['serializer']
            core=color.get('module_provenance',{})
            if (core.get('sha256'),core.get('mvid'))!=PLAYER_CORES[target] or core.get('assembly_name')!='UnityEngine.CoreModule':
                raise bindings.LayoutError('Genuine target Unity Core module differs')
            if serializer.get('sha256')!=SERIALIZER or serializer.get('mvid')!=SERIALIZER_MVID or color.get('builtin_serializer_eligibility') is not True or any(f.get('will_unity_serialize') is not True for f in color['fields']):
                raise bindings.LayoutError('Genuine installed Color predicate is absent')
        except (bindings.LayoutError,KeyError,TypeError,ValueError) as error:color_verified=False;issue('player_theme_profile_invalid',target+': '+str(error))
    try:
        for plane,report in [('original',original),('editor',editor)]:
            index=bindings._index(report);_boxed_null(index,plane);_exact_bridge(index);_exact_asset_reference(index,plane=='editor')
        ei=bindings._index(editor)
        for key in PROFILE_TYPES:
            r=ei[key]
            if r.get('loaded_module_identity_verified') is not True:raise bindings.LayoutError('Loaded module provenance not verified: '+repr(key))
            _module(r.get('loaded_module'))
            _module(r.get('loaded_module_reader'))
            if r['loaded_module']['assembly_identity'] != key[0]+', Version=0.0.0.0, Culture=neutral, PublicKeyToken=null':
                raise bindings.LayoutError('Theme loaded assembly full identity differs')
            reader=r['loaded_module_reader']
            if reader['sha256']!=CECIL or reader['mvid']!=CECIL_MVID or reader['assembly_identity']!='Unity.Cecil, Version=0.10.0.0, Culture=neutral, PublicKeyToken=fc15b93552389f74':
                raise bindings.LayoutError('Theme loaded module reader differs')
    except (bindings.LayoutError,KeyError,TypeError,ValueError) as error:issue('loaded_theme_profile_or_provenance_invalid',str(error))
    contracts={};contract_rows={}
    if runtime_contract_evidence is not None:
        try:
            if (not isinstance(runtime_contract_evidence,dict) or
                    runtime_contract_evidence.get('source_fingerprint')!=source_fingerprint or
                    set(runtime_contract_evidence.get('queries',{}))!=set(TARGETS)):
                raise bindings.LayoutError('Contract source or target query set incomplete')
            for target in TARGETS:
                projection=themecontracts.graph_projection(runtime_contract_evidence['metadata'],
                    runtime_contract_evidence['queries'][target],target,original,editor)
                contracts[target]=projection;contract_rows[target]=list(projection.rows.values())
        except (bindings.LayoutError,KeyError,TypeError,ValueError) as error:
            contracts={};issue('invalid_target_contract_graph',str(error))
    pairs.append(_Pair(original,editor,'original','editor',color_verified=color_verified).run())
    for target,player in players.items():
        pairs.append(_Pair(original,player,'original',target+'-player',color_verified=color_verified,contracts=contracts.get(target)).run())
        pairs.append(_Pair(player,editor,target+'-player','editor',color_verified=color_verified,contracts=contracts.get(target)).run())
    # No supplied alias map or optimistic flag grants an implicit contract rule.
    if set(contracts)!=set(TARGETS):
        issue('target_runtime_contract_projection_unverified','Actual target selection/forwarder implementation comparison must be integrated separately')
    if runtime_contract_evidence is not None:issue('runtime_contract_wrapper_freshness_not_integrated',
        'Graph proof requires the genuine receipt/query/module/reader source-sealed pre/post wrapper before any binding decision')
    return {'schema_version':1,'command':'compare-zone-theme-planes','status':'blocked',
            'original_schema_sha256':schema_sha256,'original_schema_content_sha256':content_digest,'source_fingerprint':source_fingerprint,
            'package_source_sha256':hashlib.sha256(package_source_bytes).hexdigest(),
            'target_contract_graph_status':'candidate-graph-ready' if set(contracts)==set(TARGETS) else 'unverified',
            'issues':issues,'pairs':pairs,'target_contract_rows':contract_rows,'conditional_projection':{'type':list(ASSET_REFERENCE),'field':'m_EditorAssetChanged','package':'com.unity.addressables','version':'1.22.3','source_sha256':AA_SOURCE},
            'exact_owner_paths':sorted(OWNERS),'references_modified':False,'player_schema_verified':False,'remap_approved':False,'gameplay_verified':False}


def validate_theme_owner(path, asset_bytes, meta_bytes):
    """Admit only the two pinned immutable original owners and exact byte shape."""
    if path not in OWNERS or hashlib.sha256(asset_bytes).hexdigest()!=OWNER_ASSET_SHA or hashlib.sha256(meta_bytes).hexdigest()!=OWNERS[path][1]:
        raise bindings.LayoutError('Unreviewed Theme owner/asset/metadata identity')
    matches=re.findall(rb'^guid: ([a-f0-9]{32})$',meta_bytes,re.M)
    if matches!=[OWNERS[path][0].encode()]:raise bindings.LayoutError('Theme owner GUID differs')
    shape=_theme_document_shape(asset_bytes)
    return {'path':path,'asset_guid':OWNERS[path][0],'asset_sha256':OWNER_ASSET_SHA,'meta_sha256':OWNERS[path][1],**shape}


def validate_theme_owner_set(asset_map, read_owner):
    """Require exact two authored occurrences; callback only reads owned files."""
    rows=asset_map.get('assets')
    if (not isinstance(rows,list) or any(not isinstance(r,dict) or
            not isinstance(r.get('script_references'),list) or
            any(not isinstance(v,str) for v in r['script_references']) for r in rows)):
        raise bindings.LayoutError('Theme owner inventory missing or malformed')
    selected=[r for r in rows if OWNER_SCRIPT in r.get('script_references',[])]
    if len(selected)!=2 or {r.get('path') for r in selected}!=set(OWNERS):
        raise bindings.LayoutError('Theme owner closure is not exactly the two reviewed assets')
    for r in selected:
        path=r['path']
        if r.get('code_quarantined') is not False or r.get('guid')!=OWNERS[path][0] or sum(v.get('guid')==r['guid'] for v in rows)!=1:
            raise bindings.LayoutError('Theme owner GUID/asset-map identity ambiguous')
    result=[]
    for r in sorted(selected,key=lambda x:x['path']):
        path=r['path']
        asset,meta=read_owner(path)
        result.append(validate_theme_owner(path,asset,meta))
    return {'owners':result,'owner_count':2,'references_modified':False,'remap_approved':False}


def _theme_document_shape(content):
    # This grammar is independent of the scalar-only existing rules. It cannot
    # open arbitrary nested YAML, quoted blocks, tags, anchors or extra fields.
    text=content.decode('utf-8')
    pattern=(r'%YAML 1\.1\n%TAG !u! tag:unity3d\.com,2011:\n--- !u!114 &(?P<id>-?[0-9]{1,20})\nMonoBehaviour:\n'
      r'  m_ObjectHideFlags: (?P<flags>[0-9]+)\n  m_CorrespondingSourceObject: \{fileID: 0\}\n  m_PrefabInstance: \{fileID: 0\}\n  m_PrefabAsset: \{fileID: 0\}\n  m_GameObject: \{fileID: 0\}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
      r'  m_Script: \{fileID: 11500000, guid: '+OWNER_SCRIPT+r', type: 3\}\n  m_Name: [A-Za-z_][A-Za-z_0-9]*\n  m_EditorClassIdentifier:\n'
      r'  m_zoneAccentOverride:\n    m_AssetGUID: (?P<asset>[a-f0-9]{32})\n    m_SubObjectName: (?P<subname>[A-Za-z_][A-Za-z_0-9]*)\n    m_SubObjectType: UnityEngine\.Sprite, UnityEngine\.CoreModule, Version=0\.0\.0\.0, Culture=neutral, PublicKeyToken=null\n'
      r'  m_zoneGradientOverride: (?P<gradient>[01])\n  m_backgroundStartColour: \{r: (?P<sr>[^,\n]+), g: (?P<sg>[^,\n]+), b: (?P<sb>[^,\n]+), a: (?P<sa>[^}\n]+)\}\n'
      r'  m_backgroundEndColour: \{r: (?P<er>[^,\n]+), g: (?P<eg>[^,\n]+), b: (?P<eb>[^,\n]+), a: (?P<ea>[^}\n]+)\}\n')
    m=re.fullmatch(pattern,text)
    if m is None:raise bindings.LayoutError('Unsupported exact Theme authored document shape')
    local=int(m['id'])
    if not -(1<<63)<=local<(1<<63) or local==0:raise bindings.LayoutError('Theme objectID out of signed64 bounds')
    bits={}
    for prefix in ('s','e'):
        values=[]
        for component in 'rgba':
            raw=m[prefix+component]
            if not re.fullmatch(r'-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?',raw):raise bindings.LayoutError('Theme color component encoding unsupported')
            try:
                value=float(raw)
                if not math.isfinite(value):raise ValueError('nonfinite')
                values.append(struct.pack('<f',value).hex())
            except (ValueError,OverflowError) as error:raise bindings.LayoutError('Theme color exceeds finite float32 storage') from error
        bits[prefix]=values
    return {'object_id':local,'accent_asset_guid':m['asset'],'accent_subobject_name':m['subname'],'gradient':m['gradient']=='1','color_float32_bits':bits}
