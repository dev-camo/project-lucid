using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Bounded native-derived tests. Private setter invocation proves its original
    // publication boundary; it does not exercise an unavailable concrete resolver.
    public static class TrackerSurfaceVerification
    {
        private static int checks;
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("TrackerSurface: " + label);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("Missing original field " + name);
        private static void Vec(Vector3 actual, Vector3 expected, string label)
        {
            Check(actual.x.Equals(expected.x), label + ":x");
            Check(actual.y.Equals(expected.y), label + ":y");
            Check(actual.z.Equals(expected.z), label + ":z");
        }
        private static void Set(Tracker2D tracker, SurfaceLocation location)
        {
            typeof(Tracker2D).GetMethod("SetSurfaceLocation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(tracker, new object[] { location });
        }
        private static void Expect<T>(Action action, string label) where T : Exception
        {
            try { action(); }
            catch (Exception exception)
            {
                Exception observed = exception is TargetInvocationException ? exception.InnerException : exception;
                Check(observed != null && observed.GetType() == typeof(T), label + ":exact exception");
                return;
            }
            Check(false, label + ":must throw");
        }

        public static int RunManaged()
        {
            checks = 0;
            Vector3 left = new Vector3(2f, 3f, 5f), right = new Vector3(7f, 11f, 13f);
            var bounds = new PositionBoundsInfo(4f, 9f, true, left, right);
            Check(bounds.LeftBoundSqrDistance == 4f, "bounds ctor left");
            Check(bounds.RightBoundSqrDistance == 9f, "bounds ctor right");
            Check(bounds.AtEndEdge, "bounds ctor edge flag");
            Vec(bounds.LeftPosition, left, "bounds left position");
            Vec(bounds.RightPosition, right, "bounds right position");
            Check(!bounds.OnLateralBounds(4f), "lateral strictly excludes equality");
            Check(bounds.OnLateralBounds(5f), "lateral takes left");
            Check(!bounds.OnLeftBounds(4f) && bounds.OnLeftBounds(5f), "left strict threshold");
            Check(!bounds.OnRightBounds(9f) && bounds.OnRightBounds(10f), "right strict threshold");
            var nanLeft = new PositionBoundsInfo(float.NaN, -1f, false, left, right);
            Check(nanLeft.OnLateralBounds(0f), "NaN left still evaluates right");
            Check(!nanLeft.OnLeftBounds(0f), "left NaN ordered false");
            Check(!nanLeft.OnLateralBounds(float.NaN), "NaN threshold rejects both");
            Check(!new PositionBoundsInfo(float.NaN, float.NaN, true, left, right).OnLateralBounds(1f), "both NaN false");
            Check(new PositionBoundsInfo(float.NegativeInfinity, float.PositiveInfinity, false, left, right).OnLateralBounds(0f), "infinite left comparison");
            Check(!new PositionBoundsInfo(4f, 9f, false, left, right).OnLateralBounds(4f), "edge flag does not broaden bounds");

            var surfaceMetadata = new MetadataGroups();
            var knotA = new MetadataGroups();
            var knotB = new MetadataGroups();
            var one = new SurfaceKnotMetadata(surfaceMetadata);
            Check(ReferenceEquals(one.Surface, surfaceMetadata), "one-arg stores surface");
            Check(one.KnotA == null && one.KnotB == null && one.T == 0f, "one-arg initializes remaining fields");
            var two = new SurfaceKnotMetadata(surfaceMetadata, knotA);
            Check(ReferenceEquals(two.Surface, surfaceMetadata) && ReferenceEquals(two.KnotA, knotA), "two-arg stores both references");
            Check(two.KnotB == null && two.T == 0f, "two-arg initializes remaining fields");
            var three = new SurfaceKnotMetadata(surfaceMetadata, knotA, knotB, -2.5f);
            Check(ReferenceEquals(three.KnotB, knotB) && three.T == -2.5f, "four-arg no clamp");
            Check(float.IsNaN(new SurfaceKnotMetadata(null, null, null, float.NaN).T), "metadata retains NaN");
            Check(float.IsPositiveInfinity(new SurfaceKnotMetadata(null, null, null, float.PositiveInfinity).T), "metadata retains infinity");
            Check(BitConverter.SingleToInt32Bits(new SurfaceKnotMetadata(null, null, null, -0f).T) == unchecked((int)0x80000000), "metadata retains negative zero");
            Check(typeof(PositionBoundsInfo).GetCustomAttributes(false)[0].GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute", "original readonly bounds marker");
            Check(typeof(SurfaceKnotMetadata).GetCustomAttributes(false)[0].GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute", "original readonly metadata marker");

            var firstSurface = new MeshSurface(null);
            var nextSurface = new MeshSurface(null);
            var nestedSurface = new MeshSurface(null);
            Check(firstSurface.Mesh == null, "mesh ctor preserves null collider");
            Check(firstSurface.GetSurfaceMetadata() == null, "original null surface metadata");
            Expect<NotImplementedException>(() => firstSurface.FindNearestSurfaceLocationFromWorld(left), "original world nearest throw");
            Expect<NotImplementedException>(() => firstSurface.FindNearestSurfaceLocationFromLocal(right), "original local nearest throw");
            Expect<NotImplementedException>(() => { float value = firstSurface.Length; }, "original length throw");
            Expect<NotImplementedException>(() => { float value = firstSurface.LengthInverse; }, "original inverse length throw");
            DateTime before = DateTime.Now, stamp = firstSurface.GetTimestamp(), after = DateTime.Now;
            Check(stamp >= before && stamp <= after && stamp.Kind == DateTimeKind.Local, "original current local timestamp");

            var tracker = new Tracker2D(null);
            Check(Field(typeof(Tracker2D), "m_collisionResolver").GetValue(tracker) == null, "constructor preserves null resolver");
            Check(Field(typeof(Tracker2D), "m_world").GetValue(tracker) == null && tracker.Surfaces == null, "constructor leaves world and surfaces null");
            var temporary = (Dictionary<ISurface, ISurface[]>)Field(typeof(Tracker2D), "m_temporaryAllocationWorld").GetValue(tracker);
            Check(temporary != null && temporary.Count == 0, "constructor publishes real temporary dictionary");
            Check(tracker.Location.m_surface == null, "default location surface");
            Vec(tracker.Location.m_worldPosition, default, "default location position");
            Check(tracker.Location.m_worldRotation.x == 0f && tracker.Location.m_worldRotation.w == 0f, "default rotation is not identity");
            var surfaces = new List<ISurface> { firstSurface };
            var world = new Dictionary<ISurface, ISurface[]> { [firstSurface] = new ISurface[] { nextSurface } };
            tracker.SetWorldInformation(surfaces, world);
            Check(ReferenceEquals(tracker.Surfaces, surfaces), "surfaces list aliases caller");
            Check(ReferenceEquals(Field(typeof(Tracker2D), "m_world").GetValue(tracker), world), "world dictionary aliases caller");
            surfaces.Add(nextSurface);
            Check(tracker.Surfaces.Count == 2, "caller list changes observed");
            Check(tracker.OnTrackableSurfaceChange == null, "callback default null");
            var initial = new SurfaceLocation { m_surface = firstSurface, m_worldPosition = left, m_metadata = three, m_positionBoundsInfo = bounds };
            Set(tracker, initial);
            Check(ReferenceEquals(tracker.Location.m_surface, firstSurface), "first setter publishes identity");
            Vec(tracker.Location.m_worldPosition, left, "first setter whole position");
            int calls = 0;
            tracker.OnTrackableSurfaceChange = (oldLocation, newLocation) => calls++;
            var same = initial; same.m_worldPosition = right;
            Set(tracker, same);
            Check(calls == 0, "same reference suppresses callback");
            Vec(tracker.Location.m_worldPosition, right, "same reference still publishes whole value");
            var next = new SurfaceLocation { m_surface = nextSurface, m_worldPosition = new Vector3(17f, 19f, 23f), m_metadata = one };
            tracker.OnTrackableSurfaceChange = (oldLocation, newLocation) =>
            {
                calls++;
                Check(ReferenceEquals(tracker.Location.m_surface, firstSurface), "callback observes old stored surface");
                Vec(tracker.Location.m_worldPosition, right, "callback observes old stored value");
                Check(ReferenceEquals(oldLocation.m_surface, firstSurface) && ReferenceEquals(newLocation.m_surface, nextSurface), "callback receives old/new identities");
                Check(ReferenceEquals(oldLocation.m_metadata.KnotB, knotB) && newLocation.m_metadata.KnotB == null, "callback receives complete metadata copies");
            };
            Set(tracker, next);
            Check(calls == 1, "changed reference invokes callback once");
            Check(ReferenceEquals(tracker.Location.m_surface, nextSurface), "callback completes before publication");
            Vec(tracker.Location.m_worldPosition, next.m_worldPosition, "published full next value");

            tracker.OnTrackableSurfaceChange = (oldLocation, newLocation) => throw new ApplicationException("native boundary witness");
            Expect<ApplicationException>(() => Set(tracker, initial), "throwing callback propagates");
            Check(ReferenceEquals(tracker.Location.m_surface, nextSurface), "throwing callback retains old surface");
            Vec(tracker.Location.m_worldPosition, next.m_worldPosition, "throwing callback retains old full value");

            bool recursed = false;
            var nested = new SurfaceLocation { m_surface = nestedSurface, m_worldPosition = new Vector3(29f, 31f, 37f) };
            tracker.OnTrackableSurfaceChange = (oldLocation, newLocation) =>
            {
                if (recursed) return;
                recursed = true;
                Check(ReferenceEquals(tracker.Location.m_surface, nextSurface), "outer reentrant callback reads prior state");
                Set(tracker, nested);
                Check(ReferenceEquals(tracker.Location.m_surface, nestedSurface), "nested setter publishes before outer callback returns");
            };
            Set(tracker, initial);
            Check(recursed && ReferenceEquals(tracker.Location.m_surface, firstSurface), "outer setter overwrites reentrant state");
            Vec(tracker.Location.m_worldPosition, left, "outer captured value wins after reentry");

            var trace = new List<string>();
            tracker.OnTrackableSurfaceChange = (oldLocation, newLocation) => { trace.Add("first"); tracker.OnTrackableSurfaceChange = (a, b) => trace.Add("replacement"); };
            tracker.OnTrackableSurfaceChange += (oldLocation, newLocation) => trace.Add("second");
            Set(tracker, next);
            Check(trace.Count == 2 && trace[0] == "first" && trace[1] == "second", "captured multicast survives field mutation");
            Set(tracker, nested);
            Check(trace.Count == 3 && trace[2] == "replacement", "next change loads replacement callback");
            tracker.OnTrackableSurfaceChange = null;
            Set(tracker, default);
            Check(tracker.Location.m_surface == null, "null location published");
            Vec(tracker.Location.m_worldPosition, default, "null-location full value");
            Check(ReferenceEquals(tracker.Surfaces, surfaces), "location setters retain registered surfaces");
            Expect<NullReferenceException>(() => tracker.Teleport(left), "null resolver teleport fault");
            Expect<NullReferenceException>(() => tracker.MoveWorld(right), "null resolver world move fault");
            Expect<NullReferenceException>(() => tracker.MoveLocal(left), "null resolver local move fault");
            Expect<NullReferenceException>(() => tracker.TeleportIfHit(default, -1f), "null resolver ray fault");
            Check(tracker.Location.m_surface == null, "resolver failures do not publish location");
            tracker.SetWorldInformation(null, null);
            Check(tracker.Surfaces == null && Field(typeof(Tracker2D), "m_world").GetValue(tracker) == null, "world setter preserves null input references");
            Check(temporary.Count == 0 && ReferenceEquals(temporary, Field(typeof(Tracker2D), "m_temporaryAllocationWorld").GetValue(tracker)), "world setter retains original temporary dictionary");
            return checks;
        }

        public static int RunEngine()
        {
            checks = 0;
            GameObject owner = null;
            MetadataGroupKey key = null;
            try
            {
                owner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MeshCollider collider = owner.AddComponent<MeshCollider>();
                collider.sharedMesh = owner.GetComponent<MeshFilter>().sharedMesh;
                owner.transform.position = new Vector3(10f, 20f, 30f);
                owner.transform.localScale = new Vector3(2f, 4f, 6f);
                Physics.SyncTransforms();
                var surface = new MeshSurface(collider);
                Check(ReferenceEquals(surface.Mesh, collider), "engine ctor retains actual collider");
                Bounds local = surface.GetBoundingBox();
                Vec(local.center, Vector3.zero, "engine default box is local");
                Vec(local.size, Vector3.one, "engine local shared mesh size");
                Bounds worldBounds = surface.GetBoundingBox(true);
                Vec(worldBounds.center, owner.transform.position, "engine world bounds position");
                Vec(worldBounds.size, owner.transform.localScale, "engine world bounds scaled size");
                Vector3 point = new Vector3(1f, 2f, 3f);
                Vec(surface.TransformPoint(point), new Vector3(12f, 28f, 48f), "engine transform point");
                Vec(surface.InverseTransformPoint(new Vector3(12f, 28f, 48f)), point, "engine inverse point");
                Check(surface.IsActive(), "engine owner active");
                collider.enabled = false;
                Check(surface.IsActive(), "engine active ignores collider enabled flag");
                collider.enabled = true;
                owner.SetActive(false);
                Check(!surface.IsActive(), "engine inactive owner");
                owner.SetActive(true);
                Physics.SyncTransforms();
                var tracker = new Tracker2D(null);
                Check(!tracker.CheckIfPointIsOnNeighbourBounds(Vector3.zero, Vector3.zero), "null current surface before null world");
                var location = new SurfaceLocation { m_surface = surface };
                Set(tracker, location);
                Expect<NullReferenceException>(() => tracker.CheckIfPointIsOnNeighbourBounds(Vector3.zero, Vector3.zero), "non-null location null world faults");
                var world = new Dictionary<ISurface, ISurface[]>();
                tracker.SetWorldInformation(new List<ISurface> { surface }, world);
                Expect<KeyNotFoundException>(() => tracker.CheckIfPointIsOnNeighbourBounds(Vector3.zero, Vector3.zero), "missing neighbour key faults");
                world.Add(surface, new ISurface[0]);
                Check(!tracker.CheckIfPointIsOnNeighbourBounds(owner.transform.position, Vector3.zero), "empty neighbours false");
                world[surface] = new ISurface[] { surface };
                Check(tracker.CheckIfPointIsOnNeighbourBounds(owner.transform.position, Vector3.zero), "actual bounds contain center");
                Check(!tracker.CheckIfPointIsOnNeighbourBounds(new Vector3(12f, 20f, 30f), Vector3.zero), "actual world bounds exclude farther point");
                Check(tracker.CheckIfPointIsOnNeighbourBounds(new Vector3(12f, 20f, 30f), new Vector3(2f, 0f, 0f)), "expand vector adds half to extents");
                world[surface] = null;
                Expect<NullReferenceException>(() => tracker.CheckIfPointIsOnNeighbourBounds(Vector3.zero, Vector3.zero), "null neighbour array faults");
                world[surface] = new ISurface[] { null, surface };
                Expect<NullReferenceException>(() => tracker.CheckIfPointIsOnNeighbourBounds(owner.transform.position, Vector3.zero), "null earlier neighbour fails before later hit");
                world[surface] = new ISurface[] { surface };
                var temporary = (Dictionary<ISurface, ISurface[]>)Field(typeof(Tracker2D), "m_temporaryAllocationWorld").GetValue(tracker);
                Expect<NullReferenceException>(() => tracker.Teleport(new Vector3(12f, 20f, 30f), 1f, null), "filtered teleport reaches genuine null resolver");
                Check(temporary.Count == 1 && ReferenceEquals(temporary[surface], world[surface]), "radius equality retains exact neighbour array");
                Expect<NullReferenceException>(() => tracker.Teleport(new Vector3(100f, 20f, 30f), 1f, null), "filtered miss reaches genuine null resolver");
                Check(temporary.Count == 0, "next teleport clears prior temporary world");
                Expect<NullReferenceException>(() => tracker.Teleport(new Vector3(100f, 20f, 30f), float.NaN, null), "NaN radius reaches genuine null resolver");
                Check(temporary.Count == 1, "ordered greater filter retains NaN radius");
                key = ScriptableObject.CreateInstance<MetadataGroupKey>();
                Expect<NullReferenceException>(() => tracker.Teleport(owner.transform.position, 1f, key), "real MeshSurface null metadata faults without invented guard");
                Check(temporary.Count == 0, "metadata fault occurs after temporary clear");
                return checks;
            }
            finally
            {
                try { if (key != null) UnityEngine.Object.DestroyImmediate(key); }
                finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
            }
        }
    }
}
