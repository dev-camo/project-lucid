namespace Hardlight
{
    public struct UIModernMessage
    {
        private readonly UIModernEventType Type;

        // Original HLModernUI06000035 is one direct enum-field write on both CPUs.
        public UIModernMessage(UIModernEventType type) { Type = type; }
    }
}
