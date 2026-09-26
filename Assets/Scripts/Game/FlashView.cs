using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// One pooled expand-and-fade flash, played wherever a packet was destroyed. Implements the
    /// last clause of Story 002 acceptance criterion 3 ("destroying a packet gives a short flash").
    /// </summary>
    /// <remarks>
    /// Driven by an explicit <see cref="Tick"/> from <see cref="NetworkMapView"/> rather than a
    /// coroutine or <c>Update</c>, so the whole effect is frame-rate independent and allocation
    /// free: no <c>WaitForSeconds</c>, no per-flash MonoBehaviour.
    /// </remarks>
    public sealed class FlashView
    {
        private readonly Transform _transform;
        private readonly SpriteRenderer _renderer;

        private float _elapsed;
        private float _duration;
        private float _startSize;
        private float _endSize;
        private Color _color;

        /// <summary>True while this flash is playing and must not be handed out by the pool.</summary>
        public bool IsPlaying { get; private set; }

        private FlashView(Transform transform, SpriteRenderer renderer)
        {
            _transform = transform;
            _renderer = renderer;
        }

        /// <summary>Creates a pooled, initially hidden flash visual under <paramref name="parent"/>.</summary>
        public static FlashView Create(Transform parent, int sortingOrder, int index)
        {
            var go = new GameObject("Flash" + index);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PrimitiveSpriteFactory.GetShape(PrimitiveShape.Circle);
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;

            return new FlashView(go.transform, renderer);
        }

        /// <summary>Starts (or restarts) the flash at <paramref name="worldPosition"/>.</summary>
        public void Play(Vector2 worldPosition, Color color, ViewConfig config)
        {
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, config.FlashDuration);
            _startSize = config.FlashStartSize;
            _endSize = config.FlashEndSize;
            _color = color;

            _transform.localPosition = new Vector3(worldPosition.x, worldPosition.y, 0f);
            _transform.localScale = new Vector3(_startSize, _startSize, 1f);
            _renderer.color = _color;
            _renderer.enabled = true;
            IsPlaying = true;
        }

        /// <summary>
        /// Advances the flash by <paramref name="deltaTime"/> seconds.
        /// </summary>
        /// <returns>True once the flash has finished and may be returned to the pool.</returns>
        public bool Tick(float deltaTime)
        {
            if (!IsPlaying)
            {
                return true;
            }

            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            float size = Mathf.Lerp(_startSize, _endSize, t);
            _transform.localScale = new Vector3(size, size, 1f);

            Color color = _color;
            color.a = 1f - t;
            _renderer.color = color;

            if (t >= 1f)
            {
                Stop();
                return true;
            }

            return false;
        }

        /// <summary>Hides the flash immediately and marks it reusable.</summary>
        public void Stop()
        {
            IsPlaying = false;
            _renderer.enabled = false;
        }
    }
}
