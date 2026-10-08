using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.JSON
{
    // Original HLUnityCore.Runtime 0x0200029b, whole owner and original self/new constraint.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class JSONObjectPooled<T> : IJsonObject where T : JSONObjectPooled<T>, new()
    {
        private static bool s_poolInitialised; // 0x0400084d, mutable original static field.

        // 0x060010c0, original abstract contract.
        public abstract IJsonObject Clone();

        // 0x060010c1. Original direct cast retains the incompatible self-type fault.
        public virtual void Release()
        {
            Hardlight.Pooling.ObjectPool<T>.Despawn((T)this);
        }

        // 0x060010c2. Original engine serializer and append order, no transport execution.
        public virtual bool Encode(StringBuilder builder)
        {
            builder.Append(UnityEngine.JsonUtility.ToJson(this));
            return true;
        }

        // 0x060010c3, original immediate RET on both architectures.
        protected virtual void Reset() { }

        // 0x060010c4. Original cached flag, pool initialisation, then virtual Reset.
        protected static T Spawn()
        {
            if (!s_poolInitialised)
            {
                Hardlight.Pooling.ObjectPool<T>.InitialisePool(JSONHelper.GetPoolSizeForType(typeof(T)));
                s_poolInitialised = true;
            }
            T result = Hardlight.Pooling.ObjectPool<T>.Spawn();
            result.Reset();
            return result;
        }

        // 0x060010c5, original Object base constructor only.
        protected JSONObjectPooled() { }
    }
}
