using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalInstancedObjectPoolGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // InstancedObjectPool<T>.Register 0x06003cf9, shared x86-64 0x13caf10:
        // GetInstanceID 0x13caf9d, ContainsKey 0x13cafbb and, only when absent,
        // a new ObjectPool (max 0x2710) 0x13cb0bc added with Dictionary.Add
        // 0x13cb0de. Get 0x06003cfb (0x13cb120) stores null 0x13cb198 when
        // TryGetValue fails; Release 0x06003cfc (0x13cb1b0) skips ObjectPool.
        // Release for an unknown key. Overwriting the pool on re-registration,
        // or leaving the out value unassigned or throwing on a miss, fails here.
        [Test]
        public void ReRegistrationKeepsTheFirstPoolAndUnknownKeysAreSilentMisses()
        {
            Mesh template = null, clone = null;
            try
            {
                template = new Mesh { name = "Lucid pooled template" };
                var pool = new InstancedObjectPool<Mesh>();
                var pools = (Dictionary<int, ObjectPool<Mesh>>)typeof(InstancedObjectPool<Mesh>)
                    .GetField("m_pools", Instance).GetValue(pool);

                int key = pool.Register(template, 3);
                Assert.That(key, Is.EqualTo(template.GetInstanceID()));
                ObjectPool<Mesh> first = pools[key];
                Assert.That(pool.Register(template, 7), Is.EqualTo(key));
                Assert.That(pools.Count, Is.EqualTo(1));
                Assert.That(pools[key], Is.SameAs(first), "the first pool is kept");
                var list = (List<Mesh>)typeof(ObjectPool<Mesh>).GetField("m_List", Instance).GetValue(first);
                Assert.That(list.Capacity, Is.EqualTo(3), "the first registration's capacity is kept");
                Assert.That(first.CountAll, Is.Zero, "registration does not clone");

                int unknown = key + 1;
                Mesh missed = template;
                Assert.That(pool.Get(unknown, out missed), Is.False);
                Assert.That(missed, Is.Null);
                Assert.DoesNotThrow(() => pool.Release(unknown, template));
                Assert.That(template != null, Is.True);
                Assert.That(first.CountInactive, Is.Zero, "an unknown key never reaches a registered pool");

                Assert.That(pool.Get(key, out clone), Is.True);
                Assert.That(clone, Is.Not.Null.And.Not.SameAs(template));
                Assert.That(first.CountAll, Is.EqualTo(1));
                pool.Release(key, clone);
                Assert.That(first.CountInactive, Is.EqualTo(1));
            }
            finally
            {
                // The pool's destroy delegate is Object.Destroy, which edit mode
                // rejects; owned meshes are destroyed directly instead.
                if (clone != null) Object.DestroyImmediate(clone);
                if (template != null) Object.DestroyImmediate(template);
            }
        }
    }
}
