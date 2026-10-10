using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006c1, complete four-method declaration.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class LevelRespawnPoints : ISystem
    {
        // The genuine readonly list initializer precedes Object's constructor.
        private readonly List<RespawnPoint> m_respawnPoints = new List<RespawnPoint>();

        // 0x060024a3; ARM 0x56f694. Ordinary Add retains duplicates/null.
        public void Add(RespawnPoint respawnPoint) => m_respawnPoints.Add(respawnPoint);

        // 0x060024a4; ARM 0x56f748. Remove only the first matching item;
        // its Boolean result is discarded, with no Unity null filtering.
        public void Remove(RespawnPoint respawnPoint) => m_respawnPoints.Remove(respawnPoint);

        // 0x060024a5; ARM 0x56f7a4. Return the same live list as an interface,
        // without allocating a copy or a read-only wrapper.
        public IReadOnlyList<RespawnPoint> GetRespawnPoints() => m_respawnPoints;

        // 0x060024a6; ARM 0x56f7ac. Only the initializer and Object base.
        public LevelRespawnPoints() { }
    }
}
