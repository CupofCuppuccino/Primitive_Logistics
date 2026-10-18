using UnityEngine;

namespace OldTech
{
    public static class RouteGeometry
    {
        public const float SummitHeight = 0.65f;
        public const float FinishX = 190f;
        private static readonly float[] PitFootSamples = { 0f, -0.36f, 0.36f };
        private static readonly float[] GroundFootSamples = { 0f };
        // x0, x1, y0, y1. Pits have real lower floors, without invisible bridges.
        public static readonly Vector4[] Sections =
        {
            new Vector4(-18f, 12f, 0f, 0f),
            new Vector4(12f, 21f, 0f, SummitHeight),
            new Vector4(21f, 26f, SummitHeight, SummitHeight),
            new Vector4(26f, 36f, SummitHeight, 0f),
            new Vector4(36f, 42f, 0f, 0f),
            new Vector4(42f, 43f, 0f, -0.9f),
            new Vector4(43f, 47f, -0.9f, -0.9f),
            new Vector4(47f, 48f, -0.9f, 0f),
            new Vector4(48f, 62f, 0f, 0f),
            new Vector4(62f, 67f, 0f, 3.8f),
            new Vector4(67f, 74f, 3.8f, 3.8f),
            new Vector4(74f, 80f, 3.8f, 0f),
            new Vector4(80f, 96f, 0f, 0f),
            new Vector4(96f, 99.4f, -2.9f, -2.9f),
            new Vector4(99.4f, 113f, 0f, 0f),
            new Vector4(113f, 117f, 0f, 3.2f),
            new Vector4(117f, 121f, 3.2f, 3.2f),
            new Vector4(121f, 126f, 3.2f, 0f),
            new Vector4(126f, 171f, 0f, 0f),
            new Vector4(171f, 172.1f, -0.85f, -0.85f),
            new Vector4(172.1f, 206f, 0f, 0f)
        };

        public static float HeightAt(float x)
        {
            foreach (Vector4 section in Sections)
                if (x >= section.x && x < section.y)
                    return Mathf.Lerp(section.z, section.w, (x - section.x) / (section.y - section.x));
            return 0f;
        }

        public static bool IsPit(float x) => (x >= 42f && x <= 48f) || (x >= 96f && x <= 99.4f) || (x >= 171f && x <= 172.1f);

        public static bool TryWalkingSurface(float x, out float height)
        {
            float ground = HeightAt(x);
            height = ground;
            // A short stride can span the gaps between adjacent round logs.
            float[] steps = IsPit(x) ? PitFootSamples : GroundFootSamples;
            foreach (float offset in steps)
            {
                if (!Physics.Raycast(new Vector3(x + offset, Mathf.Max(0f, ground) + 2f, -0.1f), Vector3.down,
                    out RaycastHit hit, 5f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore)) continue;
                height = Mathf.Max(height, hit.point.y);
                if (!IsPit(x) || height >= -0.18f) return true;
            }
            return !IsPit(x);
        }
    }
}
