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
            Assert.AreEqual(gates[0].Offset, JumpIfFalse(body, gate).Offset, "The gate tests the getter result itself");
            Assert.Greater(gates[0].Target, body[gameCenter].Offset);
            Assert.LessOrEqual(gates[0].Target, body[vibration].Offset);
            // Nothing gates the vibration lookup: native 0x649c64..0x649c8d holds only
            // the il2cpp class-init check between the GameCenter call and GetSystemRef.
            Assert.IsEmpty(body.Skip(gameCenter + 1).Take(vibration - gameCenter - 1).Where(IsConditional).ToList(),
                "No condition between the GameCenter registration and the vibration lookup.");
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
            AssertPlatformPaths(enter, body, body.IndexOf(calls[1]), body.IndexOf(calls[3]),
                body.IndexOf(calls[4]), body.IndexOf(calls[5]));
            // The store and Addressables arguments are the shipped literals.
            CollectionAssert.AreEqual(new[] { "H4RDl1ght_S4VE", "Primary", "HL_Default_Save_ID" },
                body.Where(i => i.Code == OpCodes.Ldstr).Select(i => enter.Module.ResolveString(i.Operand)).ToArray());
            Assert.IsEmpty(enter.GetMethodBody().ExceptionHandlingClauses);
        }

        // Native 0x53c2df `cmp eax, 1; jne 0x53c32b`: only a platform other than
        // OSXPlayer jumps to the `mov edi, 0x3c` targetFrameRate path, and OSXPlayer
        // falls through to SetResolution then `jmp 0x53c348` over it. The refresh
        // argument is `movabs rcx, 0x3c00000001` at 0x53c2fe: RefreshRate.numerator
        // (offset 0) is 1 and denominator (offset 4) is 60. Catches `!=`, swapped
        // paths and a 60/1 or otherwise changed refresh fraction.
        private static void AssertPlatformPaths(MethodInfo enter, List<Instruction> body, int platform,
            int fullScreen, int resolution, int frameRateCall)
        {
            Instruction branch = JumpIfNotEqual(body, platform + 1);
            int elseStart = body.FindIndex(i => i.Offset == branch.Target);
            Assert.Greater(elseStart, resolution, "Only the non-macOS path is skipped to");
            Assert.LessOrEqual(elseStart, frameRateCall - 1, "The jump lands on the frame-rate path");
            for (int i = elseStart; i < frameRateCall - 1; i++) Assert.AreEqual(OpCodes.Nop, body[i].Code);
            Instruction exit = body[elseStart - 1];
            Assert.IsTrue(exit.Code == OpCodes.Br || exit.Code == OpCodes.Br_S, "macOS path ends with " + exit.Code);
            Assert.Greater(exit.Target, body[frameRateCall].Offset, "macOS path jumps over set_targetFrameRate");
            Assert.AreEqual(1, body.Skip(platform + 1).Take(frameRateCall - platform - 1).Count(IsConditional));

            var refresh = new Dictionary<string, int>();
            for (int i = fullScreen + 1; i < resolution; i++)
            {
                if (body[i].Code != OpCodes.Stfld) continue;
                FieldInfo field = enter.Module.ResolveField(body[i].Operand);
                Assert.AreEqual(typeof(RefreshRate), field.DeclaringType, field.Name);
                refresh.Add(field.Name, Constant(body[i - 1]));
            }
            CollectionAssert.AreEquivalent(new Dictionary<string, int> { { "numerator", 1 }, { "denominator", 60 } }, refresh);
        }

        // Release IL branches with bne.un; Debug IL materialises ceq into a local
        // and tests it with brfalse. Either form jumps exactly when unequal.
        private static Instruction JumpIfNotEqual(List<Instruction> body, int compare)
        {
            Instruction next = body[compare + 1];
            if (next.Code == OpCodes.Bne_Un || next.Code == OpCodes.Bne_Un_S) return next;
            Assert.AreEqual(OpCodes.Ceq, next.Code, "Expected bne.un or ceq after the platform operand");
            Assert.AreEqual(Local(body[compare + 2], "stloc"), Local(body[compare + 3], "ldloc"));
            Instruction jump = body[compare + 4];
            Assert.IsTrue(jump.Code == OpCodes.Brfalse || jump.Code == OpCodes.Brfalse_S, "Expected brfalse, found " + jump.Code);
            return jump;
        }

        // Native 0x649c4c `cmp byte ptr [rax + 0x2b], 0; je 0x649c64`: a false
        // GameCenter jumps past the registration call 0x649c5f. Release IL is
        // `brfalse` straight after the getter; Debug IL stores the getter result in a
        // local and reloads it for `brfalse`. A negated test emits brtrue, or
        // `ldc.i4.0; ceq` before the local, so neither form matches.
        private static Instruction JumpIfFalse(List<Instruction> body, int getter)
        {
            Instruction next = body[getter + 1];
            if (next.Code == OpCodes.Brfalse || next.Code == OpCodes.Brfalse_S) return next;
            Assert.AreEqual(Local(next, "stloc"), Local(body[getter + 2], "ldloc"), "Expected the getter result in a local");
            Instruction jump = body[getter + 3];
            Assert.IsTrue(jump.Code == OpCodes.Brfalse || jump.Code == OpCodes.Brfalse_S, "Expected brfalse, found " + jump.Code);
            return jump;
        }

        private static int Local(Instruction instruction, string prefix)
        {
            string name = instruction.Code.Name;
            Assert.IsTrue(name == prefix || name.StartsWith(prefix + "."), "Expected " + prefix + ", found " + name);
            string suffix = name == prefix ? "s" : name.Substring(prefix.Length + 1);
            return suffix == "s" ? instruction.Operand : int.Parse(suffix);
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
