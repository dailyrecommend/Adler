using System;
using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 명중 한 번의 무게.
    /// <para>
    /// 맞혔다는 사실만으로는 어떤 반응이 어울리는지 정할 수 없다. 기총 한 발과
    /// 미사일 한 발은 같은 "명중"이지만, 둘에 같은 연출을 주면 한쪽은 과하고
    /// 다른 쪽은 밋밋해진다.
    /// </para>
    /// </summary>
    public enum ImpactWeight
    {
        /// <summary>기총 한 발이 스친 정도. 자주 일어난다.</summary>
        Light,

        /// <summary>폭발이 표적을 덮었다. 드물고, 한 발이 곧 한 사건이다.</summary>
        Blast,

        /// <summary>표적이 쓰러졌다. 이 게임에서 가장 값진 순간.</summary>
        Kill,

        /// <summary>
        /// 몸으로 들이받았다. 거리가 0이 되어야 일어나는 일이라, 무게가 아니라
        /// 거기까지 파고들었다는 사실이 값이다.
        /// </summary>
        Ram,
    }

    /// <summary>
    /// 명중과 격추를 손맛으로 옮긴다.
    /// <para>
    /// 표식과 달리 사라지는 연출이 아니라 순간적인 반응이다. 시간을 짧게 늦추고,
    /// 카메라를 듣는 쪽에 신호만 보낸다. 카메라를 직접 흔들지 않는 이유는 흔들림을
    /// 하나로 모으는 자리가 이미 따로 있기 때문이다 — 여기서 또 흔들면 부스터
    /// 떨림이나 피격 충격과 따로 놀게 된다.
    /// </para>
    /// <para>
    /// 시간을 늦추는 것은 무거운 명중에만 쓴다. 기총은 분당 천 발이 넘게 나가므로
    /// 맞을 때마다 늦추면 늦춤이 끊이지 않고 이어져, 타격감이 아니라 그냥 느린
    /// 게임이 된다. 흔한 일에 쓰는 순간 특별하다는 신호로서의 값어치가 사라진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitImpact : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("기총")]
        [Tooltip("기총이 맞을 때 연달아 신호를 보내지 않도록 두는 간격(초).\n" +
                 "시간은 어차피 늦추지 않으므로, 이건 카메라가 쉬지 않고 떠는 것을 막는 용도다.")]
        [Min(0.05f)]
        [SerializeField] private float _lightWindow = 0.15f;

        [Header("폭발")]
        [Min(0f)]
        [SerializeField] private float _blastStopSeconds = 0.05f;

        [Range(0.01f, 1f)]
        [SerializeField] private float _blastStopScale = 0.15f;

        [Header("들이받기")]
        [Tooltip("몸으로 들이받았을 때 늦추는 시간(초).\n" +
                 "폭발보다 길게 잡을 만하다 — 거리를 0까지 좁혀야 일어나는 일이라\n" +
                 "자주 나오지 않고, 그만큼 한 번이 사건이다.")]
        [Min(0f)]
        [SerializeField] private float _ramStopSeconds = 0.09f;

        [Range(0.01f, 1f)]
        [SerializeField] private float _ramStopScale = 0.08f;

        [Header("격추")]
        [Min(0f)]
        [SerializeField] private float _killStopSeconds = 0.11f;

        [Range(0.01f, 1f)]
        [SerializeField] private float _killStopScale = 0.05f;

        private WeaponBay _bay;
        private Clock _clock;
        private StratagemBay _stratagemBay;
        private RamAttack _ram;
        private float _releaseAt;
        private ImpactWeight _releasingWeight;
        private bool _releasing;
        private float _nextLightAt;

        /// <summary>명중할 때마다. 카메라 흔들림이 구독한다.</summary>
        public event Action<ImpactWeight> Impact;

        /// <summary>
        /// 늦춰뒀던 시간이 풀리는 순간. 시간이 멎지 않는 가벼운 명중은 오지 않는다.
        /// <para>
        /// 늦춤의 시작과 끝은 다른 사건이다. 숨을 들이켜는 소리는 멎는 순간에 나야 하고
        /// 내리치는 소리는 풀리는 순간에 나야, 멎어 있던 그 짧은 시간이 뜸이 된다.
        /// 둘 다 시작에 몰아 내면 그냥 겹친 소리 하나가 된다.
        /// </para>
        /// </summary>
        public event Action<ImpactWeight> Released;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _clock = TimeScale.For(this);
            _bay = _aircraft != null ? _aircraft.Weapons : null;
            _stratagemBay = _aircraft != null ? _aircraft.Stratagems : null;
            _ram = _aircraft != null ? _aircraft.Ram : null;

            if (_bay == null)
            {
                Debug.LogError($"{nameof(HitImpact)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            foreach (AircraftWeapon weapon in _bay.Weapons)
            {
                weapon.Hit += OnHit;

                if (weapon is MissileLauncher launcher)
                {
                    launcher.Detonated += OnMissileDetonated;
                }
            }

            if (_stratagemBay != null)
            {
                _stratagemBay.Detonated += OnDetonated;
            }

            if (_ram != null)
            {
                _ram.Rammed += OnRammed;
            }
        }

        private void OnDisable()
        {
            foreach (AircraftWeapon weapon in _bay.Weapons)
            {
                weapon.Hit -= OnHit;

                if (weapon is MissileLauncher launcher)
                {
                    launcher.Detonated -= OnMissileDetonated;
                }
            }

            if (_stratagemBay != null)
            {
                _stratagemBay.Detonated -= OnDetonated;
            }

            if (_ram != null)
            {
                _ram.Rammed -= OnRammed;
            }
        }

        /// <summary>
        /// 직격은 쓰러뜨렸을 때만 무겁다. 기총으로 깎아내는 동안은 표식과 조준점이
        /// 이미 맞고 있다고 알려주고 있으므로, 시간까지 늦출 이유가 없다.
        /// </summary>
        private void OnHit(RaycastHit hit, IDamageable damaged, DamageResult result)
        {
            if (damaged == null || !result.Landed)
            {
                return;
            }

            React(result.Killed ? ImpactWeight.Kill : ImpactWeight.Light);
        }

        /// <summary>
        /// 들이받은 것은 쓰러뜨렸어도 들이받기로 남긴다. 격추로 바꿔 부르면 기총으로
        /// 마무리한 것과 같은 연출이 나가서, 몸으로 밀어붙였다는 사실이 지워진다.
        /// </summary>
        private void OnRammed(Collision collision, DamageResult result) => React(ImpactWeight.Ram);

        private void OnDetonated(BombDefinition bomb, BlastReport report) => ReactToBlast(report);

        private void OnMissileDetonated(MissileDefinition missile, BlastReport report) => ReactToBlast(report);

        private void ReactToBlast(BlastReport report)
        {
            if (report.Damaged == 0)
            {
                return;
            }

            React(report.Killed > 0 ? ImpactWeight.Kill : ImpactWeight.Blast);
        }

        /// <summary>
        /// 무거운 쪽은 간격을 두지 않는다. 드물게 일어나는 데다,
        /// 시계가 이미 늦춰지고 있는 동안 짧은 요청을 무시하므로
        /// 연달아 터져도 늦춤이 잘게 끊기지 않는다.
        /// </summary>
        private void React(ImpactWeight weight)
        {
            if (weight == ImpactWeight.Light)
            {
                if (_clock.Now < _nextLightAt)
                {
                    return;
                }

                _nextLightAt = _clock.Now + _lightWindow;
            }
            else
            {
                float seconds = StopSecondsFor(weight);

                Clock.World.Hold(seconds, StopScaleFor(weight));
                Schedule(weight, seconds);
            }

            Impact?.Invoke(weight);
        }

        /// <summary>
        /// 언제 풀릴지 적어둔다.
        /// <para>
        /// 바깥 시간으로 잰다. 늦추는 그 배율로 재면 늦출수록 오래 걸려서 풀림이 한참
        /// 뒤에 오는데, 시계가 늦춤을 세는 방식도 바깥 시간이라 그쪽과 어긋나 버린다.
        /// </para>
        /// <para>
        /// 더 긴 요청만 갈아탄다. 시계가 짧은 요청을 무시하므로 여기서도 무시해야,
        /// 무거운 한 방이 도는 중에 잔챙이가 끼어들어 풀림을 앞당기지 않는다.
        /// </para>
        /// </summary>
        private void Schedule(ImpactWeight weight, float seconds)
        {
            float at = Time.unscaledTime + seconds;

            if (_releasing && at <= _releaseAt)
            {
                return;
            }

            _releasing = true;
            _releaseAt = at;
            _releasingWeight = weight;
        }

        private void Update()
        {
            if (!_releasing || Time.unscaledTime < _releaseAt)
            {
                return;
            }

            _releasing = false;
            Released?.Invoke(_releasingWeight);
        }

        private float StopSecondsFor(ImpactWeight weight) => weight switch
        {
            ImpactWeight.Kill => _killStopSeconds,
            ImpactWeight.Ram => _ramStopSeconds,
            _ => _blastStopSeconds,
        };

        private float StopScaleFor(ImpactWeight weight) => weight switch
        {
            ImpactWeight.Kill => _killStopScale,
            ImpactWeight.Ram => _ramStopScale,
            _ => _blastStopScale,
        };
    }
}
