using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineTargetGroupAssigner_Boss : CinemachineTargetGroupAssigner
    {
        public void AddTarget(Transform target)
        {
            AddMember(target);
        }

        public void RemoveTarget(Transform target)
        {
            RemoveMember(target);
        }

        public CinemachineTargetGroupAssigner_Boss()
        {
        }
    }
}
