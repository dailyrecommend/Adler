using System.Collections.Generic;
using Adler.Flight;
using Adler.Core;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Audio
{
    /// <summary>
    /// 무기 발사음. 지금 든 무기에 맞는 소리를 낸다.
    /// <para>
    /// 무기가 아니라 여기서 소리를 맡는 이유는, 무기가 여러 개고 실시간으로 갈아
    /// 끼우기 때문이다. 무기마다 소스를 하나씩 두면 기체에 오디오 소스가 무기 수만큼
    /// 붙고, 갈아 끼우는 순간 앞 무기의 소리가 남는다.
    /// </para>
    /// <para>
    /// 연사가 빠른 무기는 발마다 울리지 않고 <b>누르는 동안 루프를 튼다</b>. 분당
    /// 1500발이면 초당 스물다섯인데, 한 발이 0.2초짜리라면 늘 다섯 개가 겹쳐 있게 된다.
    /// 소리가 뭉개지는 것은 물론이고 동시 재생 한도에 부딪히면 새 소리가 잘려 나가서,
    /// 계속 쏠수록 오히려 조용해진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAudio : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("연사 무기")]
        [Tooltip("기총을 쏘는 동안 울릴 소스. 이어지는 사격음을 넣고 Loop을 켤 것.\n" +
                 "Volume은 0으로 두면 된다 — 여기서 여닫는다.")]
        [SerializeField] private AudioSource _gunLoop;

        [Header("단발 무기")]
        [Tooltip("미사일처럼 가끔 나가는 것들이 쓸 소스. Loop은 끌 것.")]
        [SerializeField] private AudioSource _source;

        [SerializeField] private AudioClip _missile;

        [Tooltip("루프 소스를 못 쓸 때 발마다 울릴 기총 소리.\n" +
                 "Gun Loop이 비어 있을 때만 쓰이며, 아래 최소 간격으로 솎아낸다.")]
        [SerializeField] private AudioClip _gun;

        [Header("변화")]
        [Tooltip("단발 소리의 음높이를 흔드는 폭. 같은 소리가 반복되는 것을 덜어준다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchJitter = 0.08f;

        [Tooltip("발마다 울릴 때의 최소 간격(초).\n" +
                 "연사 속도를 그대로 따라가면 소리가 겹쳐 쌓이다 잘려 나간다.\n" +
                 "0.08이면 초당 열두 번까지만 울리고, 그래도 이어지는 사격으로 들린다.")]
        [Min(0f)]
        [SerializeField] private float _minInterval = 0.08f;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.6f;

        [Header("루프 여닫기")]
        [Tooltip("마지막 발사 뒤 이만큼은 루프를 유지한다(초).\n" +
                 "발사 간격보다 넉넉해야 쏘는 도중에 끊기지 않는다.")]
        [Min(0.02f)]
        [SerializeField] private float _loopHold = 0.12f;

        [Tooltip("루프가 잦아드는 속도. 뚝 끊으면 딸깍 소리가 난다.")]
        [Min(1f)]
        [SerializeField] private float _loopFadeOut = 25f;

        private WeaponBay _weapons;
        private Clock _clock;
        private float _firingUntil;
        private float _nextOneShotAt;

        // 지금 구독해 둔 무기들. 갈아입을 때 이 목록으로 놓는다.
        private readonly List<AircraftWeapon> _listening = new();

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _weapons = _aircraft != null ? _aircraft.Weapons : null;

            if (_weapons == null)
            {
                Debug.LogError($"{nameof(WeaponAudio)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_source != null)
            {
                _source.loop = false;
                _source.playOnAwake = false;
            }

            if (_gunLoop != null)
            {
                _gunLoop.loop = true;
                _gunLoop.playOnAwake = false;
                _gunLoop.volume = 0f;
            }
        }

        /// <summary>
        /// 실려 있는 무기 전부를 구독한다.
        /// <para>
        /// Start에서 처음 붙는다. 무기의 몸은 <see cref="WeaponBay"/>가 자기 Awake에서
        /// 찍어내는데, 그것이 이 컴포넌트의 OnEnable보다 늦게 돌 수 있다.
        /// </para>
        /// <para>
        /// 장비를 갈아입으면 따라간다. 무기가 지워지고 새로 찍히므로, 붙잡았던 것을
        /// 놓고 지금 실린 것에 다시 붙는다 — 놓는 쪽은 붙었던 목록으로 한다. 무기고의
        /// 지금 목록으로 놓으면 이미 지워진 옛 무기는 영영 못 놓는다.
        /// </para>
        /// </summary>
        private void Start()
        {
            if (_weapons == null)
            {
                return;
            }

            _weapons.Rearmed += OnRearmed;
            Attach();
        }

        private void OnDestroy()
        {
            if (_weapons != null)
            {
                _weapons.Rearmed -= OnRearmed;
            }

            Detach();
        }

        private void OnRearmed()
        {
            Detach();
            Attach();
        }

        private void Attach()
        {
            foreach (AircraftWeapon weapon in _weapons.Weapons)
            {
                if (weapon != null)
                {
                    weapon.Fired += OnFired;
                    _listening.Add(weapon);
                }
            }
        }

        /// <summary>
        /// 참조 비교로 놓는다. 지워진 무기는 null인 척을 해서, 보통 비교로 거르면
        /// 놓을 기회 자체가 안 온다.
        /// </summary>
        private void Detach()
        {
            foreach (AircraftWeapon weapon in _listening)
            {
                if (!ReferenceEquals(weapon, null))
                {
                    weapon.Fired -= OnFired;
                }
            }

            _listening.Clear();
        }

        /// <summary>
        /// 한 발 나갔다. 연사 무기면 루프를 살려두고, 단발 무기면 그 자리에서 울린다.
        /// <para>
        /// 무엇이 쐈는지는 신호가 들고 온다. 무기고에게 되물으면 두 자리가 동시에
        /// 나가는 순간 답이 하나뿐이라, 기총을 갈기는 동안 나간 미사일이 기총 소리를 낸다.
        /// </para>
        /// </summary>
        private void OnFired(AircraftWeapon weapon, Vector3 origin, Vector3 direction)
        {
            if (weapon is MissileLauncher)
            {
                PlayOneShot(_missile, ignoreInterval: true);
                return;
            }

            if (_gunLoop != null)
            {
                // 쏘고 있다는 표시만 남긴다. 실제 소리는 계속 돌고 있는 루프가 낸다.
                _firingUntil = _clock.Now + _loopHold;
                return;
            }

            PlayOneShot(_gun, ignoreInterval: false);
        }

        /// <summary>
        /// 발마다 울리는 경우. 너무 자주 오면 솎아낸다.
        /// <para>
        /// 솎아내지 않으면 초당 스물다섯 개가 쌓이다 동시 재생 한도에 걸려 잘려 나간다.
        /// 열두 번쯤으로 줄여도 사람 귀에는 여전히 이어지는 사격으로 들린다.
        /// </para>
        /// </summary>
        private void PlayOneShot(AudioClip clip, bool ignoreInterval)
        {
            if (clip == null || _source == null)
            {
                return;
            }

            if (!ignoreInterval && _clock.Now < _nextOneShotAt)
            {
                return;
            }

            _nextOneShotAt = _clock.Now + _minInterval;

            _source.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            _source.PlayOneShot(clip, _volume);
        }

        private void Update()
        {
            if (_gunLoop == null)
            {
                return;
            }

            bool firing = _clock.Now < _firingUntil;

            // 시작은 즉시, 끝은 서서히. 쏘는 순간과 소리가 어긋나면 안 되지만,
            // 손을 뗄 때 뚝 끊으면 파형이 잘려 딸깍 소리가 난다.
            _gunLoop.volume = firing
                ? _volume
                : Mathf.Lerp(_gunLoop.volume, 0f, 1f - Mathf.Exp(-_loopFadeOut * _clock.Delta));

            if (_gunLoop.volume > 0.001f)
            {
                if (!_gunLoop.isPlaying && _gunLoop.clip != null)
                {
                    _gunLoop.Play();
                }
            }
            else if (_gunLoop.isPlaying)
            {
                _gunLoop.Stop();
            }
        }
    }
}
