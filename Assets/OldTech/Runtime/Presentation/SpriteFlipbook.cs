using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        public Sprite[] frames;
        public float framesPerSecond = 7f;
        public int CurrentFrame { get; private set; }
        private SpriteRenderer drawing;
        private float elapsed;

        private void Awake() { drawing = GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            elapsed += Time.deltaTime;
            CurrentFrame = Mathf.FloorToInt(elapsed * framesPerSecond) % frames.Length;
            drawing.sprite = frames[CurrentFrame];
        }
    }
}
