using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace ProjectLucid.Tests
{
    // Read-only IL of the rebuilt enter bodies, compared with the shipped x86-64
    // call order. Neither body is entered: both acquire global App, registry,
    // configuration, property-store and Addressables services.
    public sealed class OriginalApplicationEnterOrderTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ApplicationStateLoading.DoOnEnter 0x060006ae, x86-64 0x649a30. Each
        // RegisterAndInitialiseSystem call passes its own RuntimeMethod_var slot
        // (0x649b77..0x649c38): CameraStack, Save, Progression, Cutscene, Ornament,
        // GameCenterAccessPoint, UINavigation, Waypoint, Mission, MusicTrack,
        // CharacterCustomisation. GetConfig<CoreGameConfiguration> 0x649c47 then a
        // byte test at +0x2b gates GameCenter 0x649c5f before GetSystemRef<VibrationManager>
        // 0x649c8d. Swapping SaveManager and ProgressionManager, or making the
        // GameCenter registration unconditional, fails here.
        [Test]
        public void LoadingEnterRegistersSystemsInShippedOrderWithOnlyGameCenterGated()
        {
            MethodInfo enter = typeof(ApplicationStateLoading).GetMethod("DoOnEnter", Declared);
            Assert.IsNotNull(enter);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(IsCall).ToList();
            string[] expected =
            {
                Call(typeof(FSMState), "DoOnEnter"),
                Call(typeof(IGraphUserExtensions), "GetAs", typeof(App)),
                Call(typeof(App), "InitialiseProcessManagerSystems"),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(CameraStackManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(SaveManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(ProgressionManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(CutsceneManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(OrnamentManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(GameCenterAccessPointManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(UINavigationManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(WaypointManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(MissionManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(MusicTrackManager)),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(CharacterCustomisationManager)),
                Call(typeof(SystemConfiguration), "GetConfig", typeof(CoreGameConfiguration)),
                Call(typeof(CoreGameConfiguration), "get_GameCenter"),
                Call(typeof(App), "RegisterAndInitialiseSystem", typeof(GameCenter)),
                // HLHaptics.Runtime is not an EditMode reference; compare its names.
                "Hardlight.ProcessManager::GetSystemRef<Hardlight.VibrationManager>",
                "System.Action`1[Hardlight.VibrationManager]::.ctor",
                "Hardlight.SystemRef`1[Hardlight.VibrationManager]::InvokeOnValid",
            };
            CollectionAssert.AreEqual(expected, calls.Select(c => Describe(enter, c)).ToArray());

            // Only the GameCenter registration is conditional: one forward branch
            // after the getter skips exactly that call and lands before the
            // vibration lookup. The cached-callback null test follows it.
            int gate = body.IndexOf(calls[15]);
            int gameCenter = body.IndexOf(calls[16]);
            int vibration = body.IndexOf(calls[17]);
            List<Instruction> beforeGate = body.Take(gate).Where(IsConditional).ToList();
            Assert.IsEmpty(beforeGate, "The eleven registrations and initialisation are unconditional.");
            List<Instruction> gates = body.Skip(gate + 1).Take(gameCenter - gate - 1).Where(IsConditional).ToList();
            Assert.AreEqual(1, gates.Count);
            Assert.Greater(gates[0].Target, body[gameCenter].Offset);
            Assert.LessOrEqual(gates[0].Target, body[vibration].Offset);
            // No rollback or handler surrounds the ordered registrations.
            Assert.IsEmpty(enter.GetMethodBody().ExceptionHandlingClauses);
        }

        // ApplicationStateBoot.DoOnEnter 0x0600063d, x86-64 0x53c1b0. Base enter
        // 0x53c2c0; get_platform compared with 1 (OSXPlayer) 0x53c2df; SetResolution
        // 0x53c313 with RefreshRate 0x3c00000001; else targetFrameRate 0x3c 0x53c343;
        // HLPropertyStore ctor 0x53c376, RegisterSystem 0x53c39f, LoadFromIdentifier
        // 0x53c3b3; GetConfig<GameLevels>/CacheLevels 0x53c3c2/0x53c3cc; LoadSceneAsync
        // 0x53c407 precedes the Storage read and TryGetValue<Dictionary> 0x53c4d3,
        // op_Implicit 0x53c52d, set_Item 0x53c553 and SetValue<Dictionary>. Reading
        // LoadedLevels before the load request, or caching levels after it, fails here.
        [Test]
        public void BootEnterKeepsDisplayStoreCacheThenLoadBeforeLoadedLevelsRead()
        {
            MethodInfo enter = typeof(ApplicationStateBoot).GetMethod("DoOnEnter", Declared);
            Assert.IsNotNull(enter);
            List<Instruction> body = Read(enter);
            List<Instruction> calls = body.Where(IsCall).ToList();
            Type levels = typeof(Dictionary<string, AsyncOperationHandle>);
            string[] expected =
            {
                Call(typeof(FSMState), "DoOnEnter"),
                Call(typeof(Application), "get_platform"),
                Call(typeof(Screen), "get_mainWindowDisplayInfo"),
                Call(typeof(Screen), "get_fullScreenMode"),
                Call(typeof(Screen), "SetResolution"),
                Call(typeof(Application), "set_targetFrameRate"),
                Call(typeof(HLPropertyStore), ".ctor"),
                Call(typeof(ProcessManager), "RegisterSystem"),
                Call(typeof(HLPropertyStore), "LoadFromIdentifier"),
                Call(typeof(SystemConfiguration), "GetConfig", typeof(GameLevels)),
                Call(typeof(GameLevels), "CacheLevels"),
                Call(typeof(Addressables), "LoadSceneAsync"),
                Call(typeof(IGraphUser), "get_Storage"),
                Call(typeof(IGraphStorageExtensions), "TryGetValue", levels),
                Call(levels, ".ctor"),
                Call(typeof(AsyncOperationHandle<SceneInstance>), "op_Implicit"),
                Call(levels, "set_Item"),
                Call(typeof(IGraphUser), "get_Storage"),
                Call(typeof(IGraphStorage), "SetValue", levels),
            };
            CollectionAssert.AreEqual(expected, calls.Select(c => Describe(enter, c)).ToArray());

            // Platform constant 1 is RuntimePlatform.OSXPlayer; the frame rate
            // operand is the shipped 60. Both are pushed adjacent to their calls.
            Assert.AreEqual(1, (int)RuntimePlatform.OSXPlayer);
            Instruction platform = body[body.IndexOf(calls[1]) + 1];
            Assert.AreEqual(1, Constant(platform), "get_platform is compared with OSXPlayer");
            Instruction frameRate = body[body.IndexOf(calls[5]) - 1];
            Assert.AreEqual(60, Constant(frameRate), "non-macOS target frame rate");
            // The store and Addressables arguments are the shipped literals.
            CollectionAssert.AreEqual(new[] { "H4RDl1ght_S4VE", "Primary", "HL_Default_Save_ID" },
                body.Where(i => i.Code == OpCodes.Ldstr).Select(i => enter.Module.ResolveString(i.Operand)).ToArray());
            Assert.IsEmpty(enter.GetMethodBody().ExceptionHandlingClauses);
        }

        private struct Instruction
        {
            public int Offset;
            public OpCode Code;
            public int Operand;
            public int Target;
            public long Wide;
        }

        private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(code => code.Value);

        // Minimal ECMA-335 decoder: offsets, opcodes, tokens and branch targets.
        private static List<Instruction> Read(MethodInfo method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var result = new List<Instruction>();
            int i = 0;
            while (i < il.Length)
            {
                var instruction = new Instruction { Offset = i, Target = -1 };
                short value = il[i++];
                if (value == 0xfe) value = unchecked((short)(0xfe00 | il[i++]));
                Assert.IsTrue(Codes.TryGetValue(value, out instruction.Code), "Unknown IL opcode " + value);
                switch (instruction.Code.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                        instruction.Target = i + 1 + (sbyte)il[i];
                        i += 1;
                        break;
                    case OperandType.ShortInlineI:
                        instruction.Operand = instruction.Code == OpCodes.Ldc_I4_S ? (sbyte)il[i] : il[i];
                        i += 1;
                        break;
                    case OperandType.ShortInlineVar:
                        instruction.Operand = il[i];
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        instruction.Operand = BitConverter.ToUInt16(il, i);
                        i += 2;
                        break;
                    case OperandType.InlineBrTarget:
                        instruction.Target = i + 4 + BitConverter.ToInt32(il, i);
                        i += 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        instruction.Wide = BitConverter.ToInt64(il, i);
                        i += 8;
                        break;
                    case OperandType.InlineSwitch:
                        i += 4 + 4 * BitConverter.ToInt32(il, i);
                        break;
                    default:
                        instruction.Operand = BitConverter.ToInt32(il, i);
                        i += 4;
                        break;
                }
                result.Add(instruction);
            }
            return result;
        }

        private static bool IsCall(Instruction instruction) =>
            instruction.Code == OpCodes.Call || instruction.Code == OpCodes.Callvirt || instruction.Code == OpCodes.Newobj;

        private static bool IsConditional(Instruction instruction) =>
            instruction.Code.FlowControl == FlowControl.Cond_Branch;

        private static int Constant(Instruction instruction)
        {
            if (instruction.Code == OpCodes.Ldc_I4_S || instruction.Code == OpCodes.Ldc_I4) return instruction.Operand;
            OpCode[] shortForms = { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3,
                OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8 };
            int index = Array.IndexOf(shortForms, instruction.Code);
            Assert.GreaterOrEqual(index, 0, "Expected an Int32 constant, found " + instruction.Code);
            return index;
        }

        private static string Describe(MethodInfo owner, Instruction instruction) =>
            Describe(owner.Module.ResolveMethod(instruction.Operand));

        private static string Describe(MethodBase method)
        {
            string name = method.DeclaringType + "::" + method.Name;
            if (!method.IsGenericMethod) return name;
            return name + "<" + string.Join(",", method.GetGenericArguments().Select(t => t.ToString())) + ">";
        }

        private static string Call(Type owner, string name, params Type[] arguments)
        {
            string text = owner + "::" + name;
            return arguments.Length == 0 ? text : text + "<" + string.Join(",", arguments.Select(t => t.ToString())) + ">";
        }
    }
}
