using Adler.Core;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 무기 하나의 목소리. 옆에 붙은 무기의 발사를 듣고, 꽂힌 성능 에셋의 소리를 낸다.
    /// <para>
    /// 무기의 몸 프리팹 안에 산다. 그래서 어떤 소리를 낼지는 에셋이 정하고(몸을 나눠
    /// 쓰는 LM 계열이 서로 다른 소리를 낼 수 있다), 언제 나고 죽을지는 몸이 정한다 —
    /// 장비를 갈아입으면 목소리도 몸과 함께 사라지므로 재구독 같은 뒷단속이 없다.
    /// </para>
    /// <para>
    /// 무기의 종류를 모른다. 기총인지 발사기인지 묻지 않고 루프가 있으면 루프,
    /// 없으면 단발이다 — 종류를 물으면 무기를 늘릴 때마다 여기를 열게 된다.
    /// </para>
    /// <para>
    /// 소스는 스스로 만든다. 단발과 루프 두 개가 필요한 사정은 이 클래스의
    /// 속사정이라, 프리팹에서 이어줄 것이 없어야 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponVoice : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("목소리를 낼 무기. 비워두면 이 오브젝트와 부모에서 찾는다.")]
        [SerializeField] private AircraftWeapon _weapon;

        [Header("루프 여닫기")]
        [Tooltip("한 발 나간 뒤 루프가 살아 있는 시간(초). 발사 간격보다 길어야 안 끊긴다.")]
        [Min(0.02f)]
        [SerializeField] private float _loopHold = 0.12f;

        [Tooltip("손을 뗐을 때 루프가 잦아드는 속도. 뚝 끊으면 파형이 잘려 딸깍 소리가 난다.")]
        [Min(1f)]
        [SerializeField] private float _loopFadeOut = 25f;

        [Header("단발 솎아내기")]
        [Tooltip("단발 소리 사이의 최소 간격(초).\n" +
                 "루프 없이 발마다 울리는 무기가 너무 빠르면 동시 재생 한도에 잘려 나간다.")]
        [Min(0f)]
        [SerializeField] private float _minInterval = 0.08f;

        private AudioSource _oneShot;
        private AudioSource _loop;

        private Clock _clock;
        private float _firingUntil;
        private float _nextOneShotAt;

        private void Awake()
        {
            _clock = TimeScale.For(this);

            if (_weapon == null)
            {
                _weapon = GetComponentInParent<AircraftWeapon>();
            }

            if (_weapon == null)
            {
                Debug.LogError($"{nameof(WeaponVoice)}: 목소리를 낼 무기를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _oneShot = Build(loop: false);
            _loop = Build(loop: true);
        }

        private AudioSource Build(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;

            return source;
        }

        private void OnEnable()
        {
            if (_weapon != null)
            {
                _weapon.Fired += OnFired;
            }
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.Fired -= OnFired;
            }
        }

        private void OnFired(AircraftWeapon weapon, Vector3 origin, Vector3 direction)
        {
            WeaponDefinition definition = weapon.Definition;

            if (definition == null)
            {
                return;
            }

            if (definition.FireLoop != null)
            {
                // 쏘고 있다는 표시만 남긴다. 실제 소리는 계속 돌고 있는 루프가 낸다.
                _firingUntil = _clock.Now + _loopHold;
                return;
            }

            if (definition.FireSound == null || _clock.Now < _nextOneShotAt)
            {
                return;
            }

            _nextOneShotAt = _clock.Now + _minInterval;

            _oneShot.pitch = 1f + Random.Range(-definition.PitchJitter, definition.PitchJitter);
            _oneShot.PlayOneShot(definition.FireSound, definition.SoundVolume);
        }

        private void Update()
        {
            WeaponDefinition definition = _weapon.Definition;

            if (definition == null || definition.FireLoop == null)
            {
                return;
            }

            bool firing = _clock.Now < _firingUntil;

            // 매 프레임 맞춘다. 시작할 때 한 번만 넣으면 실행 중에 에셋을 돌려도
            // 다음 사격까지 안 들려서, 조율이 한 박자씩 늦는다.
            _loop.pitch = definition.LoopPitch;

            // 시작은 즉시, 끝은 서서히. 쏘는 순간과 소리가 어긋나면 안 되지만,
            // 손을 뗄 때 뚝 끊으면 파형이 잘려 딸깍 소리가 난다.
            _loop.volume = firing
                ? definition.SoundVolume
                : Mathf.Lerp(_loop.volume, 0f, 1f - Mathf.Exp(-_loopFadeOut * _clock.Delta));

            if (_loop.volume > 0.001f)
            {
                if (!_loop.isPlaying)
                {
                    _loop.clip = definition.FireLoop;
                    _loop.Play();
                }
            }
            else if (_loop.isPlaying)
            {
                _loop.Stop();
            }
        }
    }
}
