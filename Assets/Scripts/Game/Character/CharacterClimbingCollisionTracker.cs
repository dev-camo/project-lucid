using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200040c. Accumulates the contact normals of climbable terrain between
    // updates and publishes their average direction as CollisionNormal. Native code inlines
    // TryAddCollision into both collision callbacks and TerrainClimbable into TryAddCollision.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterClimbingCollisionTracker
    {
        public readonly LayerMask LayerMask;

        public Vector3 WorldVelocityAtLastTerrainEnter { get; private set; }
        public Vector3 CollisionNormal { get; private set; }
        public bool IsColliding { get; private set; }
        private Vector3 m_accruedCollisionNormals;
        private int m_collisionCount;
        private readonly Character m_character;
        private bool m_started;

        // 060017bd
        public CharacterClimbingCollisionTracker(Character character, LayerMask layerMask)
        {
            m_character = character;
            LayerMask = layerMask;
        }

        // 060017be
        public void Start()
        {
            m_character.OnCollisionDataEnterEvent += OnCollisionDataEnter;
            m_character.OnCollisionDataStayEvent += OnCollisionDataStay;
            m_started = true;
        }

        // 060017bf
        public void Stop()
        {
            IsColliding = false;
            CollisionNormal = Vector3.zero;
            m_collisionCount = 0;
            m_accruedCollisionNormals = Vector3.zero;
            m_character.OnCollisionDataEnterEvent -= OnCollisionDataEnter;
            m_character.OnCollisionDataStayEvent -= OnCollisionDataStay;
            m_started = false;
        }

        // 060017c0; contacts counted since the last update decide whether the tracker is colliding,
        // else a short raycast against the ground below the character is tried. The accrued normals
        // are consumed every update; a near-zero sum leaves no normal.
        public void Update()
        {
            IsColliding = m_collisionCount > 0;
            if (!IsColliding)
                IsColliding = PerformClimbableGroundCheck();

            float magnitude = m_accruedCollisionNormals.magnitude;
            if (IsColliding && magnitude > 1E-05f)
                CollisionNormal = m_accruedCollisionNormals / magnitude;
            else
                CollisionNormal = Vector3.zero;
            m_collisionCount = 0;
            m_accruedCollisionNormals = Vector3.zero;
        }

        // 060017c1
        private void OnCollisionDataEnter(CharacterCollisionData collisionData, Collision collision)
        {
            if (TryAddCollision(collisionData, collision))
                WorldVelocityAtLastTerrainEnter = m_character.WorldVelocity;
        }

        // 060017c2
        private void OnCollisionDataStay(CharacterCollisionData collisionData, Collision collision)
        {
            TryAddCollision(collisionData, collision);
        }

        // 060017c3; the contact normals are subtracted, so the accrued sum points into the terrain.
        private bool TryAddCollision(CharacterCollisionData collisionData, Collision collision)
        {
            if (!TerrainClimbable(LayerMask, collisionData))
                return false;
            int contactCount = collision.contactCount;
            for (int index = 0; index < contactCount; index++)
            {
                ContactPoint contact = collision.GetContact(index);
                m_accruedCollisionNormals -= contact.normal;
            }
            m_collisionCount += contactCount;
            return true;
        }

        // 060017c4
        public static bool TerrainClimbable(LayerMask layerMask, CharacterCollisionData collisionData)
        {
            return layerMask.IncludesLayer(collisionData.gameObject) && collisionData.IsClimbable;
        }

        // 060017c5; casts down from the character's centre, past its own half height by the minimum
        // ground check distance. A hit on climbable terrain adds that surface's normal.
        private bool PerformClimbableGroundCheck()
        {
            var ray = new Ray(m_character.Bounds.center, (-m_character.UpDirection).normalized);
            float distance = m_character.HalfHeight + m_character.Settings.Collider.GroundCheckMinDistance;
            if (!Physics.Raycast(ray, out RaycastHit hit, distance, LayerMask))
                return false;
            if (!m_character.TrackManager.TryGetCharacterCollisionData(hit.collider, out CharacterCollisionData collisionData))
                return false;
            if (!TerrainClimbable(LayerMask, collisionData))
                return false;
            m_accruedCollisionNormals = hit.normal + m_accruedCollisionNormals;
            m_collisionCount++;
            return true;
        }
    }
}
