using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 무언가가 맞고 부서지는 소리. <see cref="Health"/>이 있는 것이면 무엇이든 붙는다.
    /// <para>
    /// 플레이어 쪽 피격음과 나눠 둔다. 그쪽은 화면 흔들림과 박자를 맞춰야 해서
    /// 반응을 묶어주는 층을 거치지만, 맞는 쪽이 남이면 묶을 이유가 없다 — 오히려
    /// 한 발 한 발이 들려야 명중하고 있다는 것이 손에 전해진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [Tooltip("비워두면 이 오브젝트에서 찾는다.")]
        [SerializeField] private Health _health;

        [Header("소리")]
        [Tooltip("소리가 나올 소스. Loop은 끄고 Play On Awake도 끌 것.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("맞았을 때.")]
        [SerializeField] private AudioClip _hit;

        [Range(0f, 1f)]
        [SerializeField] private float _hitVolume = 0.6f;

        [Tooltip("맞은 소리를 낼 수 있는 최소 간격(초).\n\n" +
                 "기총은 분당 천 발이 넘게 나간다. 한 발마다 울리면 소리가 겹쳐 쌓이다\n" +
                 "채널이 동나서 오히려 아무것도 들리지 않는다.")]
        [Min(0f)]
        [SerializeField] private float _minInterval = 0.06f;

        [Tooltip("맞을 때마다 음높이를 이만큼 흔든다.\n" +
                 "같은 소리가 연달아 나면 기계음처럼 들려서 맞고 있다는 느낌이 사라진다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchJitter = 0.12f;

        [Header("격추")]
        [Tooltip("부서졌을 때. 비워둬도 된다.")]
        [SerializeField] private AudioClip _destroyed;

        [Range(0f, 1f)]
        [SerializeField] private float _destroyedVolume = 1f;

        private Clock _clock;
        private float _nextHitAt;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_health == null || _source == null)
            {
                Debug.LogError($"{nameof(HealthAudio)}: {nameof(Health)} 또는 Audio Source가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _source.loop = false;
            _source.playOnAwake = false;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void OnDamaged(Health health, DamageInfo damage)
        {
            if (_hit == null || _clock.Now < _nextHitAt)
            {
                return;
            }

            _nextHitAt = _clock.Now + _minInterval;

            _source.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            _source.PlayOneShot(_hit, _hitVolume);
        }

        /// <summary>
        /// 격추음은 이 오브젝트에서 내지 않는다.
        /// <para>
        /// <see cref="Health"/>은 죽었다고 알린 <b>뒤에</b> 오브젝트를 끈다. 여기서
        /// 재생하면 같은 프레임에 소스가 함께 꺼져서, 소리가 나기도 전에 잘린다.
        /// 원인을 짐작하기 어려운 종류의 침묵이다.
        /// </para>
        /// <para>
        /// 그래서 설정을 옮겨 담은 소스를 따로 세워 그 자리에 남겨 둔다. 재생이 끝날
        /// 만큼만 살렸다가 스스로 사라진다.
        /// </para>
        /// </summary>
        private void OnDied(Health health, DamageInfo damage)
        {
            if (_destroyed == null)
            {
                return;
            }

            GameObject carrier = new($"{name} Death Audio");
            carrier.transform.position = transform.position;

            AudioSource source = carrier.AddComponent<AudioSource>();

            // 인스펙터에서 맞춰둔 울림 설정을 그대로 따라간다. 새로 만든 소스는
            // 기본값이 3D라, 옮겨 담지 않으면 격추음만 다른 규칙으로 들린다.
            source.outputAudioMixerGroup = _source.outputAudioMixerGroup;
            source.spatialBlend = _source.spatialBlend;
            source.rolloffMode = _source.rolloffMode;
            source.minDistance = _source.minDistance;
            source.maxDistance = _source.maxDistance;
            source.dopplerLevel = _source.dopplerLevel;
            source.priority = _source.priority;

            source.clip = _destroyed;
            source.volume = _destroyedVolume;
            source.Play();

            Destroy(carrier, _destroyed.length + 0.1f);
        }
    }
}
