namespace Hardlight
{
    public interface PooledPrefab
    {
        int Index { get; }
    }

    public readonly struct PooledPrefab<T> : PooledPrefab
    {
        public int Index { get; }
        public T Instance { get; }

        public PooledPrefab(T instance, int index)
        {
            Instance = instance;
            Index = index;
        }
    }
}
