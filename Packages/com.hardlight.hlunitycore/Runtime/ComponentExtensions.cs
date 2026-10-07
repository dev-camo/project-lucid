using UnityEngine;

namespace Hardlight
{
    // Original Core owner: four declarations and no fields. Assert-named
    // accessors in the shipped release contain direct Unity calls; adding an
    // assertion, fallback or Unity-null receiver guard changes those bodies.
    public static class ComponentExtensions
    {
        // 06000261; generic reference-type constraint remains the original one.
        public static T GetComponentWithAssert<T>(this Component thisComponent) where T : class
        {
            return thisComponent.GetComponent<T>();
        }

        // 06000262; original optional includeInactive=false.
        public static T GetComponentInChildrenWithAssert<T>(this Component thisComponent, bool includeInactive = false) where T : class
        {
            return thisComponent.GetComponentInChildren<T>(includeInactive);
        }

        // 06000263; genuine Unity one-argument generic parent query.
        public static T GetComponentInParentWithAssert<T>(this Component thisComponent) where T : class
        {
            return thisComponent.GetComponentInParent<T>();
        }

        // 06000264; destroyImmediate is required, no original default.
        public static void Destroy(this Component thisComponent, bool destroyImmediate)
        {
            if (destroyImmediate) UnityEngine.Object.DestroyImmediate(thisComponent);
            else UnityEngine.Object.Destroy(thisComponent);
        }
    }
}
