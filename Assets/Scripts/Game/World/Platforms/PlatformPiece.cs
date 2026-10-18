using System;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Original Game.Runtime 02000777: one platform piece made of several game objects
    // (the piece and its LODs), driven together. Destroyed entries are skipped.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class PlatformPiece
    {
        [Tooltip("Links to all game objects, including LODs")]
        [SerializeField]
        public GameObject[] GameObjects;

        // 060029ee: each copy keeps the source object's parent and world pose.
        public PlatformPiece(PlatformPiece source)
        {
            GameObjects = new GameObject[source.GameObjects.Length];
            for (int i = 0; i < GameObjects.Length; i++)
                GameObjects[i] = Object.Instantiate(source.GameObjects[i], source.GameObjects[i].transform.parent, true);
        }

        public void SetActive(bool isActive)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                GameObjects[i].SetActive(isActive);
            }
        }

        // 060029f0: only the local Y changes.
        public void SetHeight(float height)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                Transform pieceTransform = GameObjects[i].transform;
                Vector3 localPosition = pieceTransform.localPosition;
                localPosition.y = height;
                pieceTransform.localPosition = localPosition;
            }
        }

        public void SetLocalPosition(Vector3 position)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                GameObjects[i].transform.localPosition = position;
            }
        }

        public void SetLocalRotation(Quaternion rotation)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                GameObjects[i].transform.localRotation = rotation;
            }
        }

        public void SetLocalScale(Vector3 scale)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                GameObjects[i].transform.localScale = scale;
            }
        }

        // 060029f4: the retail body only fetches each object's PlatformComponents and
        // discards them; componentType is unused there.
        public void SetComponentType(PlatformComponentTypes componentType)
        {
            for (int i = 0; i < GameObjects.Length; i++)
            {
                if (GameObjects[i] == null)
                    continue;
                GameObjects[i].GetComponentsInChildren<PlatformComponent>();
            }
        }

        // 060029f5 and 060029f6 are empty in the retail player.
        public void Destroy()
        {
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void RegisterUndo(Object objectToUndo, string name)
        {
        }
    }
}
