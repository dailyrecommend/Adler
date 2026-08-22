using Adler.Combat;
using Adler.Flight;
using Adler.UI;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 피격과 격추 소리.
    /// <para>
    /// <see cref="DamageFeedback"/>이 내보내는 세기를 그대로 쓴다. 그쪽은 짧은 시간에
    /// 들어온 피해를 한 번으로 묶는데, 기총에 긁히면 초당 스무 번씩 맞으므로 피해마다
    /// 울리면 소리가 쌓이다 잘려 나간다. 화면 흔들림과 같은 박자로 울리는 것이
    /// 맞기도 하다 — 눈과 귀가 따로 놀면 두 번 맞은 것처럼 느껴진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("피격 반응을 내보내는 쪽. 비워두면 기체 아래에서 찾는다.")]
        [SerializeField] private DamageFeedback _feedback;

        [Header("소리")]
        [Tooltip("소리가 나올 소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("맞았을 때.")]
        [SerializeField] private AudioClip _hit;

        [Tooltip("격추됐을 때. 비워둬도 된다.")]
        [SerializeField] private AudioClip _destroyed;

        [Header("세기")]
        [Tooltip("스치듯 맞았을 때의 크기.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minVolume = 0.35f;

        [Tooltip("한 번에 크게 맞았을 때의 크기.")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxVolume = 1f;

        [Tooltip("크게 맞을수록 음을 낮춘다. 0이면 언제나 같은 높이로 울린다.\n" +
                 "큰 피해가 낮고 묵직하게 들리면 크기만 키우는 것보다 잘 전달된다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchDrop = 0.2f;

        [Range(0f, 1f)]
        [SerializeField] private float _destroyedVolume = 1f;

        private Health _health;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _health = _aircraft != null ? _aircraft.Health : null;

            if (_feedback == null && _aircraft != null)
            {
                _feedback = _aircraft.GetComponentInChildren<DamageFeedback>(includeInactive: true);
            }

            if (_source == null || _feedback == null)
            {
                Debug.LogError(
                    $"{nameof(DamageAudio)}: Audio Source 또는 {nameof(DamageFeedback)}이 없습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;
        }

        private void OnEnable()
        {
            _feedback.Reacted += OnReacted;

            if (_health != null)
            {
                _health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            _feedback.Reacted -= OnReacted;

            if (_health != null)
            {
                _health.Died -= OnDied;
            }
        }

        /// <summary>세기는 이번에 잃은 내구도가 최대에서 차지하는 비율이다.</summary>
        private void OnReacted(float strength)
        {
            if (_hit == null)
            {
                return;
            }

            _source.pitch = 1f - (_pitchDrop * strength);
            _source.PlayOneShot(_hit, Mathf.Lerp(_minVolume, _maxVolume, strength));
        }

        private void OnDied(Health health, DamageInfo damage)
        {
            if (_destroyed == null)
            {
                return;
            }

            // 음높이를 되돌려 둔다. 마지막 피격이 낮춰둔 채로 두면 격추음까지
            // 눌린 소리로 나간다.
            _source.pitch = 1f;
            _source.PlayOneShot(_destroyed, _destroyedVolume);
        }
    }
}
