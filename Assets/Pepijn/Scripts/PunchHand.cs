using UnityEngine;

namespace Boxyboksers.Targets
{
    [DisallowMultipleComponent]
    public class PunchHand : MonoBehaviour
    {
        [Tooltip("Smoothing for the reported speed. 0 = raw/twitchy, 1 = very smooth/laggy.")]
        [SerializeField, Range(0f, 1f)] private float smoothing = 0.35f;

        public float Speed { get; private set; }

        public Vector3 Velocity { get; private set; }

        private Vector3 _lastPosition;

        private void OnEnable()
        {
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
            Speed = 0f;
        }

        private void FixedUpdate()
        {
            Vector3 pos = transform.position;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            Vector3 instant = (pos - _lastPosition) / dt;
            Velocity = Vector3.Lerp(instant, Velocity, smoothing);
            Speed = Velocity.magnitude;
            _lastPosition = pos;
        }
    }
}
