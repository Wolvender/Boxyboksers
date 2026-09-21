using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Boxyboksers.Targets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class BoxingTarget : MonoBehaviour
    {
        public enum State { Idle, FlyingIn, Hittable, Retiring }

        [Header("Fly-in")]
        [Tooltip("Seconds for the target to travel from its entry point to its hit pose.")]
        [SerializeField, Min(0.01f)] private float flyInDuration = 0.32f;

        [Tooltip("Eased 0..1 progress of the fly-in over normalized time. Ease-out reads best on camera.")]
        [SerializeField] private AnimationCurve flyInCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Scale multiplier over the fly-in. A small overshoot gives a satisfying 'pop'.")]
        [SerializeField] private AnimationCurve flyInScale = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.7f, 1.12f), new Keyframe(1f, 1f));

        [Tooltip("If true the target can already be punched mid-flight; otherwise only once it arrives.")]
        [SerializeField] private bool hittableWhileFlyingIn = true;

        [Header("Lifetime")]
        [Tooltip("If off (default), the target stays hittable forever until it's punched — it never times out.")]
        [SerializeField] private bool despawnOnTimeout = false;

        [Tooltip("Only used when 'Despawn On Timeout' is on: seconds it stays hittable before counting as a miss.")]
        [SerializeField, Min(0.1f)] private float hittableLifetime = 2.25f;

        [Tooltip("Seconds for the retire (shrink-out) animation after a hit or timeout.")]
        [SerializeField, Min(0f)] private float retireDuration = 0.18f;

        [Header("Hit detection")]
        [Tooltip("Only colliders on these layers can score a hit (the player's fists/gloves).")]
        [SerializeField] private LayerMask fistLayers = ~0;

        [Tooltip("Minimum impact speed (m/s) to count as a hit. 0 = any touch counts.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 0f;

        [Header("Feedback hooks")]
        public PunchHitEvent OnHit = new PunchHitEvent();

        public UnityEvent OnMiss = new UnityEvent();

        public event Action<BoxingTarget, bool> Retired;

        public State CurrentState { get; private set; } = State.Idle;

        private Rigidbody _body;
        private Collider _collider;
        private Vector3 _baseScale;
        private Coroutine _routine;
        private bool _resolved;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();

            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _collider.isTrigger = true;

            _baseScale = transform.localScale;
            gameObject.SetActive(false);
        }

        public void Activate(Vector3 entryPosition, Vector3 hitPosition, Quaternion hitRotation)
        {
            _resolved = false;
            transform.SetPositionAndRotation(entryPosition, hitRotation);
            transform.localScale = _baseScale * flyInScale.Evaluate(0f);

            gameObject.SetActive(true);
            _collider.enabled = hittableWhileFlyingIn;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlyInThenLive(entryPosition, hitPosition));
        }

        private IEnumerator FlyInThenLive(Vector3 from, Vector3 to)
        {
            CurrentState = State.FlyingIn;
            float t = 0f;
            while (t < flyInDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / flyInDuration);
                transform.position = Vector3.LerpUnclamped(from, to, flyInCurve.Evaluate(k));
                transform.localScale = _baseScale * flyInScale.Evaluate(k);
                yield return null;
            }
            transform.position = to;
            transform.localScale = _baseScale;

            CurrentState = State.Hittable;
            _collider.enabled = true;

            if (despawnOnTimeout)
            {
                float life = 0f;
                while (life < hittableLifetime && !_resolved)
                {
                    life += Time.deltaTime;
                    yield return null;
                }

                if (!_resolved) Resolve(false, default);
            }
            else
            {
                while (!_resolved) yield return null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_resolved || CurrentState == State.Retiring || CurrentState == State.Idle) return;
            if ((fistLayers.value & (1 << other.gameObject.layer)) == 0) return;

            float speed = ResolveImpactSpeed(other);
            if (speed < minImpactSpeed) return;

            Vector3 point = other.ClosestPoint(transform.position);
            var impact = new PunchImpact(point, (transform.position - point).normalized, speed, other);
            Resolve(true, impact);
        }

        private float ResolveImpactSpeed(Collider other)
        {
            if (other.TryGetComponent(out PunchHand hand)) return hand.Speed;
            var rb = other.attachedRigidbody;
            return rb != null ? rb.linearVelocity.magnitude : 0f;
        }

        private void Resolve(bool wasHit, PunchImpact impact)
        {
            if (_resolved) return;
            _resolved = true;
            _collider.enabled = false;

            if (wasHit) OnHit.Invoke(impact);
            else OnMiss.Invoke();

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(RetireRoutine(wasHit));
        }

        private IEnumerator RetireRoutine(bool wasHit)
        {
            CurrentState = State.Retiring;

            float t = 0f;
            Vector3 start = transform.localScale;
            while (t < retireDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / retireDuration);
                transform.localScale = Vector3.LerpUnclamped(start, Vector3.zero, k);
                yield return null;
            }

            transform.localScale = _baseScale;
            CurrentState = State.Idle;
            gameObject.SetActive(false);
            Retired?.Invoke(this, wasHit);
        }
    }

    [System.Serializable]
    public class PunchHitEvent : UnityEvent<PunchImpact> { }

    public readonly struct PunchImpact
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly float Speed;
        public readonly Collider Fist;

        public PunchImpact(Vector3 point, Vector3 normal, float speed, Collider fist)
        {
            Point = point;
            Normal = normal;
            Speed = speed;
            Fist = fist;
        }
    }
}
