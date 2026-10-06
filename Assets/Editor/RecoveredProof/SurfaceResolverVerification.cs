using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Unity.Profiling;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class SurfaceResolverVerification
    {
        private static int checks;
        private static void Check(bool condition, string name)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException(name);
        }
        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;
        private static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        private static bool Clears(SurfaceLocation location) => location.m_surface == null && Same(location.m_localPosition, Vector3.zero)
            && Same(location.m_worldPosition, Vector3.zero) && location.m_worldRotation.x == 0f
            && location.m_worldRotation.y == 0f && location.m_worldRotation.z == 0f && location.m_worldRotation.w == 0f
            && location.m_metadata.Equals(default(SurfaceKnotMetadata)) && !location.m_positionBoundsInfo.AtEndEdge;
        private static bool Cleared(RaycastHit hit) => Same(hit.point, Vector3.zero) && Same(hit.normal, Vector3.zero) && hit.distance == 0f;
        private static Ray RayAt(float z) => new Ray(new Vector3(0.25f, 0.25f, z), Vector3.forward);
        private static RaycastHit Sentinel(float distance) => new RaycastHit { distance = distance, point = new Vector3(7f, 8f, 9f), normal = Vector3.right };
        private static FieldInfo Field(string name) => typeof(CollisionResolver2D).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        private static MethodInfo Method(string name) => typeof(CollisionResolver2D).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static Exception Inner(Action action)
        {
            try { action(); return null; }
            catch (TargetInvocationException exception) { return exception.InnerException; }
            catch (Exception exception) { return exception; }
        }

        public static int RunSurfaceManaged()
        {
            checks = 0;
            Vector3 a = Vector3.zero, b = Vector3.up, c = Vector3.right;
            RaycastHit hit = Sentinel(91f);
            Check(SurfacePhysics.IntersectRayTriangle(RayAt(-2f), a, b, c, false, out hit), "front-facing triangle intersects");
            Check(hit.distance == 2f, "native signed ray parameter");
            Check(Same(hit.point, new Vector3(0.25f, 0.25f, 0f)), "intersection point uses origin plus direction times parameter");
            Check(Same(hit.normal, Vector3.back), "original winding normal");
            Check(Same(hit.barycentricCoordinate, new Vector3(0.25f, 0.5f, 0.25f)), "original setter inputs roundtrip through genuine Unity UV storage");
            Check(!SurfacePhysics.IntersectRayTriangle(RayAt(-2f), a, c, b, true, out hit), "bidirectional keeps rejected opposite winding");
            Check(Cleared(hit), "winding failure clears prior hit");
            Check(!SurfacePhysics.IntersectRayTriangle(RayAt(2f), a, b, c, false, out hit), "unidirectional rejects negative signed parameter");
            Check(Cleared(hit), "behind-origin failure output is default");
            Check(SurfacePhysics.IntersectRayTriangle(RayAt(2f), a, b, c, true, out hit), "bidirectional accepts behind-origin point");
            Check(hit.distance == -2f && Same(hit.point, new Vector3(0.25f, 0.25f, 0f)), "behind-origin signed distance preserved");
            Check(SurfacePhysics.IntersectRayTriangle(RayAt(0f), a, b, c, false, out hit) && hit.distance == 0f, "on-plane zero parameter accepted");
            Check(SurfacePhysics.IntersectRayTriangle(new Ray(Vector3.back, Vector3.forward), a, b, c, false, out hit), "vertex boundary is inclusive");
            Check(Same(hit.barycentricCoordinate, new Vector3(0f, 1f, 0f)), "vertex weights retain genuine setter/getter permutation");
            Check(SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(0.5f, 0.5f, -1f), Vector3.forward), a, b, c, false, out hit), "edge boundary is inclusive");
            Check(Same(hit.barycentricCoordinate, new Vector3(0.5f, 0f, 0.5f)), "edge weights retain genuine Unity barycentric storage");
            Check(!SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(-0.1f, 0.2f, -1f), Vector3.forward), a, b, c, true, out hit), "negative third weight rejected");
            Check(!SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(0.2f, -0.1f, -1f), Vector3.forward), a, b, c, true, out hit), "negative second weight rejected");
            Check(!SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(0.8f, 0.8f, -1f), Vector3.forward), a, b, c, true, out hit), "weight sum beyond determinant rejected");
            Check(!SurfacePhysics.IntersectRayTriangle(RayAt(-2f), a, a, c, true, out hit) && Cleared(hit), "degenerate determinant clears hit");
            Check(!SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(0.25f, 0.25f, -2f), Vector3.right), a, b, c, true, out hit), "parallel determinant rejected");
            Check(SurfacePhysics.IntersectRayTriangle(new Ray(new Vector3(0.00025f, 0.00025f, -2f), Vector3.forward), a, b * 0.001f, c * 0.001f, false, out hit), "no determinant epsilon removes tiny triangle");
            Check(Near(hit.distance, 2f) && Same(hit.normal, Vector3.zero), "tiny winding normal uses genuine Unity normalize epsilon");
            Check(SurfacePhysics.IntersectRayTriangle(RayAt(-2f), new Vector3(float.NaN, 0f, 0f), b, c, false, out hit), "unordered determinant and weights pass original rejection gates");
            Check(float.IsNaN(hit.distance) && float.IsNaN(hit.point.x), "unordered successful intersection publishes NaNs");
            hit = Sentinel(2f);
            Check(!SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), a, b, c, false, 3f, ref hit), "nearest tie rejected");
            Check(hit.distance == 2f && Same(hit.point, new Vector3(7f, 8f, 9f)), "tie retains complete prior ref hit");
            hit = Sentinel(3f);
            Check(!SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), a, b, c, false, 2f, ref hit), "ray-distance tie rejected");
            Check(hit.distance == 3f, "distance-limit failure does not publish candidate");
            Check(SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), a, b, c, false, 2.01f, ref hit) && hit.distance == 2f, "strictly closer within limit publishes");
            hit = Sentinel(float.NaN);
            Check(!SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), a, b, c, false, 3f, ref hit) && float.IsNaN(hit.distance), "NaN prior distance rejects ordered nearest comparison");
            hit = Sentinel(3f);
            Check(!SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), a, b, c, false, float.NaN, ref hit), "NaN ray limit rejects ordered comparison");
            Check(!SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(-2f), new Vector3(float.NaN, 0f, 0f), b, c, false, 3f, ref hit), "NaN successful candidate never replaces current hit");
            Check(SurfacePhysics.TryGetCloserHitFromTriangle(RayAt(2f), a, b, c, true, -1f, ref hit) && hit.distance == -2f, "strict limits retain negative bidirectional parameter");
            ISurface surface = null;
            hit = Sentinel(4f);
            Check(!SurfacePhysics.Raycast(RayAt(-2f), 3f, (ISurface)null, ref hit, ref surface), "null surface fails genuine IRibbon cast");
            Check(hit.distance == 4f && surface == null, "failed cast preserves both ref outputs");
            MeshSurface mesh = (MeshSurface)FormatterServices.GetUninitializedObject(typeof(MeshSurface));
            surface = mesh;
            Check(!SurfacePhysics.Raycast(RayAt(-2f), 3f, mesh, ref hit, ref surface, false), "genuine MeshSurface is not an original ribbon route");
            Check(ReferenceEquals(surface, mesh) && hit.distance == 4f, "non-ribbon preserves supplied ref surface");
            Check(!SurfacePhysics.Raycast(RayAt(-2f), 3f, new ISurface[] { null, mesh, null }, out hit, out surface), "list visits genuine non-ribbon and ignored null rows");
            Check(hit.distance == 4f && Cleared(new RaycastHit { point = hit.point, normal = hit.normal }) && surface == null, "list initializes hit to requested-distance plus one");
            Check(!SurfacePhysics.Raycast(RayAt(-2f), -4f, Array.Empty<ISurface>(), out hit, out surface) && hit.distance == -3f, "empty list retains signed distance plus one");
            SurfaceLocation location = new SurfaceLocation { m_surface = mesh, m_localPosition = Vector3.one, m_worldRotation = Quaternion.identity };
            Check(!SurfacePhysics.Raycast(RayAt(-2f), 3f, mesh, ref hit, out location) && Clears(location), "single non-ribbon clears surface-location output after failure");
            Check(!SurfacePhysics.Raycast(RayAt(-2f), 3f, Array.Empty<ISurface>(), out location) && Clears(location), "empty list location output default");
            hit = Sentinel(8f); surface = mesh;
            Exception fault = Inner(() => SurfacePhysics.Raycast(RayAt(-2f), 3f, (IReadOnlyList<ISurface>)null, out hit, out surface));
            Check(fault is NullReferenceException, "null list preserves original list access fault boundary");
            Check(hit.distance == 4f && Same(hit.point, Vector3.zero) && surface == null, "null-list fault occurs after both output initializations");
            location = new SurfaceLocation { m_surface = mesh, m_localPosition = Vector3.one };
            fault = Inner(() => SurfacePhysics.Raycast(RayAt(-2f), 3f, (IReadOnlyList<ISurface>)null, out location));
            Check(fault is NullReferenceException && Clears(location), "list wrapper clears location before faulting call");
            return checks;
        }

        public static int RunResolverShape()
        {
            checks = 0;
            Type type = typeof(CollisionResolver2D);
            Check(type.GetInterfaces().Length == 1 && type.GetInterfaces()[0] == typeof(ICollisionResolver2D), "genuine complete resolver interface graph");
            Check((type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original BeforeFieldInit retained");
            Check(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 9, "all nine original fields");
            Check(Field("MaxStepSize").IsLiteral && (float)Field("MaxStepSize").GetRawConstantValue() == 0.2f, "fixed native step constant");
            Check(Field("CurrentSurfaceDistanceThreshold").IsLiteral && (float)Field("CurrentSurfaceDistanceThreshold").GetRawConstantValue() == 0.001f, "native squared-distance threshold");
            Check(Field("m_closestPoints").IsInitOnly && Field("m_closestPoints").FieldType == typeof(List<SurfaceLocation>), "readonly genuine location list");
            foreach (string name in new[] { "GetNearestLocationFromWorld", "GetNearestLocationHitFromWorld", "MoveTowardsWorldLocation", "InternalMoveTowardsWorldLocation", "MoveTowardsLocalLocation", "FindClosestPointOnNeighbours" })
            {
                FieldInfo field = Field("m_profilerMarker" + name);
                Check(field != null && field.IsStatic && !field.IsInitOnly && field.FieldType == typeof(ProfilerMarker), "original mutable real marker " + name);
            }
            foreach (string name in new[] { "GetNearestLocationFromWorld", "GetNearestLocationHitFromWorld", "MoveTowardsWorldLocation", "MoveTowardsLocalLocation" })
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                Check(method.IsVirtual && !method.IsFinal, "original overridable resolver " + name);
            }
            Check(Method("InternalMoveTowardsWorldLocation").IsFamily && !Method("InternalMoveTowardsWorldLocation").IsVirtual, "protected nonvirtual original stepping implementation");
            Check(Method("CreateCollision").IsPrivate && Method("FindClosestPointOnNeighbours").IsPrivate, "original private collision/append helper visibility");
            return checks;
        }

        // The managed probe bypasses only the instance constructor. The real BeforeFieldInit
        // profiler cctor may still block a host; no marker is replaced or cleared.
        public static int RunResolverManaged()
        {
            checks = 0;
            CollisionResolver2D resolver = (CollisionResolver2D)FormatterServices.GetUninitializedObject(typeof(CollisionResolver2D));
            SurfaceLocation incoming = new SurfaceLocation { m_localPosition = new Vector3(1f, 2f, 3f), m_worldPosition = new Vector3(4f, 5f, 6f), m_worldRotation = new Quaternion(1f, 2f, 3f, 4f) };
            Collision2DInput input = new Collision2DInput { m_world = new Dictionary<ISurface, ISurface[]>(), m_currentLocation = incoming };
            Collision2DOutput output = resolver.GetNearestLocationFromWorld(input, Vector3.one);
            Check(Same(output.m_newLocation.m_localPosition, incoming.m_localPosition), "empty world retains incoming local location");
            Check(Same(output.m_newLocation.m_worldPosition, incoming.m_worldPosition) && output.m_newLocation.m_worldRotation.w == 4f, "empty world retains full incoming world transform");
            output = resolver.MoveTowardsWorldLocation(input, new Vector3(30f, 40f, 50f));
            Check(Same(output.m_newLocation.m_worldPosition, incoming.m_worldPosition), "null current surface uses genuine nearest-world fallback");
            output = resolver.MoveTowardsLocalLocation(input, new Vector3(30f, float.NaN, 50f));
            Check(Same(output.m_newLocation.m_worldPosition, incoming.m_worldPosition), "null current local fallback retains empty-world location");
            RayHit2DOutput ray = resolver.GetNearestLocationHitFromWorld(new RayHit2DInput { m_surfaces = new List<ISurface>(), m_currentLocation = incoming, m_ray = RayAt(-2f), m_distance = 3f });
            Check(!ray.m_hit && Clears(ray.m_location), "empty hit list uses default output, not incoming location");
            Exception fault = Inner(() => resolver.GetNearestLocationFromWorld(default, Vector3.zero));
            Check(fault is NullReferenceException, "null world faults at Keys without fallback provider");
            fault = Inner(() => resolver.GetNearestLocationHitFromWorld(default));
            Check(fault is NullReferenceException, "null surface list faults after requested-endpoint computation");
            MeshSurface mesh = (MeshSurface)FormatterServices.GetUninitializedObject(typeof(MeshSurface));
            incoming.m_surface = mesh; input.m_currentLocation = incoming;
            output = resolver.MoveTowardsWorldLocation(input, Vector3.zero);
            Check(ReferenceEquals(output.m_newLocation.m_surface, mesh) && Same(output.m_newLocation.m_worldPosition, incoming.m_worldPosition), "zero world offset returns incoming non-null location without projection");
            output = resolver.MoveTowardsWorldLocation(input, new Vector3(float.NaN, 0f, 0f));
            Check(ReferenceEquals(output.m_newLocation.m_surface, mesh), "unordered remaining magnitude skips world step loop");
            Collision2DInput noWorld = input; noWorld.m_world = null;
            output = resolver.MoveTowardsWorldLocation(noWorld, Vector3.zero);
            Check(ReferenceEquals(output.m_newLocation.m_surface, mesh), "zero offset does not eagerly access missing world dictionary");
            fault = Inner(() => resolver.GetNearestLocationFromWorld(new Collision2DInput { m_world = new Dictionary<ISurface, ISurface[]> { [mesh] = Array.Empty<ISurface>() }, m_currentLocation = incoming }, Vector3.one));
            Check(fault is NotImplementedException, "nearest enumeration retains genuine MeshSurface projection fault");
            ray = resolver.GetNearestLocationHitFromWorld(new RayHit2DInput { m_surfaces = new List<ISurface> { null, mesh }, m_currentLocation = incoming, m_ray = RayAt(-2f), m_distance = 3f });
            Check(!ray.m_hit && Clears(ray.m_location), "non-ribbon hit candidates retain original false/default result");
            fault = Inner(() => resolver.MoveTowardsWorldLocation(input, new Vector3(0.1f, 0f, 0f)));
            Check(fault is NotImplementedException, "genuine MeshSurface nearest-world original fault is not substituted");
            fault = Inner(() => resolver.MoveTowardsLocalLocation(input, Vector3.zero));
            Check(fault is NotImplementedException, "genuine MeshSurface nearest-local original fault is not substituted");
            PositionBoundsInfo bounds = new PositionBoundsInfo(float.NaN, -0f, false, new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
            incoming.m_positionBoundsInfo = bounds;
            output = (Collision2DOutput)Method("CreateCollision").Invoke(resolver, new object[] { incoming, true });
            Check(output.m_newLocation.m_positionBoundsInfo.AtEndEdge, "collision helper changes requested edge flag");
            Check(float.IsNaN(output.m_newLocation.m_positionBoundsInfo.LeftBoundSqrDistance) && BitConverter.SingleToInt32Bits(output.m_newLocation.m_positionBoundsInfo.RightBoundSqrDistance) == unchecked((int)0x80000000), "collision preserves raw bound distance quirks");
            Check(Same(output.m_newLocation.m_positionBoundsInfo.LeftPosition, bounds.LeftPosition) && Same(output.m_newLocation.m_positionBoundsInfo.RightPosition, bounds.RightPosition), "collision preserves both lateral bound vectors");
            Check(ReferenceEquals(output.m_newLocation.m_surface, mesh) && Same(output.m_newLocation.m_worldPosition, incoming.m_worldPosition), "collision preserves original surface and world state");
            Method("FindClosestPointOnNeighbours").Invoke(resolver, new object[] { Array.Empty<ISurface>(), null, Vector3.zero, Vector3.zero, Vector3.zero });
            Check(true, "empty neighbours returns before accessing null result list");
            fault = Inner(() => Method("FindClosestPointOnNeighbours").Invoke(resolver, new object[] { null, new List<SurfaceLocation>(), Vector3.zero, Vector3.zero, Vector3.zero }));
            Check(fault is NullReferenceException, "null neighbour array preserves length fault");
            return checks;
        }

        public static int Run()
        {
            int total = RunSurfaceManaged() + RunResolverShape() + RunResolverManaged() + RunEngine();
            Debug.Log("SurfacePhysics/CollisionResolver2D bounded checks: " + total);
            return total;
        }

        public static int RunEngine()
        {
            checks = 0;
            CollisionResolver2D resolver = new CollisionResolver2D();
            List<SurfaceLocation> points = (List<SurfaceLocation>)Field("m_closestPoints").GetValue(resolver);
            Check(points.Count == 0 && points.Capacity == 1, "genuine constructor creates capacity-one scratch list");
            foreach (string name in new[] { "GetNearestLocationFromWorld", "GetNearestLocationHitFromWorld", "MoveTowardsWorldLocation", "InternalMoveTowardsWorldLocation", "MoveTowardsLocalLocation", "FindClosestPointOnNeighbours" })
                Check(((ProfilerMarker)Field("m_profilerMarker" + name).GetValue(null)).Handle != IntPtr.Zero, "genuine native profiler marker created " + name);
            return checks;
        }
    }
}
