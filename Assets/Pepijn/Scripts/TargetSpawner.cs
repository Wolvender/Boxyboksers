using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Boxyboksers.Targets
{
    public class TargetSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct HeightTier
        {
            [Tooltip("Label for readability in the inspector (Low / Mid / High).")]
            public string name;
            [Tooltip("Height range for this tier, in metres relative to the player reference's Y.")]
            public float minHeight;
            public float maxHeight;
            [Tooltip("Relative likelihood of this tier being chosen.")]
            [Min(0f)] public float weight;
        }

        [Header("References")]
        [Tooltip("Target prefab. Must have a BoxingTarget component. Leave the prefab inactive.")]
        [SerializeField] private BoxingTarget targetPrefab;

        [Tooltip("The player's head/HMD (or rig). The arc is built in front of this. Defaults to the main camera.")]
        [SerializeField] private Transform player;

        [Tooltip("Optional parent for spawned targets, to keep the hierarchy tidy. Defaults to this object.")]
        [SerializeField] private Transform poolParent;

        [Header("Arc placement")]
        [Tooltip("Total width of the spawn arc, in degrees, centred on the player's forward.")]
        [SerializeField, Range(0f, 300f)] private float arcAngle = 160f;

        [Tooltip("Closest / farthest reach of a target from the player, in metres.")]
        [SerializeField, Min(0.1f)] private float minRadius = 0.55f;
        [SerializeField, Min(0.1f)] private float maxRadius = 0.95f;

        [Tooltip("Height bands (low/mid/high) so targets vary in reach. Picked by weight.")]
        [SerializeField]
        private HeightTier[] heightTiers =
        {
            new HeightTier { name = "Low",  minHeight = -0.55f, maxHeight = -0.25f, weight = 1f },
            new HeightTier { name = "Mid",  minHeight = -0.15f, maxHeight =  0.15f, weight = 1.4f },
            new HeightTier { name = "High", minHeight =  0.25f, maxHeight =  0.55f, weight = 1f },
        };

        [Tooltip("Extra distance beyond the hit pose the target flies in from (further out, then inward).")]
        [SerializeField, Min(0f)] private float flyInDistance = 0.6f;

        [Tooltip("Vertical offset of the fly-in entry point. Negative = swoops up from below.")]
        [SerializeField] private float flyInVerticalOffset = -0.25f;

        [Header("Pacing")]
        [Tooltip("Never more than this many targets alive at once.")]
        [SerializeField, Range(1, 6)] private int maxConcurrent = 2;

        [Tooltip("Random gap between spawns (seconds). Kept short so there's little dead air.")]
        [SerializeField, Min(0.05f)] private float minSpawnInterval = 0.45f;
        [SerializeField, Min(0.05f)] private float maxSpawnInterval = 0.9f;

        [Tooltip("Don't drop a new target right on top of the last one — minimum separation in metres.")]
        [SerializeField, Min(0f)] private float minSeparation = 0.35f;

        [Header("Difficulty ramp (optional)")]
        [Tooltip("If on, intervals scale from 1x toward the multiplier below over rampDuration.")]
        [SerializeField] private bool useDifficultyRamp = false;
        [SerializeField, Min(1f)] private float rampDuration = 60f;
        [Tooltip("Interval multiplier reached at the end of the ramp (0.5 = twice as fast).")]
        [SerializeField, Range(0.1f, 1f)] private float endIntervalScale = 0.55f;

        [Header("Startup")]
        [SerializeField] private int prewarm = 4;
        [SerializeField] private bool spawnOnStart = true;

        private ObjectPool<BoxingTarget> _pool;
        private readonly List<BoxingTarget> _active = new List<BoxingTarget>(8);
        private float _spawnTimer;
        private float _elapsed;
        private bool _running;

        public int ActiveCount => _active.Count;

        private void Awake()
        {
            if (player == null && Camera.main != null) player = Camera.main.transform;
            if (poolParent == null) poolParent = transform;

            _pool = new ObjectPool<BoxingTarget>(
                createFunc: CreatePooledTarget,
                actionOnGet: null,
                actionOnRelease: null,
                actionOnDestroy: t => { if (t != null) Destroy(t.gameObject); },
                collectionCheck: true,
                defaultCapacity: Mathf.Max(prewarm, maxConcurrent),
                maxSize: 32);

            Prewarm(prewarm);
        }

        private void Start()
        {
            if (spawnOnStart) StartSpawning();
        }

        public void StartSpawning()
        {
            if (targetPrefab == null)
            {
                Debug.LogError("[TargetSpawner] No target prefab assigned.", this);
                return;
            }
            _running = true;
            _elapsed = 0f;
            _spawnTimer = 0f;
        }

        public void StopSpawning() => _running = false;

        public void ClearAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
                ReturnToPool(t, false);
            }
            _active.Clear();
        }

        private void Update()
        {
            if (!_running) return;
            _elapsed += Time.deltaTime;

            if (_active.Count >= maxConcurrent) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            SpawnOne();
            _spawnTimer = NextInterval();
        }

        private void SpawnOne()
        {
            if (!TryPickHitPose(out Vector3 hitPos, out Quaternion rot)) return;

            Vector3 toPlayer = player.position - hitPos;
            toPlayer.y = 0f;
            Vector3 outward = toPlayer.sqrMagnitude > 0.0001f ? -toPlayer.normalized : transform.forward;
            Vector3 entryPos = hitPos + outward * flyInDistance + Vector3.up * flyInVerticalOffset;

            BoxingTarget target = _pool.Get();
            target.Retired += OnTargetRetired;
            _active.Add(target);
            target.Activate(entryPos, hitPos, rot);
        }

        private bool TryPickHitPose(out Vector3 position, out Quaternion rotation)
        {
            const int maxTries = 6;
            for (int attempt = 0; attempt < maxTries; attempt++)
            {
                float half = arcAngle * 0.5f;
                float angle = Random.Range(-half, half);
                float radius = Random.Range(minRadius, maxRadius);

                Vector3 flatForward = player.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
                flatForward.Normalize();

                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * flatForward;
                float height = SampleHeight();
                position = player.position + dir * radius + Vector3.up * height;

                if (IsClearOfActive(position) || attempt == maxTries - 1)
                {
                    Vector3 look = player.position - position;
                    look.y = 0f;
                    rotation = look.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(look.normalized, Vector3.up)
                        : Quaternion.identity;
                    return true;
                }
            }

            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        private bool IsClearOfActive(Vector3 pos)
        {
            float sqrMin = minSeparation * minSeparation;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] == null) continue;
                if ((_active[i].transform.position - pos).sqrMagnitude < sqrMin) return false;
            }
            return true;
        }

        private float SampleHeight()
        {
            if (heightTiers == null || heightTiers.Length == 0) return 0f;

            float total = 0f;
            for (int i = 0; i < heightTiers.Length; i++) total += Mathf.Max(0f, heightTiers[i].weight);
            if (total <= 0f) return 0f;

            float roll = Random.value * total;
            for (int i = 0; i < heightTiers.Length; i++)
            {
                roll -= Mathf.Max(0f, heightTiers[i].weight);
                if (roll <= 0f) return Random.Range(heightTiers[i].minHeight, heightTiers[i].maxHeight);
            }
            ref HeightTier last = ref heightTiers[heightTiers.Length - 1];
            return Random.Range(last.minHeight, last.maxHeight);
        }

        private float NextInterval()
        {
            float interval = Random.Range(minSpawnInterval, maxSpawnInterval);
            if (useDifficultyRamp)
            {
                float k = Mathf.Clamp01(_elapsed / rampDuration);
                interval *= Mathf.Lerp(1f, endIntervalScale, k);
            }
            return interval;
        }

        private void OnTargetRetired(BoxingTarget target, bool wasHit)
        {
            ReturnToPool(target, true);
        }

        private void ReturnToPool(BoxingTarget target, bool removeFromActive)
        {
            if (target == null) return;
            target.Retired -= OnTargetRetired;
            if (removeFromActive) _active.Remove(target);
            _pool.Release(target);
        }

        private BoxingTarget CreatePooledTarget()
        {
            BoxingTarget t = Instantiate(targetPrefab, poolParent);
            t.gameObject.SetActive(false);
            return t;
        }

        private void Prewarm(int count)
        {
            if (targetPrefab == null || count <= 0) return;
            var temp = new BoxingTarget[count];
            for (int i = 0; i < count; i++) temp[i] = _pool.Get();
            for (int i = 0; i < count; i++) _pool.Release(temp[i]);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform p = player != null ? player : transform;
            Vector3 flatForward = p.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            const int segments = 24;
            float half = arcAngle * 0.5f;

            DrawArcBand(p.position, flatForward, half, segments, minRadius);
            DrawArcBand(p.position, flatForward, half, segments, maxRadius);

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
            foreach (var tier in heightTiers)
            {
                float h = (tier.minHeight + tier.maxHeight) * 0.5f;
                DrawArcBand(p.position + Vector3.up * h, flatForward, half, segments, (minRadius + maxRadius) * 0.5f);
            }
        }

        private void DrawArcBand(Vector3 origin, Vector3 forward, float halfAngle, int segments, float radius)
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.7f);
            Vector3 prev = origin + Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector3 point = origin + Quaternion.AngleAxis(a, Vector3.up) * forward * radius;
                Gizmos.DrawLine(prev, point);
                prev = point;
            }
        }
#endif
    }
}
