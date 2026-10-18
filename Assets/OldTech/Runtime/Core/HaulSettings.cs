using UnityEngine;

namespace OldTech
{
    // Shared defaults for each delivery; HaulSession makes a private runtime copy.
    [CreateAssetMenu(menuName = "Old Tech/Haul Settings")]
    public sealed class HaulSettings : ScriptableObject
    {
        [Header("Cargo")]
        [Range(10f, 60f)] public float cargoMass = 24f;
        [Range(0.5f, 7f)] public float pushAcceleration = 3.4f;
        [Range(0.5f, 5f)] public float topSpeed = 2.6f;
        [Range(0.1f, 6f)] public float braking = 2.3f;

        [Header("Robot payload")]
        [Range(2f, 30f)] public float robotMass = 16f;

        [Header("Logs")]
        [Range(1f, 8f)] public float logMass = 2.4f;
        [Range(0.01f, 0.8f)] public float rollingDrag = 0.045f;
        [Range(0.2f, 1.5f)] public float friction = 0.85f;

        [Header("Mouse grip")]
        [Range(200f, 3000f)] public float gripSpring = 1100f;
        [Range(10f, 180f)] public float gripDamping = 85f;
        [Range(3f, 18f)] public float dragSpeed = 9f;
        [Range(100f, 650f)] public float maxGripForce = 230f;

        [Header("Route")]
        public float[] checkpointPositions = { 9f, 23.5f };
        [Tooltip("Required log count at start, then at each checkpoint.")]
        public int[] checkpointLogCounts = { 3, 3, 3, 4, 4, 4, 4 };
        public float fourthLogX = 29f;
        public float finishX = 39f;
        public float impactThreshold = 3.2f;

        public int LogCountAt(int checkpointIndex)
        {
            if (checkpointLogCounts == null || checkpointLogCounts.Length == 0) return 3;
            return Mathf.Clamp(checkpointLogCounts[Mathf.Clamp(checkpointIndex, 0, checkpointLogCounts.Length - 1)], 3, 4);
        }
    }
}
