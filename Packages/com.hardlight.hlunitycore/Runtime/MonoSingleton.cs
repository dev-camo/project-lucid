using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MonoSingleton<T> : MonoBehaviour, ISystem where T : MonoBehaviour, ISystem
    {
        // HLUnityCore.Runtime 0x0400072f / 0x06000e04..0x06000e05.
        public static T Instance { get; private set; }

        // 0x06000e06 plus original <WaitOnInstance>d__4 0x06000e0d..0x06000e12.
        // Yield null while Unity engine equality regards the current Instance as null.
        public static IEnumerator WaitOnInstance()
        {
            while (Instance == null) yield return null;
        }

        // 0x06000e07..0x06000e08. Use genuine Unity null semantics, not CLR identity.
        public static bool NotNull() => Instance != null;
        public static bool IsNull() => Instance == null;

        // 0x06000e09. Registry state selects the branch; Instance is not a fallback.
        protected virtual void Awake()
        {
            if (ProcessManager.IsSystemNull<T>())
            {
                ProcessManager.RegisterSystem(this, null, false, false);
                // Native performs isinst before the generic reference cast. A
                // mismatched T stores null after registration rather than throwing here.
                Instance = (T)(object)(this as T);
                return;
            }

            System.Type type = typeof(T);
            GameObject existingObject = Instance.gameObject;
            Transform existingParent = existingObject.transform.parent;
            GameObject newObject = gameObject;
            Transform newParent = newObject.transform.parent;
            string existingName = existingObject.name;
            string existingDescription = existingObject.ToString();
            string existingParentName = existingParent != null ? existingParent.name : "null";
            string existingSceneName = existingObject.scene.name;
            string newName = newObject.name;
            string newDescription = newObject.ToString();
            string newParentName = newParent != null ? newParent.name : "null";
            string newSceneName = newObject.scene.name;
            HLOutput.LogError(string.Concat(new[] {
                string.Format("Duplicate singletons found of type: {0},\n", type),
                "Existing object: ", existingName, ", ", existingDescription,
                ", existing object parent: ", existingParentName, ", found in scene: ", existingSceneName,
                "\n New object : ", newName, ", ", newDescription,
                ", new object parent: ", newParentName, ", found in scene: ", newSceneName
            }));
            if (Application.isPlaying)
            {
                HLOutput.LogError("Destroying New Object: " + newName);
                Destroy(newObject);
            }
        }

        // 0x06000e0a. Unregister the caller first; then reread/destroy the current
        // Instance's entire GameObject and finally clear the static property.
        public void DestroyInstance()
        {
            ProcessManager.UnregisterSystem(this);
            DestroyImmediate(Instance.gameObject);
            Instance = null;
        }

        // 0x06000e0b. Unity equality precedes identity-based registry removal;
        // clear after removal, including any reentrant replacement it may cause.
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                ProcessManager.UnregisterSystem(this);
                Instance = null;
            }
        }

        // 0x06000e0c. Genuine MonoBehaviour base constructor only.
        public MonoSingleton() { }
    }
}
