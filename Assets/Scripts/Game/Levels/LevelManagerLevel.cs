using Unity.IL2CPP.CompilerServices;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LevelManagerLevel
    {
        public GameplayLevelDefinition LevelDefinition { get; }
        public GameplayLevelDefinition SceneDefinition { get; }
        public LevelData Data { get; set; }
        public AsyncOperationHandle<SceneInstance> SceneInstance;

        public LevelManagerLevel(GameplayLevelDefinition levelDefinition, GameplayLevelDefinition sceneDefinition)
        {
            LevelDefinition = levelDefinition;
            SceneDefinition = sceneDefinition;
        }
    }
}
