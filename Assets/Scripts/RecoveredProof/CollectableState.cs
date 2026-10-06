using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CollectableState
    {
        public int Total { get; private set; }
        public int Collected { get; private set; }

        // Original 06003b28 adds the authored amount with Int32 wrapping.
        public void AddCollectable(int amount) => Total = unchecked(Total + amount);

        public int AdjustCollected(int amount, bool hasTotal)
        {
            // Original 06003b29 chooses the limit before the wrapped addition,
            // and uses the sum's sign for the lower clamp. With a negative Total
            // and nonnegative sum the result can therefore remain negative.
            int previous = Collected;
            int limit = hasTotal ? Total : int.MaxValue;
            int next = unchecked(previous + amount);
            Collected = next < 0 ? 0 : (next < limit ? next : limit);
            return unchecked(Collected - previous);
        }

        // Original 06003b2a retains the registered total.
        public void Reset() => Collected = 0;

        // Original 06003b2b only calls Object's constructor; both fields stay zero.
        public CollectableState() { }
    }
}
