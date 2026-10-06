"""A bounded metadata proof for the ZoneTheme contract references only.

The caller must independently seal the genuine player receipt, source, installed
reader, and every module before and after this comparison. This pure graph check
does not select a runtime, load a player DLL, authorize a remap, or build a game.
"""
import re
from . import bindings

NETSTANDARD = 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
MSCORLIB = 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
SYSTEM = 'System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
CECIL = '98ae4d35b3f91f017b1babab876d2bf4594af3d65e7e777fbd37fd1e4124f95e'
CECIL_MVID = 'a6860a9f-6366-4373-87eb-dc1f225b7fd4'
TARGET_NAMES = {'macos':'StandaloneOSX','windows':'StandaloneWindows64','linux':'StandaloneLinux64'}
QUERY_METHODS = [
 {'attributes':147,'declaring_type':'UnityEditor.BuildPipeline','implementation_attributes':4096,
  'method':'GetMonoRuntimeLibDirectory','parameter_type':'UnityEditor.BuildTarget','return_type':'System.String','token':'0x06002264'},
 {'attributes':147,'declaring_type':'UnityEditor.BuildPipeline','implementation_attributes':4096,
  'method':'CompatibilityProfileToClassLibFolder','parameter_type':'UnityEditor.ApiCompatibilityLevel','return_type':'System.String','token':'0x06002265'},
 {'attributes':150,'declaring_type':'UnityEditor.BuildTargetDiscovery','implementation_attributes':4096,
  'method':'GetPlatformProfileSuffix','parameter_type':'UnityEditor.BuildTarget','return_type':'System.String','token':'0x0600237b'},
]
REFERENCE_PIN = ('79a49049c360a3ff282e747c311fcd567834efde564a96689872564164756e84', '5a41d6b7-1898-42ec-a409-fd0b1c3e3dcf')
# These are observed, separately sealed target modules in Unity 2022.3.54f1.
# Equal assembly identities never imply equal file contents or interchangeable paths.
TARGET_PINS = {
 'macos': ('macos',
  ('bf0b7eac9010b75413e4ec1a07a3453bc6a68eeb8e916fbed7c1f2777841f7c7','533c8f5e-aecb-48be-a6db-4871cac0580f'),
  ('bef14152cf8b584376f07f6c55b406b04a70aeb7350561cf13aa7f773b7c70b9','12733c37-0fc2-48cd-a92d-d08e70d32db2'),
  ('72fc78844574884648eea4086d70decc1f65188bbd8f5ba15c4dcd56a1413a9c','f6d88e58-e037-4b48-afaa-d9bca9a5d099')),
 'windows': ('win32',
  ('6ae62e082dc494a2433984177f60ca4db5fae69b1f360a8b33754172b310b8c5','3484a94e-547c-4c6b-a5ba-7f3667acc005'),
  ('2bd024c77883e82a2723a40c0063c99eebe8d2445654feb71c1f7e16e1e01bfa','819cf46d-02f7-4ba4-ae2d-757a73aa5ae9'),
  ('35c6fbd5d1ad8abd2bbf3aab20ea37defae61c313b8292cd4509e0397c1bf4b3','78bbf10b-ce14-48b2-94d4-7e9196b11281')),
 'linux': ('linux',
  ('bf0b7eac9010b75413e4ec1a07a3453bc6a68eeb8e916fbed7c1f2777841f7c7','533c8f5e-aecb-48be-a6db-4871cac0580f'),
  ('a20abd0382915c83e87c5691e6f908ac4a3019f514ba8310ffaf5e458505c92b','c166a39b-8a00-41bd-ab59-71af387f745c'),
  ('8a561cb424bbf24367302012c147e3f3807110deb5e1bf59a327735f60f64ce3','a3b8b31c-f150-46b4-80a3-973e133de60e')),
}
CONTRACTS = {n: MSCORLIB for n in (
 'System.Attribute','System.AttributeTargets','System.AttributeUsageAttribute',
 'System.Boolean','System.Diagnostics.DebuggerBrowsableAttribute',
 'System.Diagnostics.DebuggerBrowsableState','System.Enum','System.FlagsAttribute',
 'System.Int32','System.IntPtr','System.Object','System.Reflection.DefaultMemberAttribute',
 'System.Runtime.CompilerServices.CompilerGeneratedAttribute',
 'System.Runtime.CompilerServices.IsReadOnlyAttribute','System.Single','System.String',
 'System.ValueType','System.Void')}
CONTRACTS.update({n:SYSTEM for n in (
 'System.ComponentModel.EditorBrowsableAttribute','System.ComponentModel.EditorBrowsableState')})
ENUMS = {
 'System.AttributeTargets': {'Assembly':1,'Module':2,'Class':4,'Struct':8,'Enum':16,
  'Constructor':32,'Method':64,'Property':128,'Field':256,'Event':512,'Interface':1024,
  'Parameter':2048,'Delegate':4096,'ReturnValue':8192,'GenericParameter':16384,'All':32767},
 'System.ComponentModel.EditorBrowsableState': {'Always':0,'Never':1,'Advanced':2},
 'System.Diagnostics.DebuggerBrowsableState': {'Never':0,'Collapsed':2,'RootHidden':3},
}


def _one(rows, key, value):
    if not isinstance(rows,list) or any(not isinstance(r,dict) for r in rows):
        raise bindings.LayoutError('Contract graph rows are incomplete')
    selected=[r for r in rows if r.get(key)==value]
    if len(selected)!=1:raise bindings.LayoutError('Contract graph identity is missing or duplicated')
    return selected[0]


def _module(rows,path,pin,assembly):
    m=_one(rows,'path',path)
    if (m.get('sha256'),m.get('mvid'))!=pin or m.get('assembly')!=assembly:
        raise bindings.LayoutError('Unreviewed target contract module')
    if not isinstance(m.get('defined_types'),list) or not isinstance(m.get('forwarders'),list):
        raise bindings.LayoutError('Target contract module graph incomplete')
    return m


def _enum_shape(record,original=False):
    name=record.get('full_name') if original else record.get('name')
    if name not in ENUMS:return
    if record.get('is_enum') is not True:raise bindings.LayoutError('Contract enum classification differs')
    fields=record.get('fields')
    storage=_one(fields,'name','value__')
    ref=storage.get('field_type') if original else storage.get('type')
    if (not isinstance(ref,dict) or ref.get('reflection_full_name' if original else 'full_name')!='System.Int32' or
        storage.get('attributes')!=1542):raise bindings.LayoutError('Contract enum storage differs')
    values={}
    for f in fields:
        if not isinstance(f,dict):raise bindings.LayoutError('Contract enum graph malformed')
        constant=f.get('has_default_value') if original else f.get('has_constant')
        if type(constant) is not bool:raise bindings.LayoutError('Contract enum constant evidence incomplete')
        if not constant:continue
        value=f.get('default_value') if original else f.get('constant')
        if (type(value) is not int or f.get('name') in values or f.get('attributes')!=32854 or
            original and f.get('default_value_complete') is not True):
            raise bindings.LayoutError('Contract enum constant evidence differs')
        values[f['name']]=value
    if values!=ENUMS[name]:raise bindings.LayoutError('Contract enum constants differ')


class ContractProjection:
    """Per-type reference endpoints, never an assembly or namespace alias."""
    def __init__(self,rows,target):
        self.rows={r['name']:r for r in rows};self.target=target
        if set(self.rows)!=set(CONTRACTS) or len(rows)!=len(CONTRACTS):
            raise bindings.LayoutError('Contract projection must contain the exact twenty rows')

    def identity(self,identity):
        if identity[0]=='named' and identity[2] in self.rows:
            endpoint=CONTRACTS[identity[2]].split(',')[0]
            if identity[1] in ('netstandard',endpoint):
                return ('named',endpoint,identity[2])
        if identity[0]=='generic_instance':
            return (*identity[:3],tuple(self.identity(v) for v in identity[3]))
        return identity

    def value(self,value):
        kind=value[0]
        if kind=='enum':return (kind,self.identity(value[1]),self.value(value[2]))
        if kind=='type':return (kind,None if value[1] is None else self.identity(value[1]))
        if kind=='array':return (kind,value[1],None if value[2] is None else self.identity(value[2]),
                                None if value[3] is None else tuple(self.value(v) for v in value[3]))
        return value

    def attributes(self,attributes):
        result=[]
        for assembly,name,args,named in attributes:
            endpoint=self.identity(('named',assembly,name))
            result.append((endpoint[1],name,tuple(self.value(v) for v in args),
                           tuple((group,n,self.value(v)) for group,n,v in named)))
        return tuple(sorted(result,key=repr))


def graph_projection(metadata,query,target,original,editor):
    """Check a sealed installed-Cecil graph; caller retains all outer freshness gates.

    The native query identifies one Mono library directory. CompatibilityProfile's
    AOT folder is preserved as evidence and never used as a replacement directory.
    Returned rows establish metadata reference resolution only.
    """
    if target not in TARGET_PINS:raise bindings.LayoutError('Unsupported contract target')
    suffix,facade_pin,core_pin,system_pin=TARGET_PINS[target]
    expected={'target':TARGET_NAMES[target],'unity_version':'2022.3.54f1','api_compatibility_value':6,
      'api_compatibility_name':'NET_Standard_2_0','scripting_backend_value':0,
      'scripting_backend_name':'Mono2x','platform_profile_suffix':suffix,
      'compatibility_profile_folder':'unityaot-'+suffix,
      'runtime_selection_verified':False,'layout_approved':False,
      'gameplay_verified':False,'runtime_assemblies_loaded':False,'build_player_called':False}
    if (not isinstance(query,dict) or any(type(query.get(k)) is not type(v) or query.get(k)!=v for k,v in expected.items()) or
        query.get('module_sha256')!='fb89c4764167359e3796b9a7e893f507d508f86eb8ad7daa55c78bf07abcd0b9' or
        query.get('module_mvid')!='70a991b8-3ee9-4c31-8ecd-89fa65e3f64c' or
        query.get('module_assembly')!='UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' or
        query.get('methods')!=QUERY_METHODS):
        raise bindings.LayoutError('Genuine native target query is missing or changed')
    core_path=query.get('module_path')
    ending='/Managed/UnityEngine/UnityEditor.CoreModule.dll'
    if not isinstance(core_path,str) or not core_path.endswith(ending):
        raise bindings.LayoutError('Native query engine location unsupported')
    contents=core_path[:-len(ending)]
    directory=contents+'/MonoBleedingEdge/lib/mono/unityjit-'+suffix
    if query.get('mono_runtime_lib_directory')!=directory:
        raise bindings.LayoutError('Native-selected Mono directory differs')
    if (not isinstance(metadata,dict) or metadata.get('reader_sha256')!=CECIL or metadata.get('reader_mvid')!=CECIL_MVID or
        any(metadata.get(k) is not False for k in ('player_clr_loads','editor_invoked','runtime_verified','layout_approved'))):
        raise bindings.LayoutError('Installed metadata reader proof incomplete')
    modules=metadata.get('modules')
    reference=_module(modules,contents+'/NetStandard/ref/2.1.0/netstandard.dll',REFERENCE_PIN,NETSTANDARD)
    facade=_module(modules,directory+'/Facades/netstandard.dll',facade_pin,NETSTANDARD)
    endpoints={MSCORLIB:_module(modules,directory+'/mscorlib.dll',core_pin,MSCORLIB),
               SYSTEM:_module(modules,directory+'/System.dll',system_pin,SYSTEM)}
    if reference['forwarders'] or facade['defined_types']:
        raise bindings.LayoutError('Reference/facade roles differ')
    if {r.get('name') for r in facade['forwarders']}!=set(CONTRACTS) or len(facade['forwarders'])!=len(CONTRACTS):
        raise bindings.LayoutError('Forwarder proof must cover exactly twenty contracts')
    oi,ei=bindings._index(original),bindings._index(editor)
    rows=[]
    for name,scope in sorted(CONTRACTS.items()):
        ref=_one(reference['defined_types'],'name',name)
        f=_one(facade['forwarders'],'name',name)
        endpoint=endpoints[scope];definition=_one(endpoint['defined_types'],'name',name)
        if (f.get('scope')!=scope or f.get('is_forwarder') is not True or f.get('attributes')!=0x200000 or
            f.get('declaring_type') is not None or not re.fullmatch(r'0x27[a-f0-9]{6}',f.get('token','')) or
            not re.fullmatch(r'0x02[a-f0-9]{6}',ref.get('token','')) or
            not re.fullmatch(r'0x02[a-f0-9]{6}',definition.get('token',''))):
            raise bindings.LayoutError('Target type forwarder or endpoint differs')
        if (type(ref.get('attributes')) is not int or type(definition.get('attributes')) is not int or
            (ref['attributes'] & 0x139)!=(definition['attributes'] & 0x139) or
            type(ref.get('is_enum')) is not bool or ref['is_enum']!=definition.get('is_enum') or
            ref.get('is_enum')!=(name in ENUMS)):
            raise bindings.LayoutError('Contract endpoint public type classification differs')
        ref_base,endpoint_base=ref.get('base_type'),definition.get('base_type')
        if (not isinstance(ref_base,dict) or not isinstance(endpoint_base,dict) or
            ref_base.get('full_name')!=endpoint_base.get('full_name')):
            raise bindings.LayoutError('Contract endpoint base type differs')
        if name in ENUMS:
            _enum_shape(ref);_enum_shape(definition)
            key=(scope.split(',')[0],name)
            _enum_shape(oi[key],True);_enum_shape(ei[key],True)
        rows.append({'name':name,'declared_scope':NETSTANDARD,'reference_path':reference['path'],
          'reference_sha256':reference['sha256'],'reference_mvid':reference['mvid'],'reference_type_token':ref['token'],
          'facade_path':facade['path'],'facade_sha256':facade['sha256'],'facade_mvid':facade['mvid'],
          'forwarder_token':f['token'],'forwarder_target_scope':scope,
          'endpoint_path':endpoint['path'],'endpoint_sha256':endpoint['sha256'],'endpoint_mvid':endpoint['mvid'],
          'endpoint_type_token':definition['token'],'enum_constants_verified':name in ENUMS})
    return ContractProjection(rows,target)
