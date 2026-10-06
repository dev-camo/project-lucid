using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid
{
    // Diagnostic-only delegates bind actual private original methods; no runtime
    // contracts, fake game types or simulated engine methods are introduced.
    public static class ColliderExtensionsVerification
    {
        private delegate bool Collate(int count, RaycastHit[] hits, ref Vector3 position, ref Vector3 normal, List<GameObject> include);
        private delegate bool Closest(Vector3 origin, int count, RaycastHit[] hits, ref Vector3 position, ref Vector3 normal, List<GameObject> include);
        private delegate void WorldSpace(CapsuleCollider capsule, out Vector3 top, out Vector3 bottom, out float radius);
        private static void Require(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(label);
            ++checks;
        }
        private static void Same(Vector3 actual, Vector3 expected, string label, ref int checks)
        {
            Require(actual.x.Equals(expected.x) && actual.y.Equals(expected.y) && actual.z.Equals(expected.z), label, ref checks);
        }
        private static RaycastHit Hit(Vector3 point, Vector3 normal)
        {
            return new RaycastHit { point = point, normal = normal };
        }
        private static T Bind<T>(string name) where T : Delegate
        {
            MethodInfo method = typeof(ColliderExtensions).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            return (T)Delegate.CreateDelegate(typeof(T), method);
        }
        public static int RunManaged()
        {
            int checks = 0;
            Type type = typeof(ColliderExtensions);
            Require(type.IsPublic && type.IsAbstract && type.IsSealed, "original complete static class", ref checks);
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(fields.Length == 1 && fields[0].Name == "MaximumLayers" && fields[0].IsPrivate && fields[0].IsLiteral && (int)fields[0].GetRawConstantValue() == 32, "original sole private32 constant", ref checks);
            Require(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 10, "full original ten methods", ref checks);
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == "PhysicsCastFromCollider")
                {
                    Require(parameters[parameters.Length - 1].HasDefaultValue && parameters[parameters.Length - 1].DefaultValue.Equals(QueryTriggerInteraction.UseGlobal), "exact original query optional0", ref checks);
                    if (parameters.Length == 6) Require(parameters[4].HasDefaultValue && parameters[4].DefaultValue.Equals(false), "exact original debug optionalfalse", ref checks);
                }
                if (method.Name == "ProjectRaycastLocation" || method.Name == "ProjectRaycastToLocationClosest")
                    Require(parameters[6].HasDefaultValue && parameters[6].DefaultValue == null, "exact original list optionalnull", ref checks);
            }
            Collate collate = Bind<Collate>("CollateHits");
            Closest closest = Bind<Closest>("ClosestHit");
            Vector3 sentinelPosition = new Vector3(11f, 12f, 13f);
            Vector3 sentinelNormal = new Vector3(21f, 22f, 23f);
            Vector3 position = sentinelPosition;
            Vector3 normal = sentinelNormal;
            Require(!collate(0, null, ref position, ref normal, null), "zero collate result", ref checks);
            Same(position, sentinelPosition, "zero collate retains position", ref checks);
            Same(normal, sentinelNormal, "zero collate retains normal", ref checks);
            Require(!closest(Vector3.zero, 0, null, ref position, ref normal, null), "zero closest result", ref checks);
            Same(position, sentinelPosition, "zero closest retains position", ref checks);
            Same(normal, sentinelNormal, "zero closest retains normal", ref checks);
            Require(!collate(-1, null, ref position, ref normal, null), "negative collate result", ref checks);
            Same(position, Vector3.zero, "negative collate clears position", ref checks);
            Same(normal, Vector3.zero, "negative collate clears normal", ref checks);
            position = sentinelPosition; normal = sentinelNormal;
            Require(!closest(Vector3.zero, -1, null, ref position, ref normal, null), "negative closest result", ref checks);
            Same(position, sentinelPosition, "negative closest retains position", ref checks);
            Same(normal, sentinelNormal, "negative closest retains normal", ref checks);
            Require(!collate(-2, null, ref position, ref normal, new List<GameObject>()), "negative included collate result", ref checks);
            Same(position, Vector3.zero, "negative included collate clears position", ref checks);
            Same(normal, Vector3.zero, "negative included collate clears normal", ref checks);
            var hits = new[] { Hit(new Vector3(2f, 4f, 6f), Vector3.right), Hit(new Vector3(4f, 8f, 10f), Vector3.up) };
            Require(collate(2, hits, ref position, ref normal, null), "two original hits collate", ref checks);
            Same(position, new Vector3(3f, 6f, 8f), "mean position", ref checks);
            Same(normal, new Vector3(0.5f, 0.5f, 0f), "mean remains unnormalized", ref checks);
            Require(collate(1, hits, ref position, ref normal, null), "count bounds prefix", ref checks);
            Same(position, hits[0].point, "prefix mean position", ref checks);
            Same(normal, hits[0].normal, "prefix mean normal", ref checks);
            bool fault = false;
            try { collate(1, null, ref position, ref normal, null); } catch (NullReferenceException) { fault = true; }
            Require(fault, "null array collate fault", ref checks);
            Same(position, Vector3.zero, "null array position cleared before fault", ref checks);
            Same(normal, Vector3.zero, "null array normal cleared before fault", ref checks);
            fault = false;
            try { collate(3, hits, ref position, ref normal, null); } catch (IndexOutOfRangeException) { fault = true; }
            Require(fault, "range collate fault", ref checks);
            Same(position, new Vector3(6f, 12f, 16f), "partial position retained on range fault", ref checks);
            Same(normal, new Vector3(1f, 1f, 0f), "partial normal retained on range fault", ref checks);
            var aliasHits = new[] { Hit(new Vector3(2f, 0f, 0f), new Vector3(0f, 2f, 0f)), Hit(new Vector3(4f, 0f, 0f), new Vector3(0f, 4f, 0f)) };
            Vector3 alias = sentinelPosition;
            Require(collate(2, aliasHits, ref alias, ref alias, null), "aliased collate result", ref checks);
            Same(alias, new Vector3(1.5f, 1.5f, 0f), "aliased refs average separately", ref checks);
            Require(closest(Vector3.zero, 2, hits, ref position, ref normal, null), "nearest original hit", ref checks);
            Same(position, hits[0].point, "nearest position", ref checks);
            Same(normal, Vector3.right, "nearest normal", ref checks);
            var ties = new[] { Hit(Vector3.right, Vector3.up), Hit(Vector3.left, Vector3.forward) };
            Require(closest(Vector3.zero, 2, ties, ref position, ref normal, null), "equal-distance result", ref checks);
            Same(position, Vector3.left, "equal-distance selects last position", ref checks);
            Same(normal, Vector3.forward, "equal-distance selects last normal", ref checks);
            var threshold = new[] { Hit(Vector3.zero, new Vector3(0.00005f, -0.0001f, 0.0001f)) };
            Require(closest(Vector3.zero, 1, threshold, ref position, ref normal, null), "normal threshold result", ref checks);
            Same(normal, new Vector3(0f, -0.0001f, 0.0001f), "strict original ClampToZero threshold", ref checks);
            alias = sentinelPosition;
            Require(closest(Vector3.zero, 1, threshold, ref alias, ref alias, null), "aliased closest result", ref checks);
            Same(alias, normal, "closest normal overwrites aliased position", ref checks);
            var nan = new[] { Hit(new Vector3(float.NaN, 7f, 8f), Vector3.forward) };
            Require(!closest(Vector3.zero, 1, nan, ref position, ref normal, null), "NaN closest final ordered result", ref checks);
            Same(position, nan[0].point, "NaN point still publishes", ref checks);
            Same(normal, Vector3.forward, "NaN point normal still publishes", ref checks);
            var afterNan = new[] { nan[0], threshold[0] };
            Require(closest(Vector3.zero, 2, afterNan, ref position, ref normal, null), "finite hit replaces NaN", ref checks);
            Same(position, Vector3.zero, "finite after NaN publishes position", ref checks);
            var lastNan = new[] { threshold[0], nan[0] };
            Require(!closest(Vector3.zero, 2, lastNan, ref position, ref normal, null), "later NaN changes final result", ref checks);
            Same(position, nan[0].point, "later NaN remains final point", ref checks);
            position = sentinelPosition; normal = sentinelNormal;
            var infinity = new[] { Hit(new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.up) };
            Require(!closest(Vector3.zero, 1, infinity, ref position, ref normal, null), "infinite squared-distance skipped", ref checks);
            Same(position, sentinelPosition, "infinity retains position", ref checks);
            Same(normal, sentinelNormal, "infinity retains normal", ref checks);
            fault = false;
            try { closest(Vector3.zero, 1, null, ref position, ref normal, null); } catch (NullReferenceException) { fault = true; }
            Require(fault, "closest null array fault", ref checks);
            Same(position, sentinelPosition, "closest null fault retains position", ref checks);
            Same(normal, sentinelNormal, "closest null fault retains normal", ref checks);
            fault = false;
            try { closest(Vector3.zero, 3, ties, ref position, ref normal, null); } catch (IndexOutOfRangeException) { fault = true; }
            Require(fault, "closest range fault", ref checks);
            Same(position, Vector3.left, "closest partial last tie survives fault", ref checks);
            Same(normal, Vector3.forward, "closest partial normal survives fault", ref checks);
            Require(((Collider)null).PhysicsCastFromCollider(Vector3.zero, 0, null) == 0, "unsupported null base dispatch", ref checks);
            return checks;
        }
        private static void Near(Vector3 actual, Vector3 expected, string label, ref int checks)
        {
            Require(Mathf.Abs(actual.x - expected.x) < 0.001f && Mathf.Abs(actual.y - expected.y) < 0.001f && Mathf.Abs(actual.z - expected.z) < 0.001f, label, ref checks);
        }
        // Actual Unity-only owned-collider fixtures. No engine method is simulated.
        // Transform/raycast tolerances concern real engine arithmetic; native-bit
        // equivalence, authored collision tracking and original gameplay are unproved.
        public static int RunEngine()
        {
            int checks = 0;
            GameObject floorObject = null, sphereObject = null, capsuleObject = null, boxObject = null;
            bool oldQueriesHitTriggers = Physics.queriesHitTriggers;
            bool[] oldIgnored = new bool[32];
            bool capturedLayers = false;
            try
            {
                floorObject = new GameObject("ProjectLucidColliderFloor");
                floorObject.layer = 29;
                var floor = floorObject.AddComponent<BoxCollider>();
                Vector3 origin = new Vector3(4000f, 5000f, 6000f);
                floorObject.transform.position = origin + Vector3.down * 3f;
                floor.size = new Vector3(16f, 0.2f, 16f);
                sphereObject = new GameObject("ProjectLucidColliderSphere");
                sphereObject.layer = 28;
                var sphere = sphereObject.AddComponent<SphereCollider>();
                sphereObject.transform.position = origin;
                sphereObject.transform.localScale = Vector3.one * 3f;
                sphere.radius = 0.5f;
                capsuleObject = new GameObject("ProjectLucidColliderCapsule");
                capsuleObject.layer = 28;
                var capsule = capsuleObject.AddComponent<CapsuleCollider>();
                for (int layer = 0; layer < 32; ++layer)
                    oldIgnored[layer] = Physics.GetIgnoreLayerCollision(28, layer);
                capturedLayers = true;
                Physics.IgnoreLayerCollision(28, 4, true);
                Physics.IgnoreLayerCollision(28, 31, false);
                int mask = sphere.GetLayerMask();
                int expectedMask = 0;
                for (int layer = 0; layer < 32; ++layer)
                    if (layer == 31 || (layer != 4 && !oldIgnored[layer])) expectedMask |= 1 << layer;
                Require(mask == expectedMask, "real full32-layer mask", ref checks);
                Require((mask & (1 << 4)) == 0, "ignored real layer excluded", ref checks);
                Require((mask & (1 << 31)) != 0, "signed layer31 retained", ref checks);
                WorldSpace worldSpace = Bind<WorldSpace>("ToWorldSpace");
                capsuleObject.transform.position = origin + Vector3.right * 30f;
                capsuleObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                capsuleObject.transform.localScale = new Vector3(-2f, 3f, 4f);
                capsule.center = new Vector3(1f, 2f, 3f);
                capsule.radius = 0.5f;
                capsule.height = 6f;
                Vector3 centre = capsuleObject.transform.TransformPoint(capsule.center);
                Vector3[] extents = { Vector3.up * 4f, Vector3.left * 7f, Vector3.forward * 10.5f };
                float[] radii = { 2f, 2f, 1.5f };
                for (int axis = 0; axis < 3; ++axis)
                {
                    capsule.direction = axis;
                    worldSpace(capsule, out Vector3 top, out Vector3 bottom, out float radius);
                    Require(Mathf.Abs(radius - radii[axis]) < 0.0001f, "real perpendicular absolute radius axis" + axis, ref checks);
                    Near(top, centre + extents[axis], "real top endpoint axis" + axis, ref checks);
                    Near(bottom, centre - extents[axis], "real bottom endpoint axis" + axis, ref checks);
                }
                capsule.direction = 1;
                capsule.height = 1f;
                worldSpace(capsule, out Vector3 collapsedTop, out Vector3 collapsedBottom, out float collapsedRadius);
                Require(Mathf.Abs(collapsedRadius - 2f) < 0.0001f, "real short capsule radius", ref checks);
                Near(collapsedTop, centre, "real short capsule top collapses", ref checks);
                Near(collapsedBottom, centre, "real short capsule bottom collapses", ref checks);
                boxObject = new GameObject("ProjectLucidContainsBox");
                boxObject.layer = 28;
                var box = boxObject.AddComponent<BoxCollider>();
                boxObject.transform.position = new Vector3(10f, 20f, 30f);
                boxObject.transform.localScale = new Vector3(-2f, 3f, 4f);
                box.center = new Vector3(1f, 2f, 3f);
                box.size = new Vector3(4f, 6f, 8f);
                Require(box.Contains(boxObject.transform.TransformPoint(box.center)), "real nonzero center containment", ref checks);
                Require(box.Contains(boxObject.transform.TransformPoint(box.center + box.size * 0.5f)), "real inclusive box boundary", ref checks);
                Require(!box.Contains(boxObject.transform.TransformPoint(box.center + Vector3.right * 2.01f)), "real outside authored box", ref checks);
                capsuleObject.transform.position = origin + Vector3.right * 3f;
                capsuleObject.transform.rotation = Quaternion.identity;
                capsuleObject.transform.localScale = Vector3.one;
                capsule.center = Vector3.zero;
                capsule.height = 2f;
                capsule.radius = 0.5f;
                Physics.SyncTransforms();
                LayerMask floorMask = 1 << 29;
                RaycastHit[] hits = new RaycastHit[16];
                Vector3 position = new Vector3(11f, 12f, 13f), normal = new Vector3(21f, 22f, 23f);
                Vector3 floorTop = origin + Vector3.down * 2.9f;
                Require(ColliderExtensions.ProjectRaycastLocation(origin, Vector3.down * 6f, floorMask, hits, ref position, ref normal), "real projection collates original query", ref checks);
                Near(position, floorTop, "real projected point", ref checks);
                Near(normal, Vector3.up, "real projected normal", ref checks);
                Require(ColliderExtensions.ProjectRaycastToLocationClosest(origin, Vector3.down * 6f, floorMask, hits, ref position, ref normal, new List<GameObject> { floorObject }), "real included closest hit", ref checks);
                Near(position, floorTop, "real included closest point", ref checks);
                Near(normal, Vector3.up, "real included closest normal", ref checks);
                Require(ColliderExtensions.ProjectRaycastLocation(origin, Vector3.down * 6f, floorMask, hits, ref position, ref normal, new List<GameObject> { floorObject }), "real included average hit", ref checks);
                Near(position, floorTop, "real included average point", ref checks);
                Near(normal, Vector3.up, "real included average normal", ref checks);
                Require(!ColliderExtensions.ProjectRaycastLocation(origin, Vector3.down * 6f, floorMask, hits, ref position, ref normal, new List<GameObject> { sphereObject }), "real excluded average result", ref checks);
                Same(position, Vector3.zero, "real excluded average clears point", ref checks);
                Same(normal, Vector3.zero, "real excluded average clears normal", ref checks);
                Vector3 sentinelPosition = new Vector3(11f, 12f, 13f), sentinelNormal = new Vector3(21f, 22f, 23f);
                position = sentinelPosition; normal = sentinelNormal;
                Require(!ColliderExtensions.ProjectRaycastToLocationClosest(origin, Vector3.down * 6f, floorMask, hits, ref position, ref normal, new List<GameObject> { sphereObject }), "real excluded closest result", ref checks);
                Same(position, sentinelPosition, "real excluded closest retains point", ref checks);
                Same(normal, sentinelNormal, "real excluded closest retains normal", ref checks);
                Require(!ColliderExtensions.ProjectRaycastLocation(origin, Vector3.down * 6f, 0, hits, ref position, ref normal), "real empty-mask average", ref checks);
                Same(position, sentinelPosition, "real zero query retains point", ref checks);
                Same(normal, sentinelNormal, "real zero query retains normal", ref checks);
                Require(!ColliderExtensions.ProjectRaycastToLocationClosest(origin, Vector3.down * 6f, 0, hits, ref position, ref normal), "real empty-mask closest", ref checks);
                Same(position, sentinelPosition, "real zero closest query retains point", ref checks);
                Same(normal, sentinelNormal, "real zero closest query retains normal", ref checks);
                int count = sphere.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, QueryTriggerInteraction.Ignore);
                Require(count > 0, "real sphere cast finds floor", ref checks);
                Require(hits[0].collider == floor, "real sphere hit identity", ref checks);
                Require(Mathf.Abs(hits[0].distance - 2.4f) < 0.001f, "authored sphere radius remains unscaled", ref checks);
                Near(hits[0].normal, Vector3.up, "real sphere floor normal", ref checks);
                count = capsule.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, false, QueryTriggerInteraction.Ignore);
                Require(count > 0, "real capsule cast finds floor", ref checks);
                Require(hits[0].collider == floor, "real capsule hit identity", ref checks);
                Require(Mathf.Abs(hits[0].distance - 1.9f) < 0.001f, "real capsule sweep endpoints", ref checks);
                Require(capsule.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, true, QueryTriggerInteraction.Ignore) > 0, "real post-cast debug rays complete", ref checks);
                Require(((Collider)sphere).PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, QueryTriggerInteraction.Ignore) > 0, "real base dispatch reaches sphere query", ref checks);
                Require(((Collider)capsule).PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, QueryTriggerInteraction.Ignore) > 0, "real base dispatch reaches capsule query", ref checks);
                Require(((Collider)box).PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits) == 0, "unsupported actual box stays unsupported", ref checks);
                floor.isTrigger = true;
                Physics.SyncTransforms();
                Require(sphere.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, QueryTriggerInteraction.Ignore) == 0, "sphere explicit ignore triggers", ref checks);
                Require(sphere.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, QueryTriggerInteraction.Collide) > 0, "sphere explicit collide triggers", ref checks);
                Physics.queriesHitTriggers = false;
                Require(sphere.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits) == 0, "original UseGlobal false", ref checks);
                Physics.queriesHitTriggers = true;
                Require(sphere.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits) > 0, "original UseGlobal true", ref checks);
                Require(capsule.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, false, QueryTriggerInteraction.Ignore) == 0, "capsule explicit ignore triggers", ref checks);
                Require(capsule.PhysicsCastFromCollider(Vector3.down * 6f, floorMask, hits, false, QueryTriggerInteraction.Collide) > 0, "capsule explicit collide triggers", ref checks);
                floor.isTrigger = false;
                Physics.SyncTransforms();
                Require(sphere.PhysicsCastFromCollider(Vector3.zero, floorMask, hits) == 0, "real zero-offset sphere completes", ref checks);
                Require(capsule.PhysicsCastFromCollider(Vector3.zero, floorMask, hits) == 0, "real zero-offset capsule completes", ref checks);
                UObject.DestroyImmediate(sphereObject);
                Require(sphere.PhysicsCastFromCollider(Vector3.down, floorMask, hits) == 0, "destroyed typed sphere respects Unity null", ref checks);
                Require(((Collider)sphere).PhysicsCastFromCollider(Vector3.down, floorMask, hits) == 0, "destroyed sphere base dispatch respects typed null", ref checks);
                UObject.DestroyImmediate(capsuleObject);
                Require(capsule.PhysicsCastFromCollider(Vector3.down, floorMask, hits) == 0, "destroyed typed capsule respects Unity null", ref checks);
            }
            finally
            {
                try
                {
                    try { Physics.queriesHitTriggers = oldQueriesHitTriggers; }
                    finally
                    {
                        if (capturedLayers)
                            for (int layer = 0; layer < 32; ++layer) Physics.IgnoreLayerCollision(28, layer, oldIgnored[layer]);
                    }
                }
                finally
                {
                    try { if (boxObject != null) UObject.DestroyImmediate(boxObject); }
                    finally
                    {
                        try { if (capsuleObject != null) UObject.DestroyImmediate(capsuleObject); }
                        finally
                        {
                            try { if (sphereObject != null) UObject.DestroyImmediate(sphereObject); }
                            finally { if (floorObject != null) UObject.DestroyImmediate(floorObject); }
                        }
                    }
                }
            }
            return checks;
        }
    }
}
