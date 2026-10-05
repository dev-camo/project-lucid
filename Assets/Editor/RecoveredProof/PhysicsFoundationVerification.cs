using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class PhysicsFoundationVerification
    {
        private static int checks;
        private static void Require(bool condition, string description)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(description);
        }
        private static void Throws<T>(Action action, string description) where T : Exception
        {
            bool passed = false;
            try { action(); }
            catch (T) { passed = true; }
            Require(passed, description);
        }

        // Read genuine emitted IL without executing engine icalls or creating
        // substitute camera/collider/physics objects in the managed proof.
        private static List<(OpCode opcode, object operand)> ReadIL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null))
                .ToDictionary(c => unchecked((ushort)c.Value));
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            var result = new List<(OpCode, object)>();
            for (int i = 0; i < bytes.Length;)
            {
                ushort value = bytes[i++];
                if (value == 0xfe) value = (ushort)(0xfe00 | bytes[i++]);
                OpCode code = codes[value];
                object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = (sbyte)bytes[i]; i++; break;
                    case OperandType.ShortInlineVar: operand = bytes[i]; i++; break;
                    case OperandType.ShortInlineBrTarget: operand = i + 1 + (sbyte)bytes[i]; i++; break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, i); i += 2; break;
                    case OperandType.InlineBrTarget: operand = i + 4 + BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI8: operand = BitConverter.ToInt64(bytes, i); i += 8; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, i); i += 4; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, i); i += 8; break;
                    case OperandType.InlineMethod: operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineField: operand = method.Module.ResolveField(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineType: operand = method.Module.ResolveType(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineTok: operand = method.Module.ResolveMember(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineString: operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSig: operand = method.Module.ResolveSignature(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(bytes, i); i += 4;
                        operand = Enumerable.Range(0, count).Select(n => i + 4 * count + BitConverter.ToInt32(bytes, i + 4 * n)).ToArray(); i += 4 * count;
                        break;
                    default: throw new InvalidOperationException("Unsupported genuine IL operand " + code.OperandType);
                }
                result.Add((code, operand));
            }
            return result;
        }
        private static string CallName(MethodBase method) => method.DeclaringType.FullName + "." + method.Name;

        public static int RunManaged()
        {
            checks = 0;
            Type type = typeof(PhysicsUtilities);
            Require(type.FullName == "Hardlight.PhysicsUtilities" && type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original physics assembly/type identity");
            Require(type.IsPublic && type.IsAbstract && type.IsSealed && type.BaseType == typeof(object), "original public static type flags");
            Require((type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original field-initializer BeforeFieldInit flag");
            Require(type.GetCustomAttributesData().Count == 0, "original physics type has no attributes");
            var fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(f => f.MetadataToken).ToArray();
            Require(fields.Length == 2 && fields.Select(f => f.Name).SequenceEqual(new[] { "MaxNumRaycastHits", "m_raycastHits" }), "full ordered original own-field schema");
            FieldInfo limit = fields[0], buffer = fields[1];
            Require(limit.IsPrivate && limit.IsStatic && limit.IsLiteral && !limit.IsInitOnly && limit.FieldType == typeof(int) && Equals(limit.GetRawConstantValue(), 16), "original private constant flags/type/value");
            Require(buffer.IsPrivate && buffer.IsStatic && !buffer.IsLiteral && !buffer.IsInitOnly && buffer.FieldType == typeof(RaycastHit[]), "original mutable shared buffer flags/type");
            Require(fields.All(f => f.GetCustomAttributesData().Count == 0), "original own-field attributes");
            ConstructorInfo initializer = type.TypeInitializer;
            Require(initializer != null && initializer.IsPrivate && initializer.IsStatic && initializer.IsSpecialName && (initializer.Attributes & MethodAttributes.RTSpecialName) != 0, "original generated static initializer flags");
            Require(initializer.GetParameters().Length == 0 && initializer.GetCustomAttributesData().Count == 0, "original static initializer parameter/attribute identities");
            Require(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length == 0, "original static type has no instance constructor");
            var init = ReadIL(initializer);
            Require(init.Select(x => x.opcode).SequenceEqual(new[] { OpCodes.Ldc_I4_S, OpCodes.Newarr, OpCodes.Stsfld, OpCodes.Ret }), "genuine static initializer allocation/store only");
            Require(Equals(init[0].operand, (sbyte)16) && Equals(init[1].operand, typeof(RaycastHit)) && Equals(init[2].operand, buffer), "original hit-array allocation length/type/destination");
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            var hits = (RaycastHit[])buffer.GetValue(null);
            Require(hits != null && hits.Length == 16, "actual genuine initialized buffer length");
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            Require(ReferenceEquals(hits, buffer.GetValue(null)), "static initializer preserves one shared array instance");

            var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).OrderBy(m => m.MetadataToken).ToArray();
            Require(methods.Length == 2 && methods.Select(m => m.Name).SequenceEqual(new[] { "ProjectScreenPointToGameObject", "CapsuleCastNonAlloc" }), "two original APIs plus separately counted initializer");
            foreach (var m in methods)
            {
                Require(m.IsPublic && m.IsStatic && m.IsHideBySig && !m.IsGenericMethod && m.GetMethodBody() != null, m.Name + " original concrete API flags");
                Require(m.GetCustomAttributesData().Count == 0 && m.GetParameters().All(p => !p.IsOut && !p.ParameterType.IsByRef), m.Name + " original attributes/value-parameter identities");
            }
            MethodInfo pick = methods[0], cast = methods[1];
            Require(pick.ReturnType == typeof(GameObject) && pick.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Camera), typeof(Vector2), typeof(string) }), "exact original picking signature");
            Require(pick.GetParameters().Select(p => p.Name).SequenceEqual(new[] { "camera", "position", "tag" }) && pick.GetParameters()[2].IsOptional && pick.GetParameters()[2].HasDefaultValue && pick.GetParameters()[2].DefaultValue == null, "exact original picking parameter names/null default");
            Require(pick.GetParameters().Take(2).All(p => !p.IsOptional && !p.HasDefaultValue), "only original tag parameter is optional");
            Require(cast.ReturnType == typeof(int) && cast.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(CapsuleCollider), typeof(Vector3), typeof(Vector3), typeof(int), typeof(RaycastHit[]) }), "exact original capsule signature");
            Require(cast.GetParameters().Select(p => p.Name).SequenceEqual(new[] { "collider", "position", "offset", "layerMask", "results" }) && cast.GetParameters().All(p => !p.IsOptional && !p.HasDefaultValue), "exact original capsule names/default contracts");

            var pickingIL = ReadIL(pick);
            var pickingCalls = pickingIL.Where(x => x.operand is MethodBase).Select(x => (MethodBase)x.operand).ToArray();
            var pickingNames = pickingCalls.Select(CallName).ToArray();
            string[] expectedPicking = { "UnityEngine.Vector2.op_Implicit", "UnityEngine.Camera.ScreenPointToRay", "UnityEngine.Ray.get_origin", "UnityEngine.Ray.get_direction", "UnityEngine.Physics.RaycastNonAlloc", "UnityEngine.RaycastHit.get_collider", "UnityEngine.Object.op_Equality", "UnityEngine.Component.get_gameObject", "UnityEngine.GameObject.CompareTag", "UnityEngine.Component.get_transform", "UnityEngine.Transform.get_position", "UnityEngine.Component.get_transform", "UnityEngine.Transform.get_position", "UnityEngine.Vector3.op_Subtraction", "UnityEngine.Vector3.get_sqrMagnitude", "UnityEngine.Component.get_gameObject" };
            Require(pickingNames.SequenceEqual(expectedPicking), "genuine picking call order, camera position read for each eligible hit");
            foreach (string name in expectedPicking.Distinct())
                Require(pickingNames.Contains(name), "genuine picking dependency " + name);
            MethodBase raycast = pickingCalls.Single(m => m.DeclaringType == typeof(Physics));
            Require(raycast.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit[]) }), "original narrow origin/direction raycast overload retains engine defaults");
            Require(!pickingCalls.Any(m => m.Name == "get_distance" || m.DeclaringType == typeof(Array) || m.Name == "IsNullOrEmpty"), "no hit-distance choice, buffer clearing/sorting or empty-tag bypass");
            Require(pickingIL.Any(x => x.opcode == OpCodes.Ldc_R4 && Equals(x.operand, float.MaxValue)), "original nearest-distance sentinel");
            Require(pickingIL.Count(x => x.opcode == OpCodes.Ldsfld && Equals(x.operand, buffer)) == 2, "original buffer re-read in loop, no local snapshot replacement");
            int squaredDistance = pickingIL.FindIndex(x => x.operand is MethodBase m && m.DeclaringType == typeof(Vector3) && m.Name == "get_sqrMagnitude");
            Require(squaredDistance >= 0 && (pickingIL[squaredDistance + 4].opcode == OpCodes.Bge || pickingIL[squaredDistance + 4].opcode == OpCodes.Bge_S), "actual squared-distance rejection comparison preserves equal/unordered behavior");

            var capsuleIL = ReadIL(cast);
            var capsuleCalls = capsuleIL.Where(x => x.operand is MethodBase).Select(x => (MethodBase)x.operand).ToArray();
            var capsuleNames = capsuleCalls.Select(CallName).ToArray();
            string[] getters = { "UnityEngine.Component.get_transform", "UnityEngine.Transform.get_up", "UnityEngine.Transform.get_lossyScale", "UnityEngine.CapsuleCollider.get_radius", "UnityEngine.CapsuleCollider.get_height", "UnityEngine.Vector3.get_magnitude", "UnityEngine.Vector3.get_normalized", "UnityEngine.Mathf.Max" };
            Require(capsuleNames.Take(getters.Length).SequenceEqual(getters), "original cached transform/property/offset evaluation order");
            foreach (string name in getters.Distinct())
                Require(capsuleNames.Contains(name), "genuine capsule dependency " + name);
            Require(!capsuleCalls.Any(m => m.Name == "get_center" || m.Name == "get_direction" || m.Name == "get_position"), "original ignores collider center/direction/transform position");
            Require(capsuleIL.Any(x => x.opcode == OpCodes.Ldc_R4 && Equals(x.operand, float.Epsilon)), "original minimum cast distance is Single epsilon");
            Require(capsuleNames.Last() == "UnityEngine.Physics.CapsuleCastNonAlloc", "original engine capsule call remains final");
            MethodBase sweep = capsuleCalls.Last();
            Require(sweep.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(Vector3), typeof(RaycastHit[]), typeof(float), typeof(int) }), "original narrow capsule overload retains engine trigger defaults");
            Require(!capsuleCalls.Any(m => m.DeclaringType == typeof(PhysicsUtilities)), "no substitute runtime helper or recursive dependency");
            Require(ReferenceEquals(hits, buffer.GetValue(null)), "managed schema/IL checks preserve shared hit-buffer identity");
            return checks;
        }

        private static GameObject Create(List<GameObject> objects, string name, Vector3 position)
        {
            var item = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            objects.Add(item);
            item.transform.position = position;
            return item;
        }

        // These checks create genuine temporary engine geometry only. No original
        // assets or controller/manager substitutes participate in this bounded test.
        public static int Run()
        {
            RunManaged();
            var objects = new List<GameObject>();
            bool previousTriggers = Physics.queriesHitTriggers;
            FieldInfo bufferField = typeof(PhysicsUtilities).GetField("m_raycastHits", BindingFlags.Static | BindingFlags.NonPublic);
            var buffer = (RaycastHit[])bufferField.GetValue(null);
            var saved = (RaycastHit[])buffer.Clone();
            try
            {
                Physics.queriesHitTriggers = true;
                Vector3 anchor = new Vector3(16384, 8192, -16384);
                GameObject cameraObject = Create(objects, "Lucid physics proof camera", anchor);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 4;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 200;
                camera.transform.rotation = Quaternion.identity;
                Vector3 screen = camera.WorldToScreenPoint(anchor + Vector3.forward * 10);
                Vector2 point = new Vector2(screen.x, screen.y);

                GameObject close = Create(objects, "Lucid closer collider centre", anchor + Vector3.forward * 10);
                BoxCollider closeCollider = close.AddComponent<BoxCollider>(); closeCollider.size = new Vector3(2, 2, 1);
                GameObject far = Create(objects, "Lucid closer ray-hit surface", anchor + Vector3.forward * 15);
                far.tag = "Player";
                BoxCollider farCollider = far.AddComponent<BoxCollider>(); farCollider.size = new Vector3(2, 2, 20);
                Physics.SyncTransforms();
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point) == close, "picking favours closer centre despite farther ray-hit surface");
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point, "Player") == far, "tag filtering selects farther matching centre");
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point, "Respawn") == null, "no matching known tag returns null");
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point, "") == null, "empty tag reaches genuine engine validation and rejects both hits");
                Throws<NullReferenceException>(() => PhysicsUtilities.ProjectScreenPointToGameObject(null, point), "null camera retains original boundary");
                Require(ReferenceEquals(buffer, bufferField.GetValue(null)), "picking preserves shared initialized buffer");

                closeCollider.enabled = false; farCollider.enabled = false;
                Physics.SyncTransforms();
                RaycastHit[] beforeNoHit = (RaycastHit[])buffer.Clone();
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point) == null, "zero returned hits do not consume stale buffer entries");
                Require(buffer.Select(h => h.distance).SequenceEqual(beforeNoHit.Select(h => h.distance)), "no-hit query does not clear shared distances");

                close.transform.position = anchor + Vector3.forward * 10;
                far.transform.position = close.transform.position;
                closeCollider.size = new Vector3(2, 2, 1); farCollider.size = new Vector3(2, 2, 2);
                closeCollider.enabled = true; farCollider.enabled = true;
                Physics.SyncTransforms();
                GameObject tied = PhysicsUtilities.ProjectScreenPointToGameObject(camera, point);
                Require(tied == buffer[0].collider.gameObject, "equal centre distances retain first native-buffer hit");
                closeCollider.enabled = false; farCollider.enabled = false;

                GameObject ignored = Create(objects, "Lucid ignored ray layer", anchor + Vector3.forward * 5);
                ignored.layer = 2;
                BoxCollider ignoredCollider = ignored.AddComponent<BoxCollider>(); ignoredCollider.size = Vector3.one;
                Physics.SyncTransforms();
                Require(PhysicsUtilities.ProjectScreenPointToGameObject(camera, point) == null, "original raycast overload excludes Ignore Raycast layer");
                ignoredCollider.enabled = false;

                GameObject source = Create(objects, "Lucid capsule shape properties", anchor + new Vector3(100, 100, 100));
                source.transform.localScale = new Vector3(2, 3, 4);
                CapsuleCollider capsule = source.AddComponent<CapsuleCollider>();
                capsule.enabled = false; capsule.radius = 0.5f; capsule.height = 2;
                capsule.center = new Vector3(50, 60, 70); capsule.direction = 0;
                GameObject obstacle = Create(objects, "Lucid capsule sweep target", anchor + new Vector3(0, 4.5f, -5));
                obstacle.layer = 30;
                SphereCollider obstacleCollider = obstacle.AddComponent<SphereCollider>(); obstacleCollider.radius = 0.25f;
                var results = new RaycastHit[4];
                Vector3 displacement = new Vector3(0, 0, 10);
                int mask = 1 << 30;
                Physics.SyncTransforms();
                int count = PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results);
                Require(count == 1 && results[0].collider == obstacleCollider, "original full half-height/radial max scale hits high offset target");
                Require(results[0].distance > 0 && results[0].distance < 10, "sweep starts behind supplied position and retains returned distance");
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, 0, results) == 0, "explicit empty layer mask excludes hit");
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, new RaycastHit[0]) == 0, "empty caller result array retains engine capacity boundary");
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, Vector3.zero, mask, results) == 0, "zero normalized direction reaches engine zero-hit boundary");
                source.transform.localScale = new Vector3(-2, -3, -4);
                Physics.SyncTransforms();
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 1, "negative lossy scales use original absolute magnitudes");
                source.transform.rotation = Quaternion.Euler(0, 0, 90);
                Physics.SyncTransforms();
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 0, "rotating transform.up removes former vertical target");
                obstacle.transform.position = anchor + new Vector3(4.5f, 0, -5);
                Physics.SyncTransforms();
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 1, "rotated up extends along X regardless of collider direction");
                obstacle.transform.position = anchor + new Vector3(0, 0, 5);
                Physics.SyncTransforms();
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 0, "offset magnitude bounds cast rather than infinite ray");
                obstacle.transform.position = anchor + new Vector3(0, 0, -5);
                obstacleCollider.isTrigger = true;
                Physics.queriesHitTriggers = false;
                Physics.SyncTransforms();
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 0, "original overload follows global trigger-ignore policy");
                Physics.queriesHitTriggers = true;
                Require(PhysicsUtilities.CapsuleCastNonAlloc(capsule, anchor, displacement, mask, results) == 1, "original overload follows global trigger-include policy");
                Throws<NullReferenceException>(() => PhysicsUtilities.CapsuleCastNonAlloc(null, anchor, displacement, mask, results), "null source collider retains original boundary");
                Require(ReferenceEquals(buffer, bufferField.GetValue(null)), "capsule queries use caller results rather than shared picking buffer");
                Debug.Log("PASS original physics foundation checks=" + checks);
                return checks;
            }
            finally
            {
                Physics.queriesHitTriggers = previousTriggers;
                Array.Copy(saved, buffer, saved.Length);
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                Physics.SyncTransforms();
            }
        }
    }
}
