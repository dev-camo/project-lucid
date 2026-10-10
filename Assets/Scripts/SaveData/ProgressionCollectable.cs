namespace HardlightProject
{
    // Original 020007a1 / 06002ae4; the three readonly fields preserve raw counts.
    public readonly struct ProgressionCollectable
    {
        public readonly CollectableType Type;
        public readonly int Collected;
        public readonly int Total;

        public ProgressionCollectable(CollectableType type, int collected, int total)
        {
            Type = type;
            Collected = collected;
            Total = total;
        }
    }
}
