using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class GlyphStartupVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original glyph startup verification: " + label); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Set(object value, string name, object data) { Field(value.GetType(), name).SetValue(value, data); }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void Throws<T>(Action action, string label) where T : Exception { bool thrown = false; try { action(); } catch (T) { thrown = true; } Check(thrown, label); }
        private static GameActionGlyphMap RawGameMap()
        {
            var value = Raw<GameActionGlyphMap>(); Set(value,"m_cachedLookup",new Dictionary<GameAction,Texture2D>()); return value;
        }
        private static BaseGlyphMap<T> RawInputMap<T>(Texture2D missing = null)
        {
            var value = Raw<BaseGlyphMap<T>>(); Set(value,"m_missingGlyph",missing); Set(value,"m_inputTypeDictionary",new Dictionary<InputType,Dictionary<T,Texture2D>>(HardlightInputEnumComparers.InputTypeComparer)); return value;
        }
        private static Dictionary<InputType,Dictionary<T,Texture2D>> Cache<T>(BaseGlyphMap<T> map) => (Dictionary<InputType,Dictionary<T,Texture2D>>)Field(typeof(BaseGlyphMap<T>),"m_inputTypeDictionary").GetValue(map);
        private static BaseGlyphMap<T>.MappedGlyphs Group<T>(InputType input, params BaseGlyphMap<T>.KeyGlyphMap[] keys) => new BaseGlyphMap<T>.MappedGlyphs { InputType = input, GlyphMaps = keys == null ? null : new List<BaseGlyphMap<T>.KeyGlyphMap>(keys) };
        private static BaseGlyphMap<T>.KeyGlyphMap Key<T>(T key, Texture2D glyph) => new BaseGlyphMap<T>.KeyGlyphMap { Key = key, Glyph = glyph };
        private static void Declarations()
        {
            foreach (var row in new[] {
                (Type:typeof(GameActionGlyphMap), Names:new[]{"m_actionToGlyphMap","m_inputType","m_cachedLookup"}),
                (Type:typeof(BaseGlyphMap<>), Names:new[]{"m_missingGlyph","m_inputGlyphs","m_inputTypeDictionary"}),
                (Type:typeof(BaseGlyphMap<>.KeyGlyphMap), Names:new[]{"Name","Key","Glyph"}),
                (Type:typeof(BaseGlyphMap<>.MappedGlyphs), Names:new[]{"Name","InputType","GlyphMaps"}),
                (Type:typeof(HardlightInputEnumComparers), Names:new[]{"InputTypeComparer","KeyCodeComparer","SwipeScreenZoneComparer"}) })
            {
                Check(row.Type.GetFields(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).OrderBy(f=>f.MetadataToken).Select(f=>f.Name).SequenceEqual(row.Names),row.Type.Name+" full original field order");
                Check((row.Type.Attributes & TypeAttributes.BeforeFieldInit) != 0,row.Type.Name+" original BeforeFieldInit");
            }
            Check(typeof(GameActionGlyphMap).Assembly.GetName().Name == "Game.Runtime" && typeof(BaseGlyphMap<>).Assembly.GetName().Name == "HLInput.Runtime","original two assembly identities");
            Check(typeof(GameActionGlyphMap).BaseType == typeof(ScriptableObject) && typeof(BaseGlyphMap<>).BaseType == typeof(ScriptableObject),"both original real ScriptableObject bases");
            Check(typeof(BaseGlyphMap<>).GetInterfaces().Length == 0 && typeof(GameActionGlyphMap).GetInterfaces().Length == 0,"no invented glyph source interfaces");
            foreach (Type type in new[]{typeof(BaseGlyphMap<>),typeof(BaseGlyphMap<>.KeyGlyphMap),typeof(BaseGlyphMap<>.MappedGlyphs)})
                Check(type.GetGenericArguments().All(p=>p.GenericParameterAttributes==GenericParameterAttributes.None && p.GetGenericParameterConstraints().Length==0),type.Name+" complete original unconstrained key parameters");
            foreach (Type type in new[]{typeof(BaseGlyphMap<>.KeyGlyphMap),typeof(BaseGlyphMap<>.MappedGlyphs)})
            {
                Check(type.IsSerializable && type.GetInterfaces().SequenceEqual(new[]{typeof(ISerializationCallbackReceiver)}),type.Name+" genuine serialized callback contract");
                Check(Field(type,"Name").IsDefined(typeof(HideInInspector),false),type.Name+" original hidden public Name");
            }
            Check(Field(typeof(GameActionGlyphMap),"m_cachedLookup").IsInitOnly && Field(typeof(BaseGlyphMap<>),"m_inputTypeDictionary").IsInitOnly,"both original retained readonly caches");
            Check(Field(typeof(BaseGlyphMap<>),"m_inputGlyphs").IsFamily && Field(typeof(BaseGlyphMap<>),"m_missingGlyph").IsPrivate,"original protected input list and private fallback");
            var tooltip = Field(typeof(BaseGlyphMap<>),"m_missingGlyph").GetCustomAttribute<TooltipAttribute>();
            Check(tooltip != null && tooltip.tooltip == "Glyph to show when an input, or a key is missing.","original fallback tooltip");
            var menu = typeof(GameActionGlyphMap).GetCustomAttribute<CreateAssetMenuAttribute>();
            Check(menu != null && menu.fileName == "GameActionGlyphMap" && menu.menuName == "HardlightProject/DefinitionData/Definitions/GameActionGlyphMap","complete original authoring menu");
            foreach(Type type in new[]{typeof(GameActionGlyphMap),typeof(BaseGlyphMap<>)})
                Check(type.GetCustomAttributes<Il2CppSetOptionAttribute>().Select(a=>a.Option).SequenceEqual(new[]{Option.NullChecks,Option.ArrayBoundsChecks}),type.Name+" original class option order");
            foreach(Type type in new[]{typeof(BaseGlyphMap<>.KeyGlyphMap),typeof(BaseGlyphMap<>.MappedGlyphs)})
                Check(type.GetCustomAttributes<Il2CppSetOptionAttribute>().Select(a=>a.Option).SequenceEqual(new[]{Option.ArrayBoundsChecks,Option.NullChecks}),type.Name+" original callback class option order");
            Check(typeof(KeyCodeEqualityComparer).IsSealed && !typeof(InputTypeEqualityComparer).IsSealed && !typeof(SwipeScreenZoneEqualityComparer).IsSealed,"original comparer sealed distinction");
            var fields = typeof(HardlightInputEnumComparers).GetFields(BindingFlags.Public|BindingFlags.Static).OrderBy(f=>f.MetadataToken).ToArray();
            Check(fields.Length == 3 && fields.All(f=>f.IsInitOnly),"whole original three readonly input comparer fields");
            foreach(FieldInfo f in fields)
            {
                object comparer=f.GetValue(null); Type type=f.FieldType; var equals=type.GetMethod("Equals",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);var hash=type.GetMethod("GetHashCode",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);Type enumType=hash.GetParameters()[0].ParameterType;
                object low=Enum.ToObject(enumType,int.MinValue),high=Enum.ToObject(enumType,int.MaxValue);
                Check(comparer != null && comparer.GetType()==type,f.Name+" genuine allocation");
                Check((bool)equals.Invoke(comparer,new[]{low,low}) && !(bool)equals.Invoke(comparer,new[]{low,high}),f.Name+" raw enum equality");
                Check((int)hash.Invoke(comparer,new[]{low})==int.MinValue && (int)hash.Invoke(comparer,new[]{high})==int.MaxValue,f.Name+" signed raw hash bits");
            }
            var expected=new Dictionary<string,int>{{"DefaultController",897719803},{"Keyboard",-1162288346},{"MfiController",-594121215},{"Mouse",-1482781790},{"NpadController",-1000566503},{"PS4Controller",-1901056579},{"PS5Controller",1329525373},{"Remote",1437681611},{"Touch",-31649603},{"Unknown",1838982690},{"Unsupported",1208867107},{"XboneController",-1984142849}};
            Check(Enum.GetNames(typeof(InputType)).Length==expected.Count && Enum.GetUnderlyingType(typeof(InputType))==typeof(int),"full original hashed InputType enum");
            foreach(var pair in expected) Check((int)(InputType)Enum.Parse(typeof(InputType),pair.Key)==pair.Value,"InputType."+pair.Key+" exact original value");
            Check((int)SwipeScreenZone.None==0 && (int)SwipeScreenZone.Custom==-92066078 && (int)SwipeScreenZone.Left==-1290930909 && (int)SwipeScreenZone.Right==-1134475173 && Enum.GetNames(typeof(SwipeScreenZone)).Length==4,"complete original SwipeScreenZone values");
            Check(typeof(StringGlyphMap).Assembly.GetName().Name == "HLInput.Runtime", "original concrete string map assembly");
            Check(typeof(StringGlyphMap).BaseType == typeof(BaseGlyphMap<string>) && !typeof(StringGlyphMap).IsAbstract && !typeof(StringGlyphMap).IsGenericType && !typeof(StringGlyphMap).IsSealed, "complete original concrete string map base and flags");
            Check(typeof(StringGlyphMap).GetFields(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Length == 0, "original concrete map has no own fields");
            Check(typeof(StringGlyphMap).GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Length == 0 && typeof(StringGlyphMap).GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Length == 1, "complete original one constructor and no other members");
            Check((typeof(StringGlyphMap).Attributes & TypeAttributes.BeforeFieldInit) != 0 && typeof(StringGlyphMap).GetInterfaces().Length == 0, "original concrete before-field-init and no interfaces");
            var concreteOptions = typeof(StringGlyphMap).GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(concreteOptions.Select(a=>a.Option).SequenceEqual(new[]{Option.NullChecks,Option.ArrayBoundsChecks}) && concreteOptions.All(a=>Equals(a.Value,false)), "original concrete own option order and values");
            var concreteMenu = typeof(StringGlyphMap).GetCustomAttribute<CreateAssetMenuAttribute>(false);
            Check(concreteMenu != null && concreteMenu.menuName == "Hardlight/HLInput/GameInputGlyphMaps/Create StringGlyphMap" && concreteMenu.fileName == null && concreteMenu.order == 0, "original concrete menu with unassigned filename/order");
            // Ordinary C# preserving the original public nonsealed type flags
            // generates one public constructor absent from supplied metadata.
            // This is a tracked compiler frontier, not an original body claim.
            Check(typeof(HardlightInputEnumComparers).GetConstructor(Type.EmptyTypes)!=null && !typeof(HardlightInputEnumComparers).IsSealed,"explicit unapproved natural registry constructor frontier");
        }
        private static void SerializedCallbacks()
        {
            var key=Key(17,(Texture2D)null); key.Name="stale";key.OnBeforeSerialize();Check(key.Name=="17" && key.Key==17,"before callback derives name from integer key");
            key.Key=-29;key.OnAfterDeserialize();Check(key.Name=="-29" && key.Key==-29,"after callback derives name without parsing prior text");
            var enumKey=Key(GameAction.CharacterJump,(Texture2D)null);enumKey.OnBeforeSerialize();Check(enumKey.Name==GameAction.CharacterJump.ToString(),"real game-action enum callback");
            var missing=Key((string)null,(Texture2D)null);missing.Name="retain";Throws<NullReferenceException>(missing.OnBeforeSerialize,"before null reference key is not normalized");Check(missing.Name=="retain","failed before conversion retains old name");
            Throws<NullReferenceException>(missing.OnAfterDeserialize,"after null reference key is not normalized");Check(missing.Name=="retain","failed after conversion retains old name");
            var mapped=Group<int>(InputType.Keyboard,(BaseGlyphMap<int>.KeyGlyphMap[])null);mapped.Name="stale";mapped.OnBeforeSerialize();Check(mapped.Name=="Keyboard" && mapped.GlyphMaps==null,"before input callback does not create a list");
            mapped.InputType=(InputType)17;mapped.OnAfterDeserialize();Check(mapped.Name=="17" && mapped.GlyphMaps==null,"after input callback preserves unknown numeric value");
            var reflected = typeof(BaseGlyphMap<int>.KeyGlyphMap).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly);
            Check(reflected.Length==2 && reflected.All(m=>m.IsVirtual && m.IsFinal && (m.Attributes&MethodAttributes.NewSlot)!=0),"real callback interface implementations retain final/virtual/newslot");
        }
        private static void GameLookup()
        {
            var map=RawGameMap();var first=Raw<Texture2D>();var second=Raw<Texture2D>();var initialCache=Field(typeof(GameActionGlyphMap),"m_cachedLookup").GetValue(map);
            var list=new List<BaseGlyphMap<GameAction>.KeyGlyphMap>{Key(GameAction.CharacterJump,first),Key(GameAction.CharacterJump,second),Key(GameAction.CharacterMovementHorizontal,(Texture2D)null)};Set(map,"m_actionToGlyphMap",list);Set(map,"m_inputType",InputType.PS5Controller);
            map.Initialise();Check(map.InputType==InputType.PS5Controller,"direct original input type getter");Texture2D glyph;
            Check(map.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,first),"first duplicate game action wins");
            Check(map.TryGetGlyph(GameAction.CharacterMovementHorizontal,out glyph) && ReferenceEquals(glyph,null),"present null game glyph reports true");
            Check(!map.TryGetGlyph((GameAction)int.MinValue,out glyph) && ReferenceEquals(glyph,null),"unknown game action resets output and reports false");
            list.RemoveAt(0);map.Initialise();Check(map.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,second),"reinitialization clears previous first choice");
            Check(ReferenceEquals(initialCache,Field(typeof(GameActionGlyphMap),"m_cachedLookup").GetValue(map)),"initialization retains cache identity");
            Set(map,"m_actionToGlyphMap",null);Throws<NullReferenceException>(map.Initialise,"null authored game list fails after clear");Check(!map.TryGetGlyph(GameAction.CharacterJump,out glyph),"clear precedes failing game enumerator acquisition");
            Set(map,"m_actionToGlyphMap",new List<BaseGlyphMap<GameAction>.KeyGlyphMap>{Key(GameAction.CharacterJump,first),null,Key(GameAction.CharacterMovementHorizontal,second)});Throws<NullReferenceException>(map.Initialise,"null game key row is not skipped");
            Check(map.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,first) && !map.TryGetGlyph(GameAction.CharacterMovementHorizontal,out glyph),"game lookup keeps earlier partial inserts on failure");
            Set(map,"m_actionToGlyphMap",list);typeof(GameActionGlyphMap).GetMethod("OnValidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(map,null);Check(map.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,second),"validation hook uses original initialization");
            map.Shutdown();Check(!map.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(initialCache,Field(typeof(GameActionGlyphMap),"m_cachedLookup").GetValue(map)),"shutdown clears retained cache");Check(ReferenceEquals(list,Field(typeof(GameActionGlyphMap),"m_actionToGlyphMap").GetValue(map)),"shutdown retains authored game list");
        }
        private static void InputLookup()
        {
            var fallback=Raw<Texture2D>();var first=Raw<Texture2D>();var second=Raw<Texture2D>();var map=RawInputMap<string>(fallback);var cache=Cache(map);
            var groups=new List<BaseGlyphMap<string>.MappedGlyphs>{Group(InputType.Keyboard,Key("A",first),Key("N",(Texture2D)null))};Set(map,"m_inputGlyphs",groups);map.Initialise();
            Check(ReferenceEquals(cache.Comparer,HardlightInputEnumComparers.InputTypeComparer),"genuine input comparer identity");
            Check(map.ContainsGlyphsForInput(InputType.Keyboard) && !map.ContainsGlyphsForInput(InputType.Mouse),"contains tests only input dictionary key");
            Check(ReferenceEquals(map.GetGlyphForKeyAndInputType("A",InputType.Keyboard),first),"known input/key uses mapped glyph");
            Check(ReferenceEquals(map.GetGlyphForKeyAndInputType("N",InputType.Keyboard),null),"present null glyph does not use fallback");
            Check(ReferenceEquals(map.GetGlyphForKeyAndInputType("missing",InputType.Keyboard),fallback),"missing key uses original fallback");
            Check(ReferenceEquals(map.GetGlyphForKeyAndInputType(null,InputType.Mouse),fallback),"missing input never looks up null generic key");
            Throws<ArgumentNullException>(()=>map.GetGlyphForKeyAndInputType(null,InputType.Keyboard),"present input propagates null key failure");
            Throws<ArgumentException>(map.Initialise,"generic reinitialization does not clear old entries");Check(ReferenceEquals(map.GetGlyphForKeyAndInputType("A",InputType.Keyboard),first),"duplicate input failure retains previous glyph");
            map.Shutdown();Check(ReferenceEquals(Cache(map),cache) && cache.Count==0,"generic shutdown clears same dictionary");Check(ReferenceEquals(Field(typeof(BaseGlyphMap<string>),"m_inputGlyphs").GetValue(map),groups),"generic shutdown retains authored rows");
            groups.Clear();groups.Add(Group(InputType.Keyboard,Key("A",first),Key("A",second)));Throws<ArgumentException>(map.Initialise,"duplicate generic key throws rather than replaces");Check(cache.Count==1 && ReferenceEquals(cache[InputType.Keyboard]["A"],first),"duplicate generic key retains partial first insertion");
            map.Shutdown();groups.Clear();groups.Add(Group<string>(InputType.Touch,(BaseGlyphMap<string>.KeyGlyphMap[])null));Throws<NullReferenceException>(map.Initialise,"null glyph list fails after input insertion");Check(map.ContainsGlyphsForInput(InputType.Touch) && cache[InputType.Touch].Count==0,"empty input dictionary published before null inner list");
            groups.Clear();groups.Add(Group(InputType.Mouse,Key("B",second)));groups.Add(Group(InputType.Touch,Key("C",first)));Throws<ArgumentException>(map.Initialise,"later existing input preserves newly inserted earlier group");Check(cache[InputType.Mouse].Count==1 && ReferenceEquals(cache[InputType.Mouse]["B"],second),"earlier new input insertion retained before duplicate failure");
            Set(map,"m_inputGlyphs",null);Throws<NullReferenceException>(map.Initialise,"null outer list does not clear existing dictionaries");Check(cache.Count==2,"generic failing list retains previous two input dictionaries");
            map.Shutdown();Set(map,"m_inputGlyphs",new List<BaseGlyphMap<string>.MappedGlyphs>{Group(InputType.Mouse,Key("B",second)),null});Throws<NullReferenceException>(map.Initialise,"null input row is not skipped");Check(cache.Count==1 && ReferenceEquals(cache[InputType.Mouse]["B"],second),"generic earlier inserts survive null later row");
            map.Shutdown();Set(map,"m_inputGlyphs",new List<BaseGlyphMap<string>.MappedGlyphs>{Group<string>(InputType.Keyboard)});map.Initialise();Check(map.ContainsGlyphsForInput(InputType.Keyboard) && ReferenceEquals(map.GetGlyphForKeyAndInputType("A",InputType.Keyboard),fallback),"empty authored input still counts as present and falls back for key");
            Set(map,"m_missingGlyph",null);Check(ReferenceEquals(map.GetGlyphForKeyAndInputType("A",InputType.Keyboard),null),"null fallback remains null");
        }
        private sealed class HashCallbackKey
        {
            public Action BeforeHash;
            public override int GetHashCode() { Action callback=BeforeHash;BeforeHash=null;callback?.Invoke();return 7; }
            public override bool Equals(object other) { return ReferenceEquals(this,other); }
        }
        private static void RetainedReceiverAndReload()
        {
            var map=RawInputMap<HashCallbackKey>();var oldCache=Cache(map);var newInner=new Dictionary<HashCallbackKey,Texture2D>();var newCache=new Dictionary<InputType,Dictionary<HashCallbackKey,Texture2D>>(HardlightInputEnumComparers.InputTypeComparer){{InputType.Keyboard,newInner}};var first=new HashCallbackKey();var second=new HashCallbackKey();var glyph=Raw<Texture2D>();
            first.BeforeHash=()=>Set(map,"m_inputTypeDictionary",newCache);
            Set(map,"m_inputGlyphs",new List<BaseGlyphMap<HashCallbackKey>.MappedGlyphs>{Group(InputType.Keyboard,Key(first,glyph),Key(second,(Texture2D)null))});map.Initialise();
            Check(oldCache[InputType.Keyboard].ContainsKey(first) && !oldCache[InputType.Keyboard].ContainsKey(second),"first Add retains dictionary receiver captured before key hashing callback");
            Check(!newInner.ContainsKey(first) && newInner.ContainsKey(second),"next key reloads owner dictionary field after callback replacement");
            Check(ReferenceEquals(Cache(map),newCache),"initialization never writes an earlier captured dictionary back");
        }
        public static int RunManaged()
        {
            checks=0;Declarations();SerializedCallbacks();GameLookup();InputLookup();RetainedReceiverAndReload();return checks;
        }
        // Actual Unity-only ownership/serialization checks are deliberately
        // separate from private source checks and require root Editor execution.
        public static int RunEngine()
        {
            checks=0;GameActionGlyphMap game=null;Texture2D first=null,second=null;StringGlyphMap input=null;
            try
            {
                first=new Texture2D(2,2);second=new Texture2D(3,3);game=ScriptableObject.CreateInstance<GameActionGlyphMap>();
                Check(Field(typeof(GameActionGlyphMap),"m_actionToGlyphMap").GetValue(game)==null && Field(typeof(GameActionGlyphMap),"m_cachedLookup").GetValue(game)!=null,"real game constructor initializes only cache");
                Set(game,"m_inputType",InputType.MfiController);Set(game,"m_actionToGlyphMap",new List<BaseGlyphMap<GameAction>.KeyGlyphMap>{Key(GameAction.CharacterJump,first),Key(GameAction.CharacterJump,second)});game.Initialise();Texture2D glyph;
                Check(game.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,first),"owned real Texture2D first duplicate retained");
                Check(game.InputType==InputType.MfiController,"real scriptable object input getter");
                string json=JsonUtility.ToJson(game);Check(json.Contains("m_actionToGlyphMap") && json.Contains("m_inputType") && !json.Contains("m_cachedLookup"),"actual Unity serialized original fields exclude cache");
                UnityEngine.Object.DestroyImmediate(first);first=null;Check(game.TryGetGlyph(GameAction.CharacterJump,out glyph) && !ReferenceEquals(glyph,null) && glyph==null,"destroyed Unity glyph remains a present managed dictionary reference");
                game.Shutdown();Check(!game.TryGetGlyph(GameAction.CharacterJump,out glyph) && ReferenceEquals(glyph,null),"real shutdown clears even destroyed glyph reference");
                input=ScriptableObject.CreateInstance<StringGlyphMap>();
                Check(input != null && input.GetType() == typeof(StringGlyphMap), "actual original concrete string ScriptableObject allocation");
                var cache=Cache(input);
                Check(cache!=null && ReferenceEquals(cache.Comparer,HardlightInputEnumComparers.InputTypeComparer),"real concrete map original generic-base constructor comparer");
                Check(Field(typeof(BaseGlyphMap<string>),"m_missingGlyph").GetValue(input)==null && Field(typeof(BaseGlyphMap<string>),"m_inputGlyphs").GetValue(input)==null,"real concrete constructor leaves original inherited authored fields null");
                Field(typeof(BaseGlyphMap<string>),"m_missingGlyph").SetValue(input,second);Field(typeof(BaseGlyphMap<string>),"m_inputGlyphs").SetValue(input,new List<BaseGlyphMap<string>.MappedGlyphs>{Group(InputType.Keyboard,Key("Jump",second),Key("Horizontal",(Texture2D)null))});input.Initialise();
                Check(ReferenceEquals(input.GetGlyphForKeyAndInputType("Jump",InputType.Keyboard),second),"actual input map retains owned texture reference");
                Check(ReferenceEquals(input.GetGlyphForKeyAndInputType("Horizontal",InputType.Keyboard),null),"actual input map null entry remains distinct from fallback");
                Check(ReferenceEquals(input.GetGlyphForKeyAndInputType("Missing",InputType.Keyboard),second),"actual absent key uses fallback texture");
                Throws<ArgumentException>(input.Initialise,"actual generic repeated initialization preserves Add exception");
                input.Shutdown();Check(!input.ContainsGlyphsForInput(InputType.Keyboard) && ReferenceEquals(input.GetGlyphForKeyAndInputType("Jump",InputType.Keyboard),second),"actual shutdown uses configured fallback");
                return checks;
            }
            finally
            {
                try { if(input!=null)UnityEngine.Object.DestroyImmediate(input); }
                finally { try { if(game!=null)UnityEngine.Object.DestroyImmediate(game); }
                    finally { try { if(first!=null)UnityEngine.Object.DestroyImmediate(first); }
                        finally { if(second!=null)UnityEngine.Object.DestroyImmediate(second); } } }
            }
        }
        public static void Run() { int managed=RunManaged();int engine=RunEngine();Debug.Log("Original glyph startup checks="+(managed+engine)+"; managed="+managed+"; engine="+engine+". Supplied glyph assets and whole startup remain unverified."); }
    }
}
