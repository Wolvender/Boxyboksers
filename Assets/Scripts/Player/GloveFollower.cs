using UnityEngine;

namespace Boxyboksers.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class GloveFollower : MonoBehaviour
    {
        private Rigidbody _rigidbody;

        [SerializeField] private Transform trackedHand;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (trackedHand == null) return;

            _rigidbody.MovePosition(trackedHand.position);
            _rigidbody.MoveRotation(trackedHand.rotation);
        }
    }
}
