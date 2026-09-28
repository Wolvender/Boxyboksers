using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Boxyboksers.Player
{
    [RequireComponent(typeof(AudioSource))]
    public class PunchImpactFeedback : MonoBehaviour
    {
        private AudioSource _audioSource;

        [SerializeField] private AudioClip impactSound;
        [SerializeField] private HapticImpulsePlayer hapticPlayer;
        [SerializeField] private float hapticDuration = 0.05f;
        [SerializeField] private float maxImpactSpeed = 4f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            float amplitude = Mathf.Clamp01(collision.relativeVelocity.magnitude / maxImpactSpeed);

            if (impactSound != null)
                _audioSource.PlayOneShot(impactSound, amplitude);

            if (hapticPlayer != null)
                hapticPlayer.SendHapticImpulse(amplitude, hapticDuration);
        }
    }
}
