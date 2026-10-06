using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
namespace ProjectLucid
{
    public static class CharacterControlsVerification
    {
        private static void Check(bool value, string message, ref int checks)
        { if (!value) throw new InvalidOperationException(message); checks++; }
        private static void Set(object target, string name, object value)
        { target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
        private static T Raw<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        // Raw managed instances here only test ordinary field/property/callback paths.
        // They do not represent valid live Unity objects or test engine lifecycle/constructors.
        private static GameControlsMappingDefinition Mapping(GameInput gamepad, GameInput keyboard, GameInput touch)
        {
            var d = Raw<GameControlsMappingDefinition>();
            Set(d, "m_gamepadBindings", new List<GameControlsBinding> { new GameControlsBinding(gamepad) });
            Set(d, "m_keyboardBindings", new List<GameControlsBinding> { new GameControlsBinding(keyboard) });
            Set(d, "m_touchBindings", new List<GameControlsBinding> { new GameControlsBinding(touch) });
            return d;
        }
        private static CharacterControlsMappingDefinition.CharacterControlsMapping Entry(GameAction action, GameControlsMappingDefinition primary, GameControlsMappingDefinition secondary)
        {
            var e = new CharacterControlsMappingDefinition.CharacterControlsMapping();
            Set(e, "m_action", action); Set(e, "m_mapping", primary); Set(e, "m_mappingSecondary", secondary); return e;
        }
        // Test-only callback spy implementing the genuine abstract InputModifier contract.
        // No new original type/body credit or registered gameplay host is claimed.
        private class ModifierProbe : InputModifier
        {
            public Func<float, float, GameInput, float> Callback;
            public override float Modify(float value, float deltaTime) { return Callback(value, deltaTime, (GameInput)0); }
            public override float Modify(float value, float deltaTime, GameInput input) { return Callback(value, deltaTime, input); }
            public float DispatchOriginal(float value, float deltaTime, GameInput input) { return base.Modify(value, deltaTime, input); }
        }
        private class ConstantProbe : GlobalConstantDefinition
        {
            public int Calls;
            protected override void UpdateCachedValues() { Calls++; }
            public void CallAwake() { base.Awake(); }
            public void CallValidate() { base.OnValidate(); }
            public void CallEmptyBase() { base.UpdateCachedValues(); }
        }
        public static int RunManaged()
        {
            int checks = 0;
            var camera = new CharacterControlsMappingDefinition.CameraControlsMapping();
            Check(camera.Left == GameInput.Left, "camera left native default", ref checks);
            Check(camera.Right == GameInput.Right, "camera right native default", ref checks);
            Check(camera.Up == GameInput.Up, "camera up native default", ref checks);
            Check(camera.Down == GameInput.Down, "camera down native default", ref checks);
            var blank = new CharacterControlsMappingDefinition.CharacterControlsMapping();
            Check((int)blank.Action == 0 && ReferenceEquals(blank.Mapping, null) && ReferenceEquals(blank.MappingSecondary, null), "entry zero/null ctor", ref checks);
            var binding = new GameControlsBinding(GameInput.Left);
            Check(binding.GameInput == GameInput.Left, "binding ctor input", ref checks);
            float value = 4f; binding.ApplyModifiers(ref value, .25f);
            Check(value == 4f, "null modifiers preserve value", ref checks);
            Set(binding, "m_modifiers", new List<InputModifier>());
            binding.ApplyModifiers(ref value, .25f); Check(value == 4f, "empty modifiers preserve value", ref checks);
            value = float.NaN; binding.ApplyModifiers(ref value, float.NaN);
            Check(float.IsNaN(value), "empty list preserves NaN", ref checks);
            var first = Raw<ModifierProbe>(); var second = Raw<ModifierProbe>();
            var order = new List<int>(); GameInput secondInput = default(GameInput); float secondValue = 0;
            first.Callback = (v, dt, input) => { order.Add(1); Set(binding, "m_gameInput", GameInput.Right); Set(binding, "m_modifiers", new List<InputModifier>()); return v + dt; };
            second.Callback = (v, dt, input) => { order.Add(2); secondInput = input; secondValue = v; return v * 2; };
            Set(binding, "m_modifiers", new List<InputModifier> { first, second }); value = 3;
            binding.ApplyModifiers(ref value, .5f);
            Check(order.Count == 2 && order[0] == 1 && order[1] == 2, "captured-list enumeration retains callback order", ref checks);
            Check(secondInput == GameInput.Right, "next callback reads live input", ref checks);
            Check(secondValue == 3.5f && value == 7f, "each result commits before next callback", ref checks);
            first.Callback = (v, dt, input) => v + 1f;
            Set(binding, "m_modifiers", new List<InputModifier> { first, null }); value = 10f;
            bool threw = false; try { binding.ApplyModifiers(ref value, 0); } catch (NullReferenceException) { threw = true; }
            Check(threw && value == 11f, "managed null entry keeps earlier result; no optimized native-null exception parity", ref checks);
            second.Callback = (v, dt, input) => { throw new InvalidOperationException("probe"); };
            Set(binding, "m_modifiers", new List<InputModifier> { first, second }); value = 5f;
            threw = false; try { binding.ApplyModifiers(ref value, 0); } catch (InvalidOperationException ex) { threw = ex.Message == "probe"; }
            Check(threw && value == 6f, "throwing callback leaves preceding result", ref checks);
            var live = new List<InputModifier> { first }; Set(binding, "m_modifiers", live);
            first.Callback = (v, dt, input) => { live.Add(second); return v + 2; }; value = 1;
            threw = false; try { binding.ApplyModifiers(ref value, 0); } catch (InvalidOperationException) { threw = true; }
            Check(threw && value == 3f, "same-list mutation preserves foreach invalidation", ref checks);
            float captured = 2f;
            first.Callback = (v, dt, input) => { captured = 99f; return v + 4f; };
            Set(binding, "m_modifiers", new List<InputModifier> { first }); binding.ApplyModifiers(ref captured, 0);
            Check(captured == 6f, "return assignment occurs after callback alias mutation", ref checks);
            first.Callback = (v, dt, input) => v - dt;
            Check(first.DispatchOriginal(9, 2, GameInput.Down) == 7f, "original three-arg overload dispatches two-arg virtual", ref checks);
            var vector = first.Modify(new Vector3(1, -2, float.NaN), 4);
            Check(vector.x == 1 && vector.y == -2 && float.IsNaN(vector.z), "original vector overload identity", ref checks);
            first.Reset(); Check(first.ModifierType == InputModifier.InputModifierType.Float, "reset genuine empty body preserves enum", ref checks);
            var constant = Raw<ConstantProbe>(); constant.CallAwake(); constant.CallValidate(); constant.CallEmptyBase();
            Check(constant.Calls == 2, "Awake/OnValidate virtual cache callbacks and empty base", ref checks);
            typeof(GlobalConstantDefinition).GetField("m_globalConstantType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(constant, (GlobalConstantType)(-77));
            Check((int)constant.Type == -77, "constant type getter preserves raw bits", ref checks);
            var d = Raw<CharacterControlsMappingDefinition>();
            var primary = Mapping(GameInput.Left, GameInput.Right, GameInput.Up);
            var secondary = Mapping(GameInput.Down, GameInput.Up, GameInput.Right);
            var list = new List<CharacterControlsMappingDefinition.CharacterControlsMapping> { Entry((GameAction)11, primary, secondary), Entry((GameAction)11, primary, null) };
            Set(d, "m_controlsMapping", list);
            Set(d, "m_movementMapping", new SerializableDictionary<GameAction, GameInput>(HardlightProject.HardlightEnumComparers.GameActionComparer));
            foreach (InputType input in Enum.GetValues(typeof(InputType)))
            {
                GameInput result; Check(d.TryGetGameInput(input, (GameAction)11, out result), "first matching action found", ref checks);
                GameInput expected = input == InputType.Touch ? GameInput.Right : input == InputType.Keyboard ? GameInput.Up : GameInput.Down;
                Check(result == expected, "secondary mapping and device branch", ref checks);
            }
            list[0] = Entry((GameAction)11, primary, null);
            foreach (InputType input in new[] { InputType.Touch, InputType.Keyboard, InputType.Unknown, (InputType)int.MinValue })
            {
                GameInput result; Check(d.TryGetGameInput(input, (GameAction)11, out result), "primary mapping found", ref checks);
                Check(result == (input == InputType.Touch ? GameInput.Up : input == InputType.Keyboard ? GameInput.Right : GameInput.Left), "primary device selection", ref checks);
            }
            Set(d, "m_movementMapping", new SerializableDictionary<GameAction, GameInput>(HardlightProject.HardlightEnumComparers.GameActionComparer) { { (GameAction)12, GameInput.Down } });
            GameInput selected; Check(d.TryGetGameInput(InputType.Touch, (GameAction)12, out selected) && selected == GameInput.Down, "movement fallback ignores device", ref checks);
            Check(!d.TryGetGameInput(InputType.Keyboard, (GameAction)13, out selected) && (int)selected == 0, "absent action default out", ref checks);
            primary.TouchInputs.Clear(); threw = false;
            try { d.TryGetGameInput(InputType.Touch, (GameAction)11, out selected); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "authored matching empty binding does not fall back", ref checks);
            Set(d, "m_decoratorMapping", new SerializableDictionary<GameAction, Texture2D>(HardlightProject.HardlightEnumComparers.GameActionComparer) { { (GameAction)1, Raw<Texture2D>() } });
            Set(d, "m_decoratorInputTypes", new List<InputType> { InputType.Touch });
            Texture2D image; Check(d.TryGetDecoratorImage(InputType.Touch, (GameAction)1, out image) && !ReferenceEquals(image, null), "eligible decorator", ref checks);
            var found = image; Check(!d.TryGetDecoratorImage(InputType.Keyboard, (GameAction)1, out image) && ReferenceEquals(found, image), "ineligible decorator still assigned", ref checks);
            Set(d, "m_decoratorInputTypes", null);
            Check(!d.TryGetDecoratorImage(InputType.Keyboard, (GameAction)2, out image) && ReferenceEquals(image, null), "missing decorator short-circuits input list", ref checks);
            threw = false; try { d.TryGetDecoratorImage(InputType.Touch, (GameAction)1, out image); } catch (NullReferenceException) { threw = true; }
            Check(threw && ReferenceEquals(image, found), "managed eligible-list null faults after out assignment", ref checks);
            return checks;
        }
        public static int RunEngine()
        {
            int checks = 0;
            GlobalConstantDefinition_Character constants = null;
            CharacterControlsMappingDefinition controls = null;
            GameControlsMappingDefinition mapping = null;
            try
            {
                constants = ScriptableObject.CreateInstance<GlobalConstantDefinition_Character>();
                controls = ScriptableObject.CreateInstance<CharacterControlsMappingDefinition>();
                mapping = ScriptableObject.CreateInstance<GameControlsMappingDefinition>();
                Check(constants.InputForwardScalingMultiplier == 1f && constants.InputBackwardScalingMultiplier == 1f, "engine constants input defaults", ref checks);
                Check(constants.MovementInputSpeedMin == 1f && constants.MovementStationarySlopeAngleMax == 45f && constants.MovementOrientateToSlopeAngleMax == 90f, "engine movement defaults", ref checks);
                Check(constants.MovementOrientateToVelocityMinAngleToWorldUp == 5f && constants.GripRestoreTime == 1f && constants.BoostRestoreTimeFromHalfPipe == 1f, "engine velocity/grip defaults", ref checks);
                Check(constants.ControlsUpAngleInversionThreshold == 135f && constants.ControlsForwardAngleInversionThreshold == 120f && constants.StickyControlsInputDeadZone == .5f, "engine controls defaults", ref checks);
                Check(constants.CameraToCharacterScreenSpaceAngleMin == 25f && constants.CameraToCharacterScreenSpaceAngleMax == 60f && constants.RailSwitchRequiresJump, "engine camera/rail defaults", ref checks);
                Check(constants.RailDetachAirControlsLockTime == .5f && constants.CameraForwardToGravityParallelDotThreshold == .95f && constants.DetectTrackingRaycastDistance == 10f, "engine tracking defaults", ref checks);
                Check(constants.HomingTargetGracePeriodSeconds == 0f && constants.HomingTargetHitDistance == 1f && constants.CharacterToSurfaceParallelAngleThreshold == 15f, "engine homing defaults", ref checks);
                Check(constants.QuickSwitchCooldownTime == .5f && constants.ChaosDashAirControlsLockTime == .5f && ReferenceEquals(constants.ControlsMappingDefinition, null), "engine cooldown/null defaults", ref checks);
                Check(constants.MovementInputSpeedMinSqr == 1f, "engine Awake square", ref checks);
                Check(constants.MovementStationarySlopeCosineAngleMax == Mathf.Cos(45f * Mathf.Deg2Rad), "engine Awake slope cache", ref checks);
                Check(constants.MovementOrientateToVelocityMinCosineAngleToWorldUp == Mathf.Cos(5f * Mathf.Deg2Rad), "engine Awake orientation cache", ref checks);
                Check(constants.CameraToCharacterScreenSpaceCosineAngleMin == Mathf.Cos(25f * Mathf.Deg2Rad), "engine Awake camera minimum cache", ref checks);
                Check(constants.CameraToCharacterScreenSpaceCosineAngleMax == Mathf.Cos(60f * Mathf.Deg2Rad), "engine Awake camera maximum cache", ref checks);
                Check(constants.CharacterToSurfaceParallelCosineAngleThreshold == Mathf.Cos(15f * Mathf.Deg2Rad), "engine Awake surface cache", ref checks);
                Check(controls.ControlsMapping.Count == 0 && controls.CameraMapping.Left == GameInput.Left, "engine control field initializers", ref checks);
                GameInput input; Check(!controls.TryGetGameInput(InputType.Keyboard, (GameAction)9, out input) && (int)input == 0, "engine empty movement map", ref checks);
                Texture2D image; Check(!controls.TryGetDecoratorImage(InputType.Touch, (GameAction)9, out image) && ReferenceEquals(image, null), "engine empty decorator map", ref checks);
                Check(ReferenceEquals(mapping.GamepadInputs, null) && ReferenceEquals(mapping.KeyboardInputs, null) && ReferenceEquals(mapping.TouchInputs, null), "engine mapping null lists", ref checks);
            }
            finally
            {
                try { if (!ReferenceEquals(mapping, null)) UnityEngine.Object.DestroyImmediate(mapping); }
                finally { try { if (!ReferenceEquals(controls, null)) UnityEngine.Object.DestroyImmediate(controls); }
                    finally { if (!ReferenceEquals(constants, null)) UnityEngine.Object.DestroyImmediate(constants); } }
            }
            return checks;
        }
    }
}
