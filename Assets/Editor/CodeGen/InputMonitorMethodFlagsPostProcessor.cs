using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace ProjectLucid.CodeGen
{
    // Restore twenty-three original concrete method slots without inventing members
    // on the original empty marker interfaces. No PE or symbol serialization.
    public sealed class InputMonitorMethodFlagsPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => new InputMonitorMethodFlagsPostProcessor();
        public override bool WillProcess(ICompiledAssembly assembly) => assembly.Name == "HLInput.Runtime";
        public override ILPostProcessResult Process(ICompiledAssembly assembly)
        {
            if (!WillProcess(assembly)) return null;
            try
            {
                byte[] restored = InputMonitorFlags.Restore(assembly.InMemoryAssembly.PeData);
                return new ILPostProcessResult(new InMemoryAssembly(restored, assembly.InMemoryAssembly.PdbData));
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is InvalidOperationException || error is BadImageFormatException || error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException || error is AssemblyResolutionException)
            {
                return new ILPostProcessResult(assembly.InMemoryAssembly, new List<DiagnosticMessage> {
                    new DiagnosticMessage { DiagnosticType = DiagnosticType.Error, MessageData = "InputMonitor metadata restoration refused: " + error.Message }
                });
            }
        }
    }

    internal static class InputMonitorFlags
    {
        private const ushort AddedFlags = 0x0160; // Final | Virtual | NewSlot
        private const string LocalAssembly = "HLInput.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string Owner = "Hardlight.InputMonitor";
        private sealed class FieldContract
        {
            internal readonly string Key;
            internal readonly string[] Attributes;
            internal FieldContract(string key, string[] attributes) { Key = key; Attributes = attributes; }
        }

        private static readonly string[] TypeAttributes = new[] {
            "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000",
            "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000",
        };
        private static readonly FieldContract[] Fields = new[] {
            new FieldContract("m_touch|1|Hardlight.InputTouch|HLInput.Runtime.dll|-", new string[] { "UnityEngine.HeaderAttribute|System.Void UnityEngine.HeaderAttribute::.ctor(System.String)|010010496E7075742050726F636573736F72730000", "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_swipe|1|Hardlight.InputSwipe|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_tilt|1|Hardlight.InputTilt|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_button|1|Hardlight.InputButton|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_axis|1|Hardlight.InputAxis|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_pointer|1|Hardlight.InputPointer|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_vectorisedGameInput|1|Hardlight.InputVectorised|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_onScreenControls|1|Hardlight.InputOnScreen|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_glyphMapButtonSupplier|1|Hardlight.KeyCodeGlyphMap|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "UnityEngine.HeaderAttribute|System.Void UnityEngine.HeaderAttribute::.ctor(System.String)|010013476C797068204D617020537570706C696572730000" }),
            new FieldContract("m_glyphMapAxisSupplier|1|Hardlight.StringGlyphMap|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_glyphMapSwipeSupplier|1|Hardlight.DirectionGlyphMap|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_glyphMapFixedSupplier|1|Hardlight.FixedBindingsGlyphMap|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("m_glyphMapGameInputSupplier|1|Hardlight.GameInputGlyphMap|HLInput.Runtime.dll|-", new string[] { "UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000" }),
            new FieldContract("DefaultFileName|32854|System.String|netstandard|InputMonitor", new string[] {  }),
            new FieldContract("m_sourceProvider|33|Hardlight.SourceProvider|HLInput.Runtime.dll|-", new string[] {  }),
            new FieldContract("m_paused|1|System.Boolean|netstandard|-", new string[] {  }),
            new FieldContract("m_getGlyphForKeyCodeInputNameAndInputType|1|System.Func`4<UnityEngine.KeyCode,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|-", new string[] {  }),
            new FieldContract("m_getGlyphForStringAndInputType|1|System.Func`4<System.String,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|-", new string[] {  }),
            new FieldContract("m_getGlyphForDirectionAndInputType|1|System.Func`4<Hardlight.InputSwipe/Direction,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|-", new string[] {  }),
            new FieldContract("m_getGlyphForFixedBindingsAndInputType|1|System.Func`4<Hardlight.FixedBindingsGlyphMap/FixedBindingsData,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|-", new string[] {  }),
            new FieldContract("m_getGlyphForGameInput|1|System.Func`2<Hardlight.GameInput,UnityEngine.Texture2D>|netstandard|-", new string[] {  }),
        };
        private static readonly string[] Methods = new[] {
            "get_TouchCount|2182|System.Int32|netstandard|",
            "get_SourceProvider|2182|Hardlight.ISourceProvider|HLInput.Runtime.dll|",
            "get_BaseSourceProvider|2244|Hardlight.BaseSourceProvider|HLInput.Runtime.dll|",
            "InitialiseControlMap|196|System.Void|netstandard|controlMap@Hardlight.ControlMap@HLInput.Runtime.dll#0",
            "InitialiseInputProcessors|196|System.Void|netstandard|glyphLookupSystemSetter@Hardlight.IGlyphLookupSystemSetter@HLInput.Runtime.dll#0,currentInputBindings@System.Collections.Generic.IReadOnlyList`1<Hardlight.GameInputBinding>@netstandard#0",
            "InitialiseGlyphMapSuppliers|196|System.Void|netstandard|",
            "ShutdownInputProcessors|196|System.Void|netstandard|",
            "ShutdownControlMap|196|System.Void|netstandard|controlMap@Hardlight.ControlMap@HLInput.Runtime.dll#0",
            "SetPaused|134|System.Void|netstandard|paused@System.Boolean@netstandard#0",
            "ShutdownGlyphMapSuppliers|196|System.Void|netstandard|",
            "Update|198|System.Void|netstandard|",
            "GetGlyphForKeyCodeAndInputType|129|UnityEngine.Texture2D|UnityEngine.CoreModule|key@UnityEngine.KeyCode@UnityEngine.CoreModule#0,inputType@Hardlight.InputType@HLInput.Runtime.dll#0,joystickIndex@System.Int32@netstandard#0",
            "GetGlyphForStringAndInputType|129|UnityEngine.Texture2D|UnityEngine.CoreModule|key@System.String@netstandard#0,inputType@Hardlight.InputType@HLInput.Runtime.dll#0,joystickIndex@System.Int32@netstandard#0",
            "GetGlyphForDirectionAndInputType|129|UnityEngine.Texture2D|UnityEngine.CoreModule|key@Hardlight.InputSwipe/Direction@HLInput.Runtime.dll#0,inputType@Hardlight.InputType@HLInput.Runtime.dll#0,joystickIndex@System.Int32@netstandard#0",
            "GetGlyphForFixedAndInputType|129|UnityEngine.Texture2D|UnityEngine.CoreModule|key@Hardlight.FixedBindingsGlyphMap/FixedBindingsData@HLInput.Runtime.dll#0,inputType@Hardlight.InputType@HLInput.Runtime.dll#0,joystickIndex@System.Int32@netstandard#0",
            "GetGlyphForGameInput|129|UnityEngine.Texture2D|UnityEngine.CoreModule|gameInput@Hardlight.GameInput@HLAutoGenerated#0",
            "OnValidate|134|System.Void|netstandard|",
            ".ctor|6278|System.Void|netstandard|",
        };

        private sealed class ProviderTypeContract
        {
            internal readonly string Name, Base;
            internal readonly int Flags;
            internal readonly string[] Attributes, Fields, Interfaces, Generics, Methods, Properties;
            internal ProviderTypeContract(string name, int flags, string baseType, string[] attributes, string[] fields, string[] interfaces, string[] generics, string[] methods, string[] properties)
            { Name = name; Flags = flags; Base = baseType; Attributes = attributes; Fields = fields; Interfaces = interfaces; Generics = generics; Methods = methods; Properties = properties; }
        }
        // Derived only from the different-author complete ten-owner original/current
        // capsule. Genuine stripped marker APIs remain empty; no getter invented.
        private static readonly ProviderTypeContract[] ProviderTypes = new[] {
            new ProviderTypeContract("Hardlight.BaseSourceProvider", 1048705, "System.Object",
                new string[] { "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000" },
                new string[] {  },
                new string[] {  },
                new string[] {  },
                new string[] { "Initialise|1478|System.Void|netstandard|baseControllerProvider@Hardlight.IBaseControllerProvider@HLInput.Runtime.dll#0", "Shutdown|1478|System.Void|netstandard|", "Update|1478|System.Void|netstandard|", ".ctor|6276|System.Void|netstandard|" },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.IInputAxisSource", 161, "",
                new string[] {  },
                new string[] {  },
                new string[] { "Hardlight.IBaseInputAxisSource`1<System.String>" },
                new string[] {  },
                new string[] {  },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.IInputKeySource", 161, "",
                new string[] {  },
                new string[] {  },
                new string[] { "Hardlight.IBaseInputKeySource`1<UnityEngine.KeyCode>" },
                new string[] {  },
                new string[] {  },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.IBaseSourceProvider`4", 161, "",
                new string[] {  },
                new string[] {  },
                new string[] {  },
                new string[] { "TKeySource|0|0|Hardlight.IBaseInputKeySource`1<TKey>", "TKey|1|0|", "TAxisSource|2|0|Hardlight.IBaseInputAxisSource`1<TAxis>", "TAxis|3|0|" },
                new string[] {  },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.ISourceProvider", 161, "",
                new string[] {  },
                new string[] {  },
                new string[] { "Hardlight.IBaseSourceProvider`4<Hardlight.IInputKeySource,UnityEngine.KeyCode,Hardlight.IInputAxisSource,System.String>" },
                new string[] {  },
                new string[] { "get_TouchSource|3526|Hardlight.IInputTouchSource|HLInput.Runtime.dll|" },
                new string[] { "TouchSource|Hardlight.IInputTouchSource|HLInput.Runtime.dll|0" }),
            new ProviderTypeContract("Hardlight.SourceController", 1048577, "System.Object",
                new string[] { "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000" },
                new string[] {  },
                new string[] { "Hardlight.IInputKeySource", "Hardlight.IBaseInputKeySource`1<UnityEngine.KeyCode>", "Hardlight.IInputAxisSource", "Hardlight.IBaseInputAxisSource`1<System.String>" },
                new string[] {  },
                new string[] { "GetKey|454|System.Boolean|netstandard|keyCode@UnityEngine.KeyCode@UnityEngine.CoreModule#0,joystickIndex@System.Int32@netstandard#0", "GetKeyDown|454|System.Boolean|netstandard|keyCode@UnityEngine.KeyCode@UnityEngine.CoreModule#0,joystickIndex@System.Int32@netstandard#0", "GetKeyUp|454|System.Boolean|netstandard|keyCode@UnityEngine.KeyCode@UnityEngine.CoreModule#0,joystickIndex@System.Int32@netstandard#0", "GetAxis|454|System.Single|netstandard|axisName@System.String@netstandard#0,joystickIndex@System.Int32@netstandard#0", "Update|454|System.Void|netstandard|", ".ctor|6278|System.Void|netstandard|" },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.SourceMouse", 1048577, "System.Object",
                new string[] { "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000" },
                new string[] { "NumberOfMouseButtonTouches|32849|System.Int32|netstandard|2", "MouseButton0Touch|32849|System.Int32|netstandard|0", "MouseButton1Touch|32849|System.Int32|netstandard|1", "m_trackedMouseTouches|1|UnityEngine.Touch[]|UnityEngine.InputLegacyModule|-", "m_currentActiveTouches|1|System.Collections.Generic.List`1<UnityEngine.Touch>|netstandard|-" },
                new string[] { "Hardlight.IInputTouchSource", "Hardlight.IInputPointerSource" },
                new string[] {  },
                new string[] { ".ctor|6278|System.Void|netstandard|", "GetTouchCount|486|System.Int32|netstandard|", "GetTouch|486|UnityEngine.Touch|UnityEngine.InputLegacyModule|touchIndex@System.Int32@netstandard#0", "GetTilt|486|UnityEngine.Vector3|UnityEngine.CoreModule|", "GetPosition|486|UnityEngine.Vector3|UnityEngine.CoreModule|", "GetScrollWheelDelta|486|System.Single|netstandard|", "Update|134|System.Void|netstandard|", "UpdateTouchIntenal|129|System.Void|netstandard|index@System.Int32@netstandard#0", "TouchIsTracked|129|System.Boolean|netstandard|fingerID@System.Int32@netstandard#0", "RemoveTrackedTouch|129|System.Void|netstandard|fingerID@System.Int32@netstandard#0", "UpdateTrackedTouch|129|System.Void|netstandard|touch@UnityEngine.Touch@UnityEngine.InputLegacyModule#0" },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.SourceProvider", 1048833, "Hardlight.BaseSourceProvider",
                new string[] { "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000" },
                new string[] { "m_sourceController|1|Hardlight.SourceController|HLInput.Runtime.dll|-", "m_sourceMouse|1|Hardlight.SourceMouse|HLInput.Runtime.dll|-", "m_sourceTouchScreen|1|Hardlight.SourceTouchScreen|HLInput.Runtime.dll|-" },
                new string[] { "Hardlight.ISourceProvider", "Hardlight.IBaseSourceProvider`4<Hardlight.IInputKeySource,UnityEngine.KeyCode,Hardlight.IInputAxisSource,System.String>" },
                new string[] {  },
                new string[] { "get_KeySource|2182|Hardlight.IInputKeySource|HLInput.Runtime.dll|", "get_AxisSource|2182|Hardlight.IInputAxisSource|HLInput.Runtime.dll|", "get_PointerSource|2182|Hardlight.IInputPointerSource|HLInput.Runtime.dll|", "get_TouchSource|2534|Hardlight.IInputTouchSource|HLInput.Runtime.dll|", "get_GestureSource|2182|Hardlight.IInputTouchSource|HLInput.Runtime.dll|", "Initialise|198|System.Void|netstandard|baseControllerProvider@Hardlight.IBaseControllerProvider@HLInput.Runtime.dll#0", "Shutdown|198|System.Void|netstandard|", "Update|198|System.Void|netstandard|", "CreateControllerSource|129|System.Void|netstandard|", "CreateMouseSource|129|System.Void|netstandard|", "CreateTouchSource|129|System.Void|netstandard|", ".ctor|6278|System.Void|netstandard|" },
                new string[] { "KeySource|Hardlight.IInputKeySource|HLInput.Runtime.dll|0", "AxisSource|Hardlight.IInputAxisSource|HLInput.Runtime.dll|1", "PointerSource|Hardlight.IInputPointerSource|HLInput.Runtime.dll|2", "TouchSource|Hardlight.IInputTouchSource|HLInput.Runtime.dll|3", "GestureSource|Hardlight.IInputTouchSource|HLInput.Runtime.dll|4" }),
            new ProviderTypeContract("Hardlight.SourceTouchScreen", 1048577, "System.Object",
                new string[] { "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000" },
                new string[] {  },
                new string[] { "Hardlight.IInputTouchSource" },
                new string[] {  },
                new string[] { "GetTouchCount|486|System.Int32|netstandard|", "GetTouch|486|UnityEngine.Touch|UnityEngine.InputLegacyModule|touchIndex@System.Int32@netstandard#0", "GetTilt|486|UnityEngine.Vector3|UnityEngine.CoreModule|", ".ctor|6278|System.Void|netstandard|" },
                new string[] {  }),
            new ProviderTypeContract("Hardlight.IControllerProvider`1", 161, "",
                new string[] {  },
                new string[] {  },
                new string[] { "Hardlight.IBaseControllerProvider" },
                new string[] { "TController|0|0|" },
                new string[] { "GetControllers|1478|System.Collections.Generic.IReadOnlyList`1<TController>|netstandard|" },
                new string[] {  }),
        };

        internal static byte[] Restore(byte[] original)
        {
            Require(original != null, "No PE image.");
            using (var input = new MemoryStream(original, false))
            using (var module = ModuleDefinition.ReadModule(input, new ReaderParameters { ReadingMode = ReadingMode.Deferred }))
            {
                Require(module.Assembly != null && module.Assembly.Name.FullName == LocalAssembly && !module.Assembly.Name.HasPublicKey && module.Name == "HLInput.Runtime.dll", "Unexpected assembly identity/signing.");
                TypeDefinition[] all = AllTypes(module.Types).ToArray();
                TypeDefinition[] matches = all.Where(t => t.FullName == Owner).ToArray();
                if (matches.Length == 0)
                {
                    // The existing small input package has none of these owners.
                    // A partially promoted marker graph must not silently pass.
                    Require(!all.Any(t => t.FullName == "Hardlight.IInputMonitor" || t.FullName == "Hardlight.IBaseInputMonitor" || ProviderTypes.Any(c => c.Name == t.FullName) || ExtraTypes.Any(c => c.Name == t.FullName)), "InputMonitor missing from a partial input/provider graph.");
                    return original;
                }
                Require(matches.Length == 1 && matches[0].DeclaringType == null, "Duplicate/nested InputMonitor owner.");
                var type = matches[0];
                Require((int)type.Attributes == 1057025 && !type.HasGenericParameters && !type.HasNestedTypes && !type.HasSecurityDeclarations && type.PackingSize == -1 && type.ClassSize == -1, "Changed InputMonitor type flags/layout/generics.");
                Require(type.BaseType != null && type.BaseType.FullName == "Hardlight.ActiveBindingsInputMonitor`5<Hardlight.ActiveGameInputBindings,Hardlight.GameInputBinding,Hardlight.BindingData,UnityEngine.KeyCode,System.String>", "Changed InputMonitor base signature.");
                Scope(type.BaseType);
                CheckAttributes(type.CustomAttributes, TypeAttributes);
                CheckMarker(all, "Hardlight.IBaseInputMonitor", null);
                CheckMarker(all, "Hardlight.IInputMonitor", "Hardlight.IBaseInputMonitor");
                Require(type.Interfaces.Count == 2, "Changed InputMonitor marker count.");
                string[] markers = { "Hardlight.IInputMonitor", "Hardlight.IBaseInputMonitor" };
                for (int i = 0; i < markers.Length; ++i)
                {
                    Require(type.Interfaces[i].InterfaceType.FullName == markers[i] && type.Interfaces[i].InterfaceType.Scope == module && type.Interfaces[i].CustomAttributes.Count == 0, "Changed InputMonitor marker identity/order.");
                }
                Require(type.Fields.Count == Fields.Length && type.Methods.Count == Methods.Length && type.Events.Count == 0 && type.Properties.Count == 3, "Changed InputMonitor member inventory.");
                for (int i = 0; i < Fields.Length; ++i)
                {
                    var field = type.Fields[i];
                    Require(FieldKey(field) == Fields[i].Key && !field.HasMarshalInfo && field.Offset == -1 && field.InitialValue.Length == 0, "Changed InputMonitor field/order/constant/layout.");
                    Scope(field.FieldType); CheckAttributes(field.CustomAttributes, Fields[i].Attributes);
                }
                var targets = new List<MethodDefinition>(); bool? restored = null;
                for (int i = 0; i < Methods.Length; ++i)
                {
                    var method = type.Methods[i];
                    ushort expected = (ushort)int.Parse(Methods[i].Split('|')[1]);
                    bool target = i == 1 || i == 8;
                    ushort actual = (ushort)method.Attributes;
                    if (target)
                    {
                        bool done = actual == (expected | AddedFlags);
                        Require(actual == expected || done, "Unexpected target MethodDef flags.");
                        Require(!restored.HasValue || restored.Value == done, "Partially restored target flags.");
                        restored = done; targets.Add(method);
                    }
                    Require(MethodKey(method, target ? expected : actual) == Methods[i], "Changed InputMonitor signature/name/parameter/order.");
                    Require(method.ImplAttributes == 0 && method.HasBody && method.HasThis && !method.ExplicitThis && method.CallingConvention == MethodCallingConvention.Default && !method.HasGenericParameters && !method.HasSecurityDeclarations && method.Overrides.Count == 0 && !method.HasPInvokeInfo && method.CustomAttributes.Count == 0, "Changed InputMonitor method implementation/attributes.");
                    Require(method.MethodReturnType.Attributes == 0 && !method.MethodReturnType.HasConstant && !method.MethodReturnType.HasMarshalInfo && method.MethodReturnType.CustomAttributes.Count == 0, "Changed return metadata.");
                    Scope(method.ReturnType);
                    foreach (var parameter in method.Parameters)
                    {
                        Require(!parameter.HasConstant && !parameter.HasMarshalInfo && parameter.CustomAttributes.Count == 0, "Changed parameter defaults/attributes.");
                        Scope(parameter.ParameterType);
                    }
                }
                CheckProperties(type);
                Require(targets.Count == 2 && targets[0].Name == "get_SourceProvider" && targets[1].Name == "SetPaused", "Expected exact two target methods.");
                CheckTargetBody(targets[0], type.Fields.Single(f => f.Name == "m_sourceProvider"), false);
                CheckTargetBody(targets[1], type.Fields.Single(f => f.Name == "m_paused"), true);
                CheckProviderFamily(all, module, targets, ref restored);
                CheckExtraFamily(all, module, targets, ref restored);
                Require(targets.Count == 23, "Expected exactly twenty-three backed targets.");
                var copy = (byte[])original.Clone(); var table = new MethodTable(copy);
                int[] offsets = targets.Select(target => table.FlagsOffset(target.MetadataToken.RID)).ToArray();
                Require(offsets.Distinct().Count() == 23, "Duplicate target MethodDef cell.");
                foreach (var target in targets)
                {
                    Require(target.MetadataToken.TokenType == TokenType.Method, "Target is not a MethodDef.");
                    int offset = table.FlagsOffset(target.MetadataToken.RID);
                    ushort old = table.U16(offset);
                    Require(old == (ushort)target.Attributes, "PE/Cecil MethodDef disagreement.");
                    table.SetU16(offset, (ushort)(old | AddedFlags));
                }
                int changes = 0;
                for (int i = 0; i < copy.Length; ++i)
                    if (copy[i] != original[i])
                    {
                        Require(offsets.Any(offset => i == offset || i == offset + 1), "Write outside twenty-three target cells.");
                        ++changes;
                    }
                Require(changes == (restored.Value ? 0 : 46), "Unexpected twenty-three-target byte delta.");
                return copy;
            }
        }
        private static void CheckProviderFamily(TypeDefinition[] all, ModuleDefinition module, List<MethodDefinition> targets, ref bool? restored)
        {
            foreach (var contract in ProviderTypes)
            {
                var matches = all.Where(t => t.FullName == contract.Name).ToArray();
                Require(matches.Length == 1 && matches[0].DeclaringType == null, "Missing/duplicate/nested provider owner " + contract.Name);
                var type = matches[0];
                Require((int)type.Attributes == contract.Flags && !type.HasNestedTypes && !type.HasSecurityDeclarations && type.PackingSize == -1 && type.ClassSize == -1 && type.Events.Count == 0, "Changed provider type/layout/natural owners/events.");
                Require((type.BaseType == null ? "" : type.BaseType.FullName) == contract.Base, "Changed provider base.");
                if (type.BaseType != null) Scope(type.BaseType);
                CheckAttributes(type.CustomAttributes, contract.Attributes);
                Require(type.Interfaces.Count == contract.Interfaces.Length && type.GenericParameters.Count == contract.Generics.Length && type.Fields.Count == contract.Fields.Length && type.Methods.Count == contract.Methods.Length && type.Properties.Count == contract.Properties.Length, "Changed complete provider inventory.");
                for (int i = 0; i < contract.Interfaces.Length; ++i)
                {
                    var entry = type.Interfaces[i];
                    Require(entry.InterfaceType.FullName == contract.Interfaces[i] && entry.InterfaceType.Scope == module && entry.CustomAttributes.Count == 0, "Changed provider interface identity/order.");
                    Scope(entry.InterfaceType);
                }
                for (int i = 0; i < contract.Generics.Length; ++i)
                {
                    var g = type.GenericParameters[i];
                    string key = g.Name + "|" + g.Position + "|" + (int)g.Attributes + "|" + string.Join("&", g.Constraints.Select(c => c.ConstraintType.FullName));
                    Require(key == contract.Generics[i] && ReferenceEquals(g.Owner, type) && g.Type == GenericParameterType.Type && g.CustomAttributes.Count == 0 && g.Constraints.All(c => c.CustomAttributes.Count == 0), "Changed provider generic owner/constraints.");
                    foreach (var constraint in g.Constraints) Scope(constraint.ConstraintType);
                }
                for (int i = 0; i < contract.Fields.Length; ++i)
                {
                    var f = type.Fields[i];
                    Require(FieldKey(f) == contract.Fields[i] && !f.HasMarshalInfo && f.Offset == -1 && f.InitialValue.Length == 0 && f.CustomAttributes.Count == 0, "Changed provider field/order/constant.");
                    Scope(f.FieldType);
                }
                var propertyGetters = new HashSet<MethodDefinition>();
                for (int i = 0; i < contract.Properties.Length; ++i)
                {
                    string[] key = contract.Properties[i].Split('|');
                    var p = type.Properties[i]; var getter = type.Methods[int.Parse(key[3])];
                    Require(p.Name == key[0] && p.PropertyType.FullName == key[1] && p.PropertyType.Scope.Name == key[2] && p.Attributes == 0 && !p.HasConstant && p.CustomAttributes.Count == 0 && p.Parameters.Count == 0 && p.OtherMethods.Count == 0 && p.SetMethod == null && ReferenceEquals(p.GetMethod, getter), "Changed provider property/getter.");
                    Scope(p.PropertyType); propertyGetters.Add(getter);
                }
                for (int i = 0; i < contract.Methods.Length; ++i)
                {
                    var method = type.Methods[i]; ushort expected = (ushort)int.Parse(contract.Methods[i].Split('|')[1]);
                    bool target = contract.Name == "Hardlight.SourceProvider" && (i == 0 || i == 1 || i == 2 || i == 4);
                    ushort actual = (ushort)method.Attributes;
                    if (target)
                    {
                        bool done = actual == (expected | AddedFlags);
                        Require(expected == 2182 && (actual == expected || done), "Unexpected SourceProvider target flags.");
                        Require(restored.HasValue && restored.Value == done, "Partially restored six-target group.");
                        targets.Add(method);
                    }
                    Require(MethodKey(method, target ? expected : actual) == contract.Methods[i], "Changed provider method/order/signature/flags.");
                    Require(method.ImplAttributes == 0 && method.HasBody == !method.IsAbstract && method.HasThis && !method.ExplicitThis && method.CallingConvention == MethodCallingConvention.Default && !method.HasGenericParameters && !method.HasSecurityDeclarations && method.Overrides.Count == 0 && !method.HasPInvokeInfo && method.CustomAttributes.Count == 0, "Changed provider implementation metadata.");
                    Require(method.SemanticsAttributes == (propertyGetters.Contains(method) ? MethodSemanticsAttributes.Getter : 0), "Changed provider method/property semantics.");
                    Require(method.MethodReturnType.Attributes == 0 && !method.MethodReturnType.HasConstant && !method.MethodReturnType.HasMarshalInfo && method.MethodReturnType.CustomAttributes.Count == 0, "Changed provider return metadata.");
                    Scope(method.ReturnType);
                    foreach (var parameter in method.Parameters)
                    {
                        Require(!parameter.HasConstant && !parameter.HasMarshalInfo && parameter.CustomAttributes.Count == 0, "Changed provider parameter metadata.");
                        Scope(parameter.ParameterType);
                    }
                    // The already-correct TouchSource is guarded but never written.
                    if (contract.Name == "Hardlight.SourceProvider" && i < 5)
                        CheckTargetBody(method, type.Fields[i < 2 ? 0 : 1], false);
                }
            }
        }
        // Guard the complete original axis, active-binding and binding declarations.
        // Declaration literals qualify the selected emitted context; only the
        // named original flag losses below authorize physical Attributes writes.
        private sealed class ExtraContract
        {
            internal readonly string Name, Header;
            internal readonly string[] Fields, Methods, Properties, Events, Interfaces, Nested;
            internal ExtraContract(string name, string header, string[] fields, string[] methods, string[] properties, string[] events, string[] interfaces, string[] nested)
            { Name = name; Header = header; Fields = fields; Methods = methods; Properties = properties; Events = events; Interfaces = interfaces; Nested = nested; }
        }
        private static readonly ExtraContract[] ExtraTypes = new[] {
            new ExtraContract("Hardlight.BaseInputAxis`2", "Hardlight.BaseInputAxis`2|1056897|System.Object|netstandard|0|-1|-1||TAxis|0|0|;TAxisBinding|1|0|Hardlight.BaseBinding|HLInput.Runtime.dll|0;Hardlight.IAxisProvider`1<TAxis>|HLInput.Runtime.dll|0|Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000;Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000", new string[] { "AxisStartHandlers|1|Hardlight.IInputAxis/OnAxisStartHandler|HLInput.Runtime.dll|0|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "AxisHandlers|1|Hardlight.IInputAxis/OnAxisHandler|HLInput.Runtime.dll|0|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "AxisEndHandlers|1|Hardlight.IInputAxis/OnAxisEndHandler|HLInput.Runtime.dll|0|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "m_inputAxisSource|1|Hardlight.IBaseInputAxisSource`1<TAxis>|HLInput.Runtime.dll|0|-|-1||", "m_inputAxisProviders|1|System.Collections.Generic.IReadOnlyList`1<Hardlight.IInputAxisProvider`1<TAxisBinding>>|netstandard|0|-|-1||", "m_trackingLookup|1|System.Collections.Generic.HashSet`1<System.String>|netstandard|0|-|-1||" }, new string[] { "add_AxisStartHandlers|2182|0|8|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisStartHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "remove_AxisStartHandlers|2182|0|16|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisStartHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "add_AxisHandlers|2182|0|8|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "remove_AxisHandlers|2182|0|16|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "add_AxisEndHandlers|2182|0|8|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisEndHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "remove_AxisEndHandlers|2182|0|16|1|0|0|System.Void|netstandard|0|value|0|Hardlight.IInputAxis/OnAxisEndHandler|HLInput.Runtime.dll|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "Initialise|134|0|0|1|0|0|System.Void|netstandard|0|gameInputGlyphMap|0|System.Collections.Generic.Dictionary`2<Hardlight.GameInput,System.Collections.Generic.Dictionary`2<Hardlight.InputType,System.Collections.Generic.List`1<UnityEngine.Texture2D>>>&|netstandard|0|0|-;getGlyphForKeyAndInputType|1|System.Func`4<TAxis,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|0|0|-;inputTouchSource|2|Hardlight.IBaseInputAxisSource`1<TAxis>|HLInput.Runtime.dll|0|0|-;inputAxisProviders|3|System.Collections.Generic.IReadOnlyList`1<Hardlight.IInputAxisProvider`1<TAxisBinding>>|netstandard|0|0|-|||1", "Shutdown|134|0|0|1|0|0|System.Void|netstandard|0||||1", "Update|134|0|0|1|0|0|System.Void|netstandard|0||||1", "UpdateAxisBinding|1476|0|0|1|0|0|System.Void|netstandard|0|gameInput|0|Hardlight.GameInput|HLAutoGenerated|1|0|-;axisProvider|1|Hardlight.IAxisProvider`1<TAxis>|HLInput.Runtime.dll|0|0|-;inputAxisSource|2|Hardlight.IBaseInputAxisSource`1<TAxis>|HLInput.Runtime.dll|0|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-;joystickIndex|4|System.Int32|netstandard|1|0|-;modifiers|5|System.Collections.Generic.IReadOnlyList`1<Hardlight.InputModifier>|netstandard|0|0|-|||0", "ProcessAxis|132|0|0|1|0|0|System.Void|netstandard|0|gameInput|0|Hardlight.GameInput|HLAutoGenerated|1|0|-;axis|1|TAxis|HLInput.Runtime.dll|0|0|-;inputKeySource|2|Hardlight.IBaseInputAxisSource`1<TAxis>|HLInput.Runtime.dll|0|0|-;trackingKey|3|System.String|netstandard|0|0|-;inputType|4|Hardlight.InputType|HLInput.Runtime.dll|1|0|-;joystickIndex|5|System.Int32|netstandard|1|0|-;modifiers|6|System.Collections.Generic.IReadOnlyList`1<Hardlight.InputModifier>|netstandard|0|0|-|||1", "ApplyModifiers|129|0|0|1|0|0|System.Void|netstandard|0|modifiers|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.InputModifier>|netstandard|0|0|-;value|1|System.Single&|netstandard|0|0|-;gameInput|2|Hardlight.GameInput|HLAutoGenerated|1|0|-|||1", "ResetModifiers|129|0|0|1|0|0|System.Void|netstandard|0|modifiers|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.InputModifier>|netstandard|0|0|-|||1", "PopulateToGlyphMap|129|0|0|1|0|0|System.Void|netstandard|0|gameInputGlyphMap|0|System.Collections.Generic.Dictionary`2<Hardlight.GameInput,System.Collections.Generic.Dictionary`2<Hardlight.InputType,System.Collections.Generic.List`1<UnityEngine.Texture2D>>>&|netstandard|0|0|-;getGlyphForKeyAndInputType|1|System.Func`4<TAxis,Hardlight.InputType,System.Int32,UnityEngine.Texture2D>|netstandard|0|0|-;inputAxisProviders|2|System.Collections.Generic.IReadOnlyList`1<Hardlight.IInputAxisProvider`1<TAxisBinding>>|netstandard|0|0|-|||1", ".ctor|6276|0|0|1|0|0|System.Void|netstandard|0||||1" }, new string[] {  }, new string[] { "AxisStartHandlers|0|Hardlight.IInputAxis/OnAxisStartHandler|HLInput.Runtime.dll|0|System.Void Hardlight.BaseInputAxis`2::add_AxisStartHandlers(Hardlight.IInputAxis/OnAxisStartHandler)|System.Void Hardlight.BaseInputAxis`2::remove_AxisStartHandlers(Hardlight.IInputAxis/OnAxisStartHandler)|", "AxisHandlers|0|Hardlight.IInputAxis/OnAxisHandler|HLInput.Runtime.dll|0|System.Void Hardlight.BaseInputAxis`2::add_AxisHandlers(Hardlight.IInputAxis/OnAxisHandler)|System.Void Hardlight.BaseInputAxis`2::remove_AxisHandlers(Hardlight.IInputAxis/OnAxisHandler)|", "AxisEndHandlers|0|Hardlight.IInputAxis/OnAxisEndHandler|HLInput.Runtime.dll|0|System.Void Hardlight.BaseInputAxis`2::add_AxisEndHandlers(Hardlight.IInputAxis/OnAxisEndHandler)|System.Void Hardlight.BaseInputAxis`2::remove_AxisEndHandlers(Hardlight.IInputAxis/OnAxisEndHandler)|" }, new string[] { "Hardlight.IInputAxis|HLInput.Runtime.dll|0" }, new string[] {  }),
            new ExtraContract("Hardlight.BaseActiveBindings`2", "Hardlight.BaseActiveBindings`2|1048705|Hardlight.SingleScriptableObject|HLUnityCore.Runtime|0|-1|-1||TInputBindingProvider|0|0|Hardlight.BaseInputBinding`1<TBindingData>|HLInput.Runtime.dll|0;TBindingData|1|0|Hardlight.BaseBindingData|HLInput.Runtime.dll|0|Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000;Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", new string[] { "m_activeBindings|1|System.Collections.Generic.List`1<TInputBindingProvider>|netstandard|0|-|-1||", "m_allowSaveLoad|33|System.Boolean|netstandard|1|-|-1||", "m_currentInputBindings|33|System.Collections.Generic.List`1<TInputBindingProvider>|netstandard|0|-|-1||", "m_baseControllerProvider|1|Hardlight.IBaseControllerProvider|HLInput.Runtime.dll|0|-|-1||", "OnCurrentInputBindingsUpdate|1|System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>|netstandard|0|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "OnCurrentBaseInputBindingsUpdate|1|System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>|netstandard|0|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000" }, new string[] { "get_CurrentInputBindings|2182|0|2|1|0|0|System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>|netstandard|0||||1", "add_OnCurrentInputBindingsUpdate|2182|0|8|1|0|0|System.Void|netstandard|0|value|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>|netstandard|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "remove_OnCurrentInputBindingsUpdate|2182|0|16|1|0|0|System.Void|netstandard|0|value|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>|netstandard|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "get_CurrentBaseInputBindings|2182|0|2|1|0|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>|netstandard|0||||1", "add_OnCurrentBaseInputBindingsUpdate|2182|0|8|1|0|0|System.Void|netstandard|0|value|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>|netstandard|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "remove_OnCurrentBaseInputBindingsUpdate|2182|0|16|1|0|0|System.Void|netstandard|0|value|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>|netstandard|0|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "SetupBindings|1476|0|0|1|0|0|System.Void|netstandard|0||||0", "SetActiveBindings|132|0|0|1|0|0|System.Void|netstandard|0|newActiveBindings|0|System.Collections.Generic.List`1<TInputBindingProvider>|netstandard|0|0|-|||1", "Initialise|134|0|0|1|0|0|System.Void|netstandard|0|baseControllerProvider|0|Hardlight.IBaseControllerProvider|HLInput.Runtime.dll|0|0|-|||1", "Shutdown|134|0|0|1|0|0|System.Void|netstandard|0||||1", "AddPropertyStoreHandlers|129|0|0|1|0|0|System.Void|netstandard|0||||1", "RemovePropertyStoreHandlers|129|0|0|1|0|0|System.Void|netstandard|0||||1", "RefreshBindings|129|0|0|1|0|0|System.Void|netstandard|0||||1", "InternalRefreshBindings|452|0|0|1|0|0|System.Void|netstandard|0||||1", "OnPropertyStoreSave|129|0|0|1|0|0|System.Void|netstandard|0|properties|0|Hardlight.HLPropertyList|HLUnityCore.Runtime|0|0|-|||1", "OnPropertyStoreLoad|129|0|0|1|0|0|System.Void|netstandard|0|properties|0|Hardlight.HLPropertyList|HLUnityCore.Runtime|0|0|-;isnewfile|1|System.Boolean|netstandard|1|0|-|||1", "OnValidate|129|0|0|1|0|0|System.Void|netstandard|0||||1", ".ctor|6276|0|0|1|0|0|System.Void|netstandard|0||||1" }, new string[] { "CurrentInputBindings|0|System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>|netstandard|0|System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider> Hardlight.BaseActiveBindings`2::get_CurrentInputBindings()||", "CurrentBaseInputBindings|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>|netstandard|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider> Hardlight.BaseActiveBindings`2::get_CurrentBaseInputBindings()||" }, new string[] { "OnCurrentInputBindingsUpdate|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>|netstandard|0|System.Void Hardlight.BaseActiveBindings`2::add_OnCurrentInputBindingsUpdate(System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>)|System.Void Hardlight.BaseActiveBindings`2::remove_OnCurrentInputBindingsUpdate(System.Action`1<System.Collections.Generic.IReadOnlyList`1<TInputBindingProvider>>)|", "OnCurrentBaseInputBindingsUpdate|0|System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>|netstandard|0|System.Void Hardlight.BaseActiveBindings`2::add_OnCurrentBaseInputBindingsUpdate(System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>)|System.Void Hardlight.BaseActiveBindings`2::remove_OnCurrentBaseInputBindingsUpdate(System.Action`1<System.Collections.Generic.IReadOnlyList`1<Hardlight.IBaseInputBindingProvider>>)|" }, new string[] { "Hardlight.ICurrentBaseInputBindingsProvider|HLInput.Runtime.dll|0" }, new string[] { "Hardlight.BaseActiveBindings`2/<>c", "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0" }),
            new ExtraContract("Hardlight.BaseInputBinding`1", "Hardlight.BaseInputBinding`1|1048705|UnityEngine.ScriptableObject|UnityEngine.CoreModule|0|-1|-1||TBindingData|0|0|Hardlight.BaseBindingData|HLInput.Runtime.dll|0|Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000;Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", new string[] { "m_forcedBinding|1|System.Boolean|netstandard|1|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000;UnityEngine.TooltipAttribute|System.Void UnityEngine.TooltipAttribute::.ctor(System.String)|010020457870656374656420746F20616C7761797320626520617661696C61626C652E0000", "m_allowRemapping|1|System.Boolean|netstandard|1|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_fallbackInputType|1|Hardlight.InputType|HLInput.Runtime.dll|1|-|-1||UnityEngine.TooltipAttribute|System.Void UnityEngine.TooltipAttribute::.ctor(System.String)|01004A436F6E74726F6C6C65722077696C6C2066616C6C6261636B20746F20746865206669727374206964656E746966696572206D61746368696E67207468697320696E70757420747970652E0000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_bindingInputType|1|Hardlight.InputType|HLInput.Runtime.dll|1|-|-1||UnityEngine.TooltipAttribute|System.Void UnityEngine.TooltipAttribute::.ctor(System.String)|010058496E7075742074797065206F6620746869732062696E64696E672E2049742063616E206265206F76657272696464656E20696620616E206964656E746966696572206973206D6174636865642061742072756E74696D652E0000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_bindingData|1|System.Collections.Generic.List`1<TBindingData>|netstandard|0|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_bindingIdentifiers|1|System.Collections.Generic.List`1<Hardlight.BindingIdentifier>|netstandard|0|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_inputTypeOverride|1|Hardlight.InputType|HLInput.Runtime.dll|1|-|-1||", "<JoystickIndex>k__BackingField|1|System.Int32|netstandard|1|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "<Priority>k__BackingField|1|System.Int32|netstandard|1|-|-1||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", "m_isInitialised|129|System.Boolean|netstandard|1|-|-1||" }, new string[] { "get_BindingIdentifiers|2182|0|2|1|0|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.BindingIdentifier>|netstandard|0||||1", "get_JoystickIndex|2534|0|2|1|0|0|System.Int32|netstandard|1|||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "set_JoystickIndex|2177|0|1|1|0|0|System.Void|netstandard|0|value|0|System.Int32|netstandard|1|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "get_Priority|2182|0|2|1|0|0|System.Int32|netstandard|1|||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "set_Priority|2177|0|1|1|0|0|System.Void|netstandard|0|value|0|System.Int32|netstandard|1|0|-||System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000|1", "get_AllowRemapping|2182|0|2|1|0|0|System.Boolean|netstandard|1||||1", "get_ForcedBinding|2182|0|2|1|0|0|System.Boolean|netstandard|1||||1", "get_InputTypeOverride|2534|0|2|1|0|0|Hardlight.InputType|HLInput.Runtime.dll|1||||1", "get_BindingInputType|2534|0|2|1|0|0|Hardlight.InputType|HLInput.Runtime.dll|1||||1", "get_FallbackInputType|2182|0|2|1|0|0|Hardlight.InputType|HLInput.Runtime.dll|1||||1", "get_BindingData|2182|0|2|1|0|0|System.Collections.Generic.IReadOnlyList`1<TBindingData>|netstandard|0||||1", "Awake|129|0|0|1|0|0|System.Void|netstandard|0||||1", "Setup|134|0|0|1|0|0|System.Void|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;priority|1|System.Int32|netstandard|1|0|-;identifier|2|System.String|netstandard|0|0|-|||1", "PrepareToSave|134|0|0|1|0|0|System.Void|netstandard|0||||1", "UpdateFromSave|134|0|0|1|0|0|System.Void|netstandard|0||||1", ".ctor|6276|0|0|1|0|0|System.Void|netstandard|0||||1" }, new string[] { "BindingIdentifiers|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.BindingIdentifier>|netstandard|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.BindingIdentifier> Hardlight.BaseInputBinding`1::get_BindingIdentifiers()||", "JoystickIndex|0|System.Int32|netstandard|1|System.Int32 Hardlight.BaseInputBinding`1::get_JoystickIndex()|System.Void Hardlight.BaseInputBinding`1::set_JoystickIndex(System.Int32)|", "Priority|0|System.Int32|netstandard|1|System.Int32 Hardlight.BaseInputBinding`1::get_Priority()|System.Void Hardlight.BaseInputBinding`1::set_Priority(System.Int32)|", "AllowRemapping|0|System.Boolean|netstandard|1|System.Boolean Hardlight.BaseInputBinding`1::get_AllowRemapping()||", "ForcedBinding|0|System.Boolean|netstandard|1|System.Boolean Hardlight.BaseInputBinding`1::get_ForcedBinding()||", "InputTypeOverride|0|Hardlight.InputType|HLInput.Runtime.dll|1|Hardlight.InputType Hardlight.BaseInputBinding`1::get_InputTypeOverride()||", "BindingInputType|0|Hardlight.InputType|HLInput.Runtime.dll|1|Hardlight.InputType Hardlight.BaseInputBinding`1::get_BindingInputType()||", "FallbackInputType|0|Hardlight.InputType|HLInput.Runtime.dll|1|Hardlight.InputType Hardlight.BaseInputBinding`1::get_FallbackInputType()||", "BindingData|0|System.Collections.Generic.IReadOnlyList`1<TBindingData>|netstandard|0|System.Collections.Generic.IReadOnlyList`1<TBindingData> Hardlight.BaseInputBinding`1::get_BindingData()||" }, new string[] {  }, new string[] { "Hardlight.IBaseInputBindingProvider|HLInput.Runtime.dll|0" }, new string[] {  }),
            new ExtraContract("Hardlight.IInputAxis", "Hardlight.IInputAxis|161||-1|-1|||", new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] { "Hardlight.IInputAxis/OnAxisStartHandler", "Hardlight.IInputAxis/OnAxisHandler", "Hardlight.IInputAxis/OnAxisEndHandler" }),
            new ExtraContract("Hardlight.IInputAxis/OnAxisStartHandler", "Hardlight.IInputAxis/OnAxisStartHandler|258|System.MulticastDelegate|netstandard|0|-1|-1|Hardlight.IInputAxis||", new string[] {  }, new string[] { ".ctor|6278|3|0|1|0|0|System.Void|netstandard|0|object|0|System.Object|netstandard|0|0|-;method|1|System.IntPtr|netstandard|1|0|-|||0", "Invoke|454|3|0|1|0|0|System.Void|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-|||0", "BeginInvoke|454|3|0|1|0|0|System.IAsyncResult|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-;callback|4|System.AsyncCallback|netstandard|0|0|-;object|5|System.Object|netstandard|0|0|-|||0", "EndInvoke|454|3|0|1|0|0|System.Void|netstandard|0|result|0|System.IAsyncResult|netstandard|0|0|-|||0" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.IInputAxis/OnAxisHandler", "Hardlight.IInputAxis/OnAxisHandler|258|System.MulticastDelegate|netstandard|0|-1|-1|Hardlight.IInputAxis||", new string[] {  }, new string[] { ".ctor|6278|3|0|1|0|0|System.Void|netstandard|0|object|0|System.Object|netstandard|0|0|-;method|1|System.IntPtr|netstandard|1|0|-|||0", "Invoke|454|3|0|1|0|0|System.Void|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-|||0", "BeginInvoke|454|3|0|1|0|0|System.IAsyncResult|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-;callback|4|System.AsyncCallback|netstandard|0|0|-;object|5|System.Object|netstandard|0|0|-|||0", "EndInvoke|454|3|0|1|0|0|System.Void|netstandard|0|result|0|System.IAsyncResult|netstandard|0|0|-|||0" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.IInputAxis/OnAxisEndHandler", "Hardlight.IInputAxis/OnAxisEndHandler|258|System.MulticastDelegate|netstandard|0|-1|-1|Hardlight.IInputAxis||", new string[] {  }, new string[] { ".ctor|6278|3|0|1|0|0|System.Void|netstandard|0|object|0|System.Object|netstandard|0|0|-;method|1|System.IntPtr|netstandard|1|0|-|||0", "Invoke|454|3|0|1|0|0|System.Void|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-|||0", "BeginInvoke|454|3|0|1|0|0|System.IAsyncResult|netstandard|0|joystickIndex|0|System.Int32|netstandard|1|0|-;gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|0|-;value|2|System.Single|netstandard|1|0|-;inputType|3|Hardlight.InputType|HLInput.Runtime.dll|1|0|-;callback|4|System.AsyncCallback|netstandard|0|0|-;object|5|System.Object|netstandard|0|0|-|||0", "EndInvoke|454|3|0|1|0|0|System.Void|netstandard|0|result|0|System.IAsyncResult|netstandard|0|0|-|||0" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.ICurrentBaseInputBindingsProvider", "Hardlight.ICurrentBaseInputBindingsProvider|161||-1|-1|||", new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.IBaseInputBindingProvider", "Hardlight.IBaseInputBindingProvider|161||-1|-1|||", new string[] {  }, new string[] { "get_JoystickIndex|3526|0|2|1|0|0|System.Int32|netstandard|1||||0", "get_InputTypeOverride|3526|0|2|1|0|0|Hardlight.InputType|HLInput.Runtime.dll|1||||0", "get_BindingInputType|3526|0|2|1|0|0|Hardlight.InputType|HLInput.Runtime.dll|1||||0" }, new string[] { "JoystickIndex|0|System.Int32|netstandard|1|System.Int32 Hardlight.IBaseInputBindingProvider::get_JoystickIndex()||", "InputTypeOverride|0|Hardlight.InputType|HLInput.Runtime.dll|1|Hardlight.InputType Hardlight.IBaseInputBindingProvider::get_InputTypeOverride()||", "BindingInputType|0|Hardlight.InputType|HLInput.Runtime.dll|1|Hardlight.InputType Hardlight.IBaseInputBindingProvider::get_BindingInputType()||" }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.BaseBindingData", "Hardlight.BaseBindingData|1056897|System.Object|netstandard|0|-1|-1|||Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000;Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000", new string[] { "m_gameInput|1|Hardlight.GameInput|HLAutoGenerated|1|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000;Hardlight.Utils.HashEnumAttribute|System.Void Hardlight.Utils.HashEnumAttribute::.ctor(System.Type)|01005B486172646C696768742E47616D65496E7075742C20484C4175746F47656E6572617465642C2056657273696F6E3D302E302E302E302C2043756C747572653D6E65757472616C2C205075626C69634B6579546F6B656E3D6E756C6C0000", "m_vectorisedBindings|4|System.Collections.Generic.List`1<Hardlight.VectorisedBinding>|netstandard|0|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "s_allModifiers|17|Hardlight.InputModifier[]|HLInput.Runtime.dll|0|-|-1||" }, new string[] { "get_VectorisedBindings|2182|0|2|1|0|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.VectorisedBinding>|netstandard|0||||1", "get_GameInput|2534|0|2|1|0|0|Hardlight.GameInput|HLAutoGenerated|1||||1", "GetCachedAllModifiers|145|0|0|0|0|0|Hardlight.InputModifier[]|HLInput.Runtime.dll|0||||1", "SaveModifierNames|1478|0|0|1|0|0|System.Void|netstandard|0||||0", "InternalSaveModifierNames|132|0|0|1|0|0|System.Void|netstandard|0|bindings|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.BaseBinding>|netstandard|0|0|-|||1", "InternalSaveModifierName|132|0|0|1|0|0|System.Void|netstandard|0|binding|0|Hardlight.BaseBinding|HLInput.Runtime.dll|0|0|-|||1", "LoadModifiersByName|1478|0|0|1|0|0|System.Void|netstandard|0||||0", "InternalLoadModifiersByName|132|0|0|1|0|0|System.Void|netstandard|0|bindings|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.BaseBinding>|netstandard|0|0|-|||1", "InternalLoadModifierByName|132|0|0|1|0|0|System.Void|netstandard|0|binding|0|Hardlight.BaseBinding|HLInput.Runtime.dll|0|0|-|||1", "InternalLoadModifierByName|129|0|0|1|0|0|System.Void|netstandard|0|binding|0|Hardlight.BaseBinding|HLInput.Runtime.dll|0|0|-;allModifiers|1|System.Collections.Generic.IReadOnlyList`1<Hardlight.InputModifier>|netstandard|0|0|-|||1", ".ctor|6276|0|0|1|0|0|System.Void|netstandard|0||||1" }, new string[] { "VectorisedBindings|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.VectorisedBinding>|netstandard|0|System.Collections.Generic.IReadOnlyList`1<Hardlight.VectorisedBinding> Hardlight.BaseBindingData::get_VectorisedBindings()||", "GameInput|0|Hardlight.GameInput|HLAutoGenerated|1|Hardlight.GameInput Hardlight.BaseBindingData::get_GameInput()||" }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.ActiveGameInputBindings", "Hardlight.ActiveGameInputBindings|1048577|Hardlight.BaseActiveBindings`2<Hardlight.GameInputBinding,Hardlight.BindingData>|HLInput.Runtime.dll|0|-1|-1|||Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000100000002000000;Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)|01000200000002000000", new string[] { "m_editorMacBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||UnityEngine.Serialization.FormerlySerializedAsAttribute|System.Void UnityEngine.Serialization.FormerlySerializedAsAttribute::.ctor(System.String)|0100106D5F656469746F7242696E64696E67730000;JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_editorWindowsBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_androidBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_iOSBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_tvOSBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_macOSBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000;JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000", "m_windowsBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000;UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000", "m_switchBindings|1|System.Collections.Generic.List`1<Hardlight.GameInputBinding>|netstandard|0|-|-1||UnityEngine.SerializeField|System.Void UnityEngine.SerializeField::.ctor()|01000000;JetBrains.Annotations.UsedImplicitlyAttribute|System.Void JetBrains.Annotations.UsedImplicitlyAttribute::.ctor()|01000000", "DefaultFileName|32854|System.String|netstandard|0|System.String:ActiveGameInputBindings|-1||" }, new string[] { "SetupBindings|196|0|0|1|0|0|System.Void|netstandard|0||||1", ".ctor|6278|0|0|1|0|0|System.Void|netstandard|0||||1" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.BaseActiveBindings`2/<>c", "Hardlight.BaseActiveBindings`2/<>c|1057027|System.Object|netstandard|0|-1|-1|Hardlight.BaseActiveBindings`2|TInputBindingProvider|0|0|Hardlight.BaseInputBinding`1<TBindingData>|HLInput.Runtime.dll|0;TBindingData|1|0|Hardlight.BaseBindingData|HLInput.Runtime.dll|0|System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", new string[] { "<>9|54|Hardlight.BaseActiveBindings`2/<>c<TInputBindingProvider,TBindingData>|HLInput.Runtime.dll|0|-|-1||", "<>9__20_0|22|System.Predicate`1<TInputBindingProvider>|netstandard|0|-|-1||", "<>9__20_1|22|System.Comparison`1<TInputBindingProvider>|netstandard|0|-|-1||" }, new string[] { ".cctor|6289|0|0|0|0|0|System.Void|netstandard|0||||1", ".ctor|6278|0|0|1|0|0|System.Void|netstandard|0||||1", "<RefreshBindings>b__20_0|131|0|0|1|0|0|System.Boolean|netstandard|1|binding|0|TInputBindingProvider|HLInput.Runtime.dll|0|0|-|||1", "<RefreshBindings>b__20_1|131|0|0|1|0|0|System.Int32|netstandard|1|a|0|TInputBindingProvider|HLInput.Runtime.dll|0|0|-;b|1|TInputBindingProvider|HLInput.Runtime.dll|0|0|-|||1" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  }),
            new ExtraContract("Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0", "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0|1048835|System.Object|netstandard|0|-1|-1|Hardlight.BaseActiveBindings`2|TInputBindingProvider|0|0|Hardlight.BaseInputBinding`1<TBindingData>|HLInput.Runtime.dll|0;TBindingData|1|0|Hardlight.BaseBindingData|HLInput.Runtime.dll|0|System.Runtime.CompilerServices.CompilerGeneratedAttribute|System.Void System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor()|01000000", new string[] { "controllerName|6|System.String|netstandard|0|-|-1||" }, new string[] { ".ctor|6278|0|0|1|0|0|System.Void|netstandard|0||||1", "<RefreshBindings>b__2|131|0|0|1|0|0|System.Boolean|netstandard|1|binding|0|TInputBindingProvider|HLInput.Runtime.dll|0|0|-|||1" }, new string[] {  }, new string[] {  }, new string[] {  }, new string[] {  })
        };
        private static readonly string[] ExtraTargetKeys = new string[] { "Hardlight.BaseInputAxis`2|add_AxisStartHandlers", "Hardlight.BaseInputAxis`2|remove_AxisStartHandlers", "Hardlight.BaseInputAxis`2|add_AxisHandlers", "Hardlight.BaseInputAxis`2|remove_AxisHandlers", "Hardlight.BaseInputAxis`2|add_AxisEndHandlers", "Hardlight.BaseInputAxis`2|remove_AxisEndHandlers", "Hardlight.BaseActiveBindings`2|get_CurrentInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|get_CurrentBaseInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseInputBinding`1|get_BindingIdentifiers", "Hardlight.BaseInputBinding`1|get_Priority", "Hardlight.BaseInputBinding`1|get_AllowRemapping", "Hardlight.BaseInputBinding`1|get_ForcedBinding", "Hardlight.BaseInputBinding`1|get_FallbackInputType" };
        private static readonly string[] NamedTypeKeys = new string[] { "Hardlight.BaseActiveBindings`2|HLInput.Runtime.dll|0", "Hardlight.BaseActiveBindings`2/<>c|HLInput.Runtime.dll|0", "Hardlight.BaseBinding|HLInput.Runtime.dll|0", "Hardlight.BaseBindingData|HLInput.Runtime.dll|0", "Hardlight.BaseInputAxis`2|HLInput.Runtime.dll|0", "Hardlight.BaseInputBinding`1|HLInput.Runtime.dll|0", "Hardlight.BindingData|HLInput.Runtime.dll|0", "Hardlight.BindingIdentifier|HLInput.Runtime.dll|0", "Hardlight.GameInput|HLAutoGenerated|1", "Hardlight.GameInputBinding|HLInput.Runtime.dll|0", "Hardlight.HLPropertyList|HLUnityCore.Runtime|0", "Hardlight.IAxisProvider`1|HLInput.Runtime.dll|0", "Hardlight.IBaseControllerProvider|HLInput.Runtime.dll|0", "Hardlight.IBaseInputAxisSource`1|HLInput.Runtime.dll|0", "Hardlight.IBaseInputBindingProvider|HLInput.Runtime.dll|0", "Hardlight.ICurrentBaseInputBindingsProvider|HLInput.Runtime.dll|0", "Hardlight.IInputAxis|HLInput.Runtime.dll|0", "Hardlight.IInputAxis/OnAxisEndHandler|HLInput.Runtime.dll|0", "Hardlight.IInputAxis/OnAxisHandler|HLInput.Runtime.dll|0", "Hardlight.IInputAxis/OnAxisStartHandler|HLInput.Runtime.dll|0", "Hardlight.IInputAxisProvider`1|HLInput.Runtime.dll|0", "Hardlight.InputModifier|HLInput.Runtime.dll|0", "Hardlight.InputType|HLInput.Runtime.dll|1", "Hardlight.SingleScriptableObject|HLUnityCore.Runtime|0", "Hardlight.Utils.HashEnumAttribute|HLUnityCore.Runtime|0", "Hardlight.VectorisedBinding|HLInput.Runtime.dll|0", "JetBrains.Annotations.UsedImplicitlyAttribute|UnityEngine.CoreModule|0", "System.Action`1|netstandard|0", "System.AsyncCallback|netstandard|0", "System.Boolean|netstandard|1", "System.Collections.Generic.Dictionary`2|netstandard|0", "System.Collections.Generic.HashSet`1|netstandard|0", "System.Collections.Generic.IReadOnlyList`1|netstandard|0", "System.Collections.Generic.List`1|netstandard|0", "System.Comparison`1|netstandard|0", "System.Delegate|netstandard|0", "System.Func`4|netstandard|0", "System.IAsyncResult|netstandard|0", "System.Int32|netstandard|1", "System.IntPtr|netstandard|1", "System.MulticastDelegate|netstandard|0", "System.Object|netstandard|0", "System.Predicate`1|netstandard|0", "System.Runtime.CompilerServices.CompilerGeneratedAttribute|netstandard|0", "System.Single|netstandard|1", "System.String|netstandard|0", "System.Threading.Interlocked|netstandard|0", "System.Type|netstandard|0", "System.Void|netstandard|0", "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|HLUnityCore.Runtime|0", "Unity.IL2CPP.CompilerServices.Option|HLUnityCore.Runtime|1", "UnityEngine.ScriptableObject|UnityEngine.CoreModule|0", "UnityEngine.Serialization.FormerlySerializedAsAttribute|UnityEngine.CoreModule|0", "UnityEngine.SerializeField|UnityEngine.CoreModule|0", "UnityEngine.Texture2D|UnityEngine.CoreModule|0", "UnityEngine.TooltipAttribute|UnityEngine.CoreModule|0" };
        private static string B(bool value) => value ? "1" : "0";
        private static string TypeKey(TypeReference t) => t == null ? "" : t.FullName + "|" + t.Scope.Name + "|" + B(t.IsValueType);
        private static string ConstantKey(bool has, object value) => !has ? "-" : value == null ? "<null>" : value.GetType().FullName + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        private static string AttributeKey(IEnumerable<CustomAttribute> attributes, TypeDefinition owner)
        {
            return string.Join(";", attributes.Select(a => {
                ExtraScope(a.AttributeType, owner); ExtraScope(a.Constructor.DeclaringType, owner);
                Require(a.Constructor.HasThis && !a.Constructor.ExplicitThis && a.Constructor.CallingConvention == MethodCallingConvention.Default && !a.Constructor.HasGenericParameters, "Changed added-owner attribute constructor convention.");
                ExtraScope(a.Constructor.ReturnType, owner);
                foreach (var p in a.Constructor.Parameters) ExtraScope(p.ParameterType, owner);
                return a.AttributeType.FullName + "|" + a.Constructor.FullName + "|" + BitConverter.ToString(a.GetBlob()).Replace("-", "");
            }));
        }
        private static string GenericKey(IEnumerable<GenericParameter> parameters, IGenericParameterProvider owner)
        {
            return string.Join(";", parameters.Select(g => {
                var type = owner as TypeDefinition;
                Require(type != null && ReferenceEquals(g.Owner, type) && g.Type == GenericParameterType.Type && g.Position >= 0 && g.Position < type.GenericParameters.Count && ReferenceEquals(type.GenericParameters[g.Position], g) && g.CustomAttributes.Count == 0, "Changed additional generic owner/position/attributes.");
                foreach (var c in g.Constraints) { Require(c.CustomAttributes.Count == 0, "Changed constraint annotations."); ExtraScope(c.ConstraintType, type); }
                return g.Name + "|" + g.Position + "|" + (int)g.Attributes + "|" + string.Join(";", g.Constraints.Select(c => TypeKey(c.ConstraintType)));
            }));
        }
        private static string AdditionalParameterKey(IEnumerable<ParameterDefinition> parameters, TypeDefinition owner)
        {
            return string.Join(";", parameters.Select(p => {
                Require(!p.HasMarshalInfo && p.CustomAttributes.Count == 0, "Changed added-owner parameter marshal/annotations."); ExtraScope(p.ParameterType, owner);
                return p.Name + "|" + p.Index + "|" + TypeKey(p.ParameterType) + "|" + (int)p.Attributes + "|" + ConstantKey(p.HasConstant, p.Constant);
            }));
        }
        private static string AdditionalFieldKey(FieldDefinition f, TypeDefinition owner)
        {
            Require(!f.HasMarshalInfo, "Changed additional field marshal."); ExtraScope(f.FieldType, owner);
            return f.Name + "|" + (int)f.Attributes + "|" + TypeKey(f.FieldType) + "|" + ConstantKey(f.HasConstant, f.Constant) + "|" + f.Offset + "|" + BitConverter.ToString(f.InitialValue).Replace("-", "") + "|" + AttributeKey(f.CustomAttributes, owner);
        }
        private static string AdditionalMethodKey(MethodDefinition m, ushort flags, TypeDefinition owner)
        {
            Require(!m.HasSecurityDeclarations && m.Overrides.Count == 0 && !m.HasPInvokeInfo, "Changed additional method security/override/PInvoke.");
            Require(m.MethodReturnType.Attributes == 0 && !m.MethodReturnType.HasConstant && !m.MethodReturnType.HasMarshalInfo && m.MethodReturnType.CustomAttributes.Count == 0, "Changed additional return metadata.");
            ExtraScope(m.ReturnType, owner);
            return string.Join("|", new[] { m.Name, flags.ToString(), ((int)m.ImplAttributes).ToString(), ((int)m.SemanticsAttributes).ToString(), B(m.HasThis), B(m.ExplicitThis), ((int)m.CallingConvention).ToString(), TypeKey(m.ReturnType), AdditionalParameterKey(m.Parameters, owner), GenericKey(m.GenericParameters, owner), AttributeKey(m.CustomAttributes, owner), B(m.HasBody) });
        }
        private static string AdditionalPropertyKey(PropertyDefinition p, TypeDefinition owner)
        {
            Require(!p.HasConstant && p.Parameters.Count == 0 && p.OtherMethods.Count == 0, "Changed additional property constant/parameters/others."); ExtraScope(p.PropertyType, owner);
            Require((p.GetMethod == null || owner.Methods.Contains(p.GetMethod)) && (p.SetMethod == null || owner.Methods.Contains(p.SetMethod)), "Detached additional property accessor.");
            return string.Join("|", new[] { p.Name, ((int)p.Attributes).ToString(), TypeKey(p.PropertyType), p.GetMethod == null ? "" : p.GetMethod.FullName, p.SetMethod == null ? "" : p.SetMethod.FullName, AttributeKey(p.CustomAttributes, owner) });
        }
        private static string AdditionalEventKey(EventDefinition e, TypeDefinition owner)
        {
            Require(e.InvokeMethod == null && e.OtherMethods.Count == 0 && e.AddMethod != null && e.RemoveMethod != null && owner.Methods.Contains(e.AddMethod) && owner.Methods.Contains(e.RemoveMethod), "Changed additional event accessor ownership."); ExtraScope(e.EventType, owner);
            return string.Join("|", new[] { e.Name, ((int)e.Attributes).ToString(), TypeKey(e.EventType), e.AddMethod.FullName, e.RemoveMethod.FullName, AttributeKey(e.CustomAttributes, owner) });
        }
        private static void ExtraScope(TypeReference type, TypeDefinition owner)
        {
            if (type is GenericParameter p)
            {
                Require(p.Type == GenericParameterType.Type && ReferenceEquals(p.Owner, owner) && p.Position >= 0 && p.Position < owner.GenericParameters.Count && ReferenceEquals(owner.GenericParameters[p.Position], p), "Changed exact additional signature generic owner.");
                return;
            }
            if (type is GenericInstanceType g)
            {
                Require(g.GenericArguments.Count > 0, "Empty additional generic instance.");
                foreach (var a in g.GenericArguments) ExtraScope(a, owner);
            }
            if (type is TypeSpecification s) { ExtraScope(s.ElementType, owner); return; }
            Require(NamedTypeKeys.Contains(TypeKey(type), StringComparer.Ordinal), "Changed exact additional named type/value/provider.");
            if (type.Scope is ModuleDefinition local)
            {
                Require(ReferenceEquals(local, owner.Module) && local.Name == "HLInput.Runtime.dll" && local.Assembly.Name.FullName == LocalAssembly, "Changed exact additional local scope.");
                return;
            }
            var scope = type.Scope as AssemblyNameReference;
            Require(scope != null, "Unsupported additional named type scope.");
            string expected = scope.Name == "netstandard" ? "netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" : scope.Name + ", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            Require(scope.FullName == expected, "Changed additional exact assembly scope identity.");
        }
        private static void CheckExtraFamily(TypeDefinition[] all, ModuleDefinition module, List<MethodDefinition> targets, ref bool? restored)
        {
            foreach (var contract in ExtraTypes)
            {
                var matches = all.Where(t => t.FullName == contract.Name).ToArray(); Require(matches.Length == 1, "Missing/duplicate exact additional owner " + contract.Name);
                var t = matches[0];
                Require(ReferenceEquals(t.Module, module) && !t.HasSecurityDeclarations, "Changed additional owner module/security.");
                if (t.BaseType != null) ExtraScope(t.BaseType, t);
                string header = string.Join("|", new[] { t.FullName, ((int)t.Attributes).ToString(), TypeKey(t.BaseType), t.PackingSize.ToString(), t.ClassSize.ToString(), t.DeclaringType == null ? "" : t.DeclaringType.FullName, GenericKey(t.GenericParameters, t), AttributeKey(t.CustomAttributes, t) });
                Require(header == contract.Header, "Changed complete additional owner declaration/layout/generics.");
                Require(t.Fields.Select(f => AdditionalFieldKey(f, t)).SequenceEqual(contract.Fields) && t.Interfaces.All(i => i.CustomAttributes.Count == 0) && t.Interfaces.Select(i => TypeKey(i.InterfaceType)).SequenceEqual(contract.Interfaces) && t.NestedTypes.Select(n => n.FullName).SequenceEqual(contract.Nested), "Changed complete additional fields/interfaces/natural owners.");
                foreach (var i in t.Interfaces) ExtraScope(i.InterfaceType, t);
                // Read all property/event associations before testing semantics.
                Require(t.Properties.Select(p => AdditionalPropertyKey(p, t)).SequenceEqual(contract.Properties) && t.Events.Select(e => AdditionalEventKey(e, t)).SequenceEqual(contract.Events), "Changed complete additional property/event associations.");
                Require(t.Methods.Count == contract.Methods.Length, "Changed additional method population.");
                for (int i = 0; i < t.Methods.Count; ++i)
                {
                    var m = t.Methods[i]; bool target = ExtraTargetKeys.Contains(t.FullName + "|" + m.Name, StringComparer.Ordinal);
                    ushort actual = (ushort)m.Attributes;
                    if (target)
                    {
                        bool done = actual == 2534; Require(actual == 2182 || done, "Unexpected additional target flags.");
                        Require(restored.HasValue && restored.Value == done, "Mixed/partial twenty-three-target restoration."); targets.Add(m);
                    }
                    Require(AdditionalMethodKey(m, target ? (ushort)2182 : actual, t) == contract.Methods[i], "Changed complete additional method order/signature/semantics/attributes.");
                    if (target) CheckAdditionalTargetBody(m, t);
                }
            }
            Require(targets.Count == 23 && ExtraTargetKeys.Length == 17, "Changed backed target population.");
            // These genuine already-correct getters are never selected for writes.
            Require(!ExtraTargetKeys.Contains("Hardlight.BaseBindingData|get_GameInput", StringComparer.Ordinal), "Invented sixth binding-data target.");
        }
        private static void CheckSelfField(FieldReference field, TypeDefinition owner, FieldDefinition declared)
        {
            Require(field != null && field.Name == declared.Name && TypeKey(field.FieldType) == TypeKey(declared.FieldType), "Changed target backing field.");
            ExtraScope(field.FieldType, owner); ExtraScope(field.DeclaringType, owner);
            var self = field.DeclaringType as GenericInstanceType;
            Require(self != null && ReferenceEquals(self.ElementType, owner) && self.GenericArguments.Count == owner.GenericParameters.Count && self.GenericArguments.Select((a, i) => ReferenceEquals(a, owner.GenericParameters[i])).All(v => v), "Changed target exact generic-self field owner/arguments.");
        }
        private static void CheckAdditionalTargetBody(MethodDefinition method, TypeDefinition owner)
        {
            Require(method.HasBody, "Missing additional target body."); var b = method.Body; var ins = b.Instructions;
            if (method.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                string name = owner.FullName == "Hardlight.BaseActiveBindings`2" ? "m_currentInputBindings"
                    : method.Name == "get_BindingIdentifiers" ? "m_bindingIdentifiers"
                    : method.Name == "get_Priority" ? "<Priority>k__BackingField"
                    : method.Name == "get_AllowRemapping" ? "m_allowRemapping"
                    : method.Name == "get_ForcedBinding" ? "m_forcedBinding" : "m_fallbackInputType";
                Require(b.CodeSize == 7 && b.MaxStackSize == 8 && !b.InitLocals && b.Variables.Count == 0 && b.ExceptionHandlers.Count == 0 && ins.Count == 3 && ins[0].OpCode.Code == Code.Ldarg_0 && ins[1].OpCode.Code == Code.Ldfld && ins[2].OpCode.Code == Code.Ret && ins.Select(i => i.Offset).SequenceEqual(new[] { 0, 1, 6 }), "Changed additional getter physical body/header/locals/EH.");
                CheckSelfField(ins[1].Operand as FieldReference, owner, owner.Fields.Single(f => f.Name == name)); return;
            }
            bool add = method.Name.StartsWith("add_", StringComparison.Ordinal); string eventName = method.Name.Substring(add ? 4 : 7);
            var e = owner.Events.Single(v => v.Name == eventName); var field = owner.Fields.Single(f => f.Name == eventName);
            Require(ReferenceEquals(add ? e.AddMethod : e.RemoveMethod, method) && method.Parameters.Count == 1 && TypeKey(method.Parameters[0].ParameterType) == TypeKey(e.EventType) && TypeKey(field.FieldType) == TypeKey(e.EventType), "Changed exact target event/parameter/delegate/field linkage.");
            string[] codes = { "ldarg.0", "ldfld", "stloc.0", "ldloc.0", "stloc.1", "ldloc.1", "ldarg.1", "call", "castclass", "stloc.2", "ldarg.0", "ldflda", "ldloc.2", "ldloc.1", "call", "stloc.0", "ldloc.0", "ldloc.1", "bne.un.s", "ret" };
            int[] offsets = { 0, 1, 6, 7, 8, 9, 10, 11, 16, 21, 22, 23, 28, 29, 30, 35, 36, 37, 38, 40 };
            Require(b.CodeSize == 41 && b.MaxStackSize == 3 && b.InitLocals && b.Variables.Count == 3 && b.ExceptionHandlers.Count == 0 && b.Variables.All(v => !v.IsPinned && TypeKey(v.VariableType) == TypeKey(e.EventType)) && ins.Select(i => i.OpCode.Name).SequenceEqual(codes) && ins.Select(i => i.Offset).SequenceEqual(offsets) && ReferenceEquals(ins[18].Operand, ins[3]), "Changed exact CAS retry body/header/locals/EH.");
            foreach (var v in b.Variables) ExtraScope(v.VariableType, owner);
            CheckSelfField(ins[1].Operand as FieldReference, owner, field); CheckSelfField(ins[11].Operand as FieldReference, owner, field);
            var cast = ins[8].Operand as TypeReference; Require(cast != null && TypeKey(cast) == TypeKey(e.EventType), "Changed CAS cast delegate."); ExtraScope(cast, owner);
            var combine = ins[7].Operand as MethodReference;
            Require(combine != null && combine.FullName == "System.Delegate System.Delegate::" + (add ? "Combine" : "Remove") + "(System.Delegate,System.Delegate)" && !combine.HasThis && !combine.ExplicitThis && combine.CallingConvention == MethodCallingConvention.Default && !combine.HasGenericParameters && combine.Parameters.Count == 2, "Changed exact CAS Combine/Remove call.");
            ExtraScope(combine.DeclaringType, owner); ExtraScope(combine.ReturnType, owner); foreach (var p in combine.Parameters) ExtraScope(p.ParameterType, owner);
            var exchange = ins[14].Operand as GenericInstanceMethod;
            Require(exchange != null && exchange.GenericArguments.Count == 1 && TypeKey(exchange.GenericArguments[0]) == TypeKey(e.EventType), "Changed CAS CompareExchange specialization."); ExtraScope(exchange.GenericArguments[0], owner);
            var element = exchange.ElementMethod;
            Require(element.Name == "CompareExchange" && element.DeclaringType.FullName == "System.Threading.Interlocked" && !element.HasThis && !element.ExplicitThis && element.CallingConvention == MethodCallingConvention.Generic && element.GenericParameters.Count == 1 && element.Parameters.Count == 3, "Changed CAS CompareExchange element declaration."); ExtraScope(element.DeclaringType, owner);
            var g = element.GenericParameters[0];
            Require(g.Type == GenericParameterType.Method && g.Position == 0 && ReferenceEquals(g.Owner, element) && g.CustomAttributes.Count == 0 && g.Constraints.Count == 0 && g.Attributes == 0 && ReferenceEquals(element.ReturnType, g) && element.Parameters[0].ParameterType is ByReferenceType byref && ReferenceEquals(byref.ElementType, g) && ReferenceEquals(element.Parameters[1].ParameterType, g) && ReferenceEquals(element.Parameters[2].ParameterType, g), "Changed exact CAS method-owned MVAR/byref graph.");
            Require(ins.Where((i, index) => !new[] { 1, 7, 8, 11, 14, 18 }.Contains(index)).All(i => i.Operand == null), "Unexpected additional CAS operand.");
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (var type in roots) { yield return type; foreach (var nested in AllTypes(type.NestedTypes)) yield return nested; }
        }
        private static void CheckMarker(TypeDefinition[] all, string name, string inherited)
        {
            var matches = all.Where(t => t.FullName == name).ToArray();
            Require(matches.Length == 1, "Missing/duplicate marker " + name); var marker = matches[0];
            Require((int)marker.Attributes == 161 && marker.DeclaringType == null && marker.BaseType == null && marker.Methods.Count == 0 && marker.Fields.Count == 0 && marker.Properties.Count == 0 && marker.Events.Count == 0 && marker.CustomAttributes.Count == 0 && !marker.HasGenericParameters && !marker.HasNestedTypes && !marker.HasSecurityDeclarations && marker.PackingSize == -1 && marker.ClassSize == -1, "Marker must remain genuinely empty.");
            Require(marker.Interfaces.Count == (inherited == null ? 0 : 1), "Changed marker inheritance.");
            if (inherited != null) Require(marker.Interfaces[0].InterfaceType.FullName == inherited && marker.Interfaces[0].InterfaceType.Scope == marker.Module && marker.Interfaces[0].CustomAttributes.Count == 0, "Changed inherited marker identity.");
        }
        private static void CheckProperties(TypeDefinition type)
        {
            string[] names = { "TouchCount", "SourceProvider", "BaseSourceProvider" };
            int[] methods = { 0, 1, 2 };
            for (int i = 0; i < names.Length; ++i)
            {
                var p = type.Properties[i]; var getter = type.Methods[methods[i]];
                Require(p.Name == names[i] && p.Attributes == 0 && !p.HasConstant && p.CustomAttributes.Count == 0 && p.Parameters.Count == 0 && p.OtherMethods.Count == 0 && p.SetMethod == null && ReferenceEquals(p.GetMethod, getter) && p.PropertyType.FullName == getter.ReturnType.FullName && p.PropertyType.Scope.Name == getter.ReturnType.Scope.Name && getter.SemanticsAttributes == MethodSemanticsAttributes.Getter, "Changed property/getter contract.");
                Scope(p.PropertyType);
            }
            Require(type.Methods.Skip(3).All(m => m.SemanticsAttributes == 0), "Unexpected method semantics.");
        }
        private static void CheckTargetBody(MethodDefinition method, FieldDefinition field, bool store)
        {
            var body = method.Body; var instructions = body.Instructions;
            Require(!body.InitLocals && body.MaxStackSize == 8 && body.Variables.Count == 0 && body.ExceptionHandlers.Count == 0 && instructions.Count == (store ? 4 : 3), "Changed target body/header/locals/EH.");
            Require(instructions[0].OpCode.Code == Code.Ldarg_0 && (!store || instructions[1].OpCode.Code == Code.Ldarg_1) && instructions[store ? 2 : 1].OpCode.Code == (store ? Code.Stfld : Code.Ldfld) && ReferenceEquals(instructions[store ? 2 : 1].Operand, field) && instructions[instructions.Count - 1].OpCode.Code == Code.Ret, "Changed target field/load/store body.");
        }
        private static string FieldKey(FieldDefinition f) => f.Name + "|" + (int)f.Attributes + "|" + f.FieldType.FullName + "|" + f.FieldType.Scope.Name + "|" + (f.HasConstant ? Convert.ToString(f.Constant, System.Globalization.CultureInfo.InvariantCulture) : "-");
        private static string MethodKey(MethodDefinition m, ushort flags) => m.Name + "|" + flags + "|" + m.ReturnType.FullName + "|" + m.ReturnType.Scope.Name + "|" + string.Join(",", m.Parameters.Select(p => p.Name + "@" + p.ParameterType.FullName + "@" + p.ParameterType.Scope.Name + "#" + (int)p.Attributes));
        private static void CheckAttributes(IEnumerable<CustomAttribute> attributes, string[] expected)
        {
            var actual = attributes.ToArray(); Require(actual.Length == expected.Length, "Changed custom attribute count.");
            for (int i = 0; i < actual.Length; ++i)
            {
                Scope(actual[i].AttributeType); Scope(actual[i].Constructor.DeclaringType);
                Require(actual[i].Constructor.HasThis && !actual[i].Constructor.ExplicitThis && actual[i].Constructor.CallingConvention == MethodCallingConvention.Default && !actual[i].Constructor.HasGenericParameters, "Changed attribute constructor convention.");
                Scope(actual[i].Constructor.ReturnType);
                foreach (var parameter in actual[i].Constructor.Parameters) Scope(parameter.ParameterType);
                string key = actual[i].AttributeType.FullName + "|" + actual[i].Constructor.FullName + "|" + BitConverter.ToString(actual[i].GetBlob()).Replace("-", "");
                Require(key == expected[i], "Changed custom attribute order/payload.");
            }
        }
        private static void Scope(TypeReference type)
        {
            if (type is GenericParameter parameter)
            {
                var owner = parameter.Owner as TypeDefinition;
                Require(owner != null && ProviderTypes.Any(c => c.Name == owner.FullName) && parameter.Type == GenericParameterType.Type && parameter.Position >= 0 && parameter.Position < owner.GenericParameters.Count && ReferenceEquals(owner.GenericParameters[parameter.Position], parameter), "Changed provider generic signature owner.");
                return;
            }
            if (type is GenericInstanceType generic) foreach (var argument in generic.GenericArguments) Scope(argument);
            if (type is TypeSpecification specification) { Scope(specification.ElementType); return; }
            string expectedName = type.FullName == "Hardlight.GameInput" ? "HLAutoGenerated"
                : type.FullName.StartsWith("Hardlight.", StringComparison.Ordinal) ? "HLInput.Runtime.dll"
                : type.FullName.StartsWith("System.", StringComparison.Ordinal) ? "netstandard"
                : type.FullName == "UnityEngine.Touch" ? "UnityEngine.InputLegacyModule"
                : type.FullName.StartsWith("UnityEngine.", StringComparison.Ordinal) ? "UnityEngine.CoreModule"
                : type.FullName.StartsWith("Unity.IL2CPP.CompilerServices.", StringComparison.Ordinal) ? "HLUnityCore.Runtime" : null;
            Require(expectedName != null && type.Scope != null && type.Scope.Name == expectedName, "Changed exact declared type provider.");
            bool value = type.FullName == "System.Single" || type.FullName == "UnityEngine.Touch" || type.FullName == "UnityEngine.Vector3" || type.FullName == "System.Boolean" || type.FullName == "System.Int32" || type.FullName == "Hardlight.GameInput" || type.FullName == "Hardlight.InputType" || type.FullName == "Hardlight.InputSwipe/Direction" || type.FullName == "Hardlight.FixedBindingsGlyphMap/FixedBindingsData" || type.FullName == "UnityEngine.KeyCode" || type.FullName == "Unity.IL2CPP.CompilerServices.Option";
            Require(type.IsValueType == value, "Changed declared value/reference signature.");
            if (type.Scope is ModuleDefinition module) { Require(module.Name == "HLInput.Runtime.dll" && module.Assembly.Name.FullName == LocalAssembly, "Changed local type scope."); return; }
            var scope = type.Scope as AssemblyNameReference; Require(scope != null, "Unsupported type scope.");
            string expected = scope.Name == "netstandard" ? "netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"
                : scope.Name == "HLAutoGenerated" || scope.Name == "HLUnityCore.Runtime" || scope.Name == "UnityEngine.CoreModule" || scope.Name == "UnityEngine.InputLegacyModule" ? scope.Name + ", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" : null;
            Require(expected != null && scope.FullName == expected, "Changed declared type scope.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

        // The table layout follows the existing codec preservation reader. The
        // containing stream and file-backed section are also bounded here.
        private sealed class MethodTable
        {
            private readonly byte[] image;
            private readonly int start, stride;
            private readonly uint count;
            internal MethodTable(byte[] image)
            {
                this.image = image;
                Require(U16(0) == 0x5a4d, "Missing DOS header.");
                int pe = checked((int)U32(0x3c));
                Require(U32(pe) == 0x00004550, "Missing PE header.");
                int sections = U16(pe + 6), optional = checked(pe + 24);
                int optionalSize = U16(pe + 20), sectionTable = checked(optional + optionalSize);
                Bounds(optional, optionalSize); Bounds(sectionTable, checked(sections * 40));
                ushort magic = U16(optional);
                Require(magic == 0x10b || magic == 0x20b, "Unsupported PE magic.");
                int directoryOffset = magic == 0x10b ? 96 : 112;
                Require(optionalSize >= directoryOffset + 15 * 8 && U32(optional + directoryOffset - 4) >= 15, "Missing CLI directory.");
                int directories = checked(optional + directoryOffset);
                Require(U32(directories + 4 * 8) == 0 && U32(directories + 4 * 8 + 4) == 0 && U32(optional + 64) == 0, "Signed/checksummed image cannot be restored verbatim.");
                Func<uint, uint, int> rva = (address, size) => {
                    int result = -1;
                    for (int i = 0; i < sections; ++i) {
                        int row = checked(sectionTable + i * 40);
                        uint va = U32(row + 12), raw = U32(row + 16), ptr = U32(row + 20);
                        if (address >= va && (ulong)address - va < raw && (ulong)address - va + size <= raw) {
                            Require(result < 0, "Ambiguous file-backed RVA.");
                            result = checked((int)((ulong)ptr + address - va)); Bounds(result, checked((int)size));
                        }
                    }
                    Require(result >= 0, "RVA outside file-backed sections."); return result;
                };
                uint cliSize = U32(directories + 14 * 8 + 4);
                Require(cliSize >= 72, "Truncated CLI header.");
                int cli = rva(U32(directories + 14 * 8), cliSize);
                Require(U32(cli) >= 72 && U32(cli) <= cliSize && (U32(cli + 16) & 8) == 0 && U32(cli + 32) == 0 && U32(cli + 36) == 0, "Unexpected strong-name header.");
                uint metadataSize = U32(cli + 12);
                int metadata = rva(U32(cli + 8), metadataSize), metadataEnd = checked(metadata + (int)metadataSize);
                Require(metadataSize >= 20 && U32(metadata) == 0x424a5342, "Missing CLR metadata.");
                int versionEnd = checked(metadata + 16 + (int)U32(metadata + 12));
                int header = checked((versionEnd + 3) & ~3);
                Require(header >= metadata + 16 && header + 4 <= metadataEnd, "Version outside metadata.");
                int streams = U16(header + 2), cursor = header + 4, tables = -1, tablesEnd = -1;
                var labels = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < streams; ++i) {
                    Require(cursor >= metadata && (long)cursor + 8 <= metadataEnd, "Stream header outside metadata.");
                    uint offset = U32(cursor), size = U32(cursor + 4);
                    int name = cursor + 8, end = name;
                    while (end < metadataEnd && Byte(end) != 0) ++end;
                    Require(end < metadataEnd && end - name <= 32, "Invalid stream name.");
                    string label = System.Text.Encoding.ASCII.GetString(image, name, end - name);
                    Require(labels.Add(label) && (ulong)offset + size <= metadataSize, "Duplicate/out-of-range stream.");
                    Require(label != "#-", "Unoptimized pointer-table metadata is unsupported.");
                    if (label == "#~") { tables = checked(metadata + (int)offset); tablesEnd = checked(tables + (int)size); }
                    cursor = checked((end + 4) & ~3);
                    Require(cursor <= metadataEnd, "Aligned stream header outside metadata.");
                }
                Require(tables >= cursor && (long)tables + 24 <= tablesEnd, "Missing/overlapping table stream.");
                byte heaps = Byte(tables + 6); ulong valid = U64(tables + 8); cursor = tables + 24;
                Require((heaps & ~7) == 0 && (valid & ((1UL << 3) | (1UL << 5) | (1UL << 7))) == 0, "Unexpected heap/pointer-table flags.");
                var rows = new uint[64];
                for (int i = 0; i < 64; ++i) if ((valid & (1UL << i)) != 0) {
                    Require((long)cursor + 4 <= tablesEnd, "Row counts outside table stream."); rows[i] = U32(cursor); cursor = checked(cursor + 4);
                }
                Func<int, int> index = id => rows[id] >= 65536 ? 4 : 2;
                Func<int, int[], int> coded = (bits, ids) => ids.Max(id => rows[id]) >= (1U << (16 - bits)) ? 4 : 2;
                int str = (heaps & 1) == 0 ? 2 : 4, guid = (heaps & 2) == 0 ? 2 : 4, blob = (heaps & 4) == 0 ? 2 : 4;
                int[] widths = { 2 + str + guid * 3, coded(2, new[] { 0, 26, 35, 1 }) + str * 2, 4 + str * 2 + coded(2, new[] { 2, 1, 27 }) + index(4) + index(6), index(4), 2 + str + blob, index(6) };
                for (int i = 0; i < 6; ++i) cursor = checked(cursor + checked((int)rows[i] * widths[i]));
                start = cursor; count = rows[6]; stride = 8 + str + blob + index(8);
                Require(count > 0 && (long)start + (long)count * stride <= tablesEnd, "MethodDef rows outside table stream.");
                Bounds(start, checked((int)count * stride));
            }
            internal int FlagsOffset(uint rid) { Require(rid > 0 && rid <= count, "MethodDef RID outside table."); return checked(start + ((int)rid - 1) * stride + 6); }
            private void Bounds(int offset, int size) { Require(offset >= 0 && size >= 0 && (long)offset + size <= image.Length, "PE read outside image."); }
            private byte Byte(int offset) { Bounds(offset, 1); return image[offset]; }
            internal ushort U16(int offset) { Bounds(offset, 2); return (ushort)(image[offset] | image[offset + 1] << 8); }
            private uint U32(int offset) { Bounds(offset, 4); return (uint)(image[offset] | image[offset + 1] << 8 | image[offset + 2] << 16 | image[offset + 3] << 24); }
            private ulong U64(int offset) => U32(offset) | ((ulong)U32(offset + 4) << 32);
            internal void SetU16(int offset, ushort value) { Bounds(offset, 2); image[offset] = (byte)value; image[offset + 1] = (byte)(value >> 8); }
        }
    }
}
