using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;

namespace Adler.CameraRig
{
    /// <summary>
    /// 정해둔 디버프가 걸려 있는 동안 안개를 짙게 만든다.
    /// <para>
    /// 안개만 Volume으로 처리할 수 없어서 따로 둔다. URP의 안개는 Volume 오버라이드가
    /// 아니라 씬에 하나뿐인 전역 설정이라, 구역마다 다르게 하려면 이렇게 값을 직접
    /// 움직이는 수밖에 없다.
    /// </para>
    /// <para>
    /// 뿌옇게 만드는 일을 화면 효과로 흉내 내지 않는 이유는 거리 때문이다. 안개는 멀수록
    /// 짙어지는 것이라 먼 표적만 사라지는데, 화면 전체를 하얗게 덮으면 코앞의 지형까지
    /// 함께 흐려져 조종이 불가능해진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebuffFog : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("이 디버프가 걸려 있는 동안 안개가 짙어진다.")]
        [SerializeField] private DebuffDefinition _debuff;

        [Header("짙어졌을 때")]
        [Tooltip("안개 색. 하늘색과 비슷하게 맞춰야 한다 —\n" +
                 "하늘에는 안개가 걸리지 않아서, 색이 다르면 뿌연 지면 위로 맑은 하늘이 뜬다.")]
        [SerializeField] private Color _fogColor = new Color(0.72f, 0.75f, 0.78f, 1f);

        [Tooltip("짙기. 값이 클수록 가까이서 막힌다.\n" +
                 "0.02면 100m쯤, 0.033이면 60m쯤에서 아무것도 안 보인다.")]
        [Min(0f)]
        [SerializeField] private float _fogDensity = 0.033f;

        [Header("전환")]
        [Tooltip("짙어지는 속도. 느릴수록 들어왔다는 것이 읽힌다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampUpSpeed = 1.5f;

        [Tooltip("걷히는 속도. 빠져나온 것은 빨리 알려주는 편이 낫다.")]
        [Min(0.1f)]
        [SerializeField] private float _rampDownSpeed = 3f;

        private AircraftDebuffs _debuffs;

        // 씬에 원래 있던 설정. 구역을 나오면 여기로 돌아간다.
        private bool _baseEnabled;
        private Color _baseColor;
        private float _baseDensity;
        private FogMode _baseMode;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _debuffs = _aircraft != null ? _aircraft.Debuffs : null;

            if (_debuffs == null || _debuff == null)
            {
                Debug.LogError($"{nameof(DebuffFog)}: 디버프 목록 또는 정의가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _baseEnabled = RenderSettings.fog;
            _baseColor = RenderSettings.fogColor;
            _baseDensity = RenderSettings.fogDensity;
            _baseMode = RenderSettings.fogMode;
        }

        /// <summary>
        /// 씬 설정을 되돌린다.
        /// <para>
        /// 안개는 씬에 하나뿐인 값이라 플레이를 멈춰도 에디터에 그대로 남는다.
        /// 되돌리지 않으면 다음에 씬을 열 때 짙은 안개로 시작한다.
        /// </para>
        /// </summary>
        private void OnDisable()
        {
            RenderSettings.fog = _baseEnabled;
            RenderSettings.fogColor = _baseColor;
            RenderSettings.fogDensity = _baseDensity;
            RenderSettings.fogMode = _baseMode;
        }

        private void Update()
        {
            bool active = _debuffs.IsActive(_debuff);

            float targetDensity = active ? _fogDensity : (_baseEnabled ? _baseDensity : 0f);
            Color targetColor = active ? _fogColor : _baseColor;
            float speed = active ? _rampUpSpeed : _rampDownSpeed;

            float t = 1f - Mathf.Exp(-speed * _clock.Delta);

            float density = Mathf.Lerp(RenderSettings.fogDensity, targetDensity, t);
            Color color = Color.Lerp(RenderSettings.fogColor, targetColor, t);

            // 제곱 감쇠라야 가까운 곳은 멀쩡하고 먼 곳만 사라진다. 선형으로 두면
            // 코앞부터 균일하게 흐려져서 뿌옇다기보다 화면이 바랜 것처럼 보인다.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;

            // 짙기가 0에 가까우면 아예 끈다. 켜둔 채로 두면 모든 셰이더가 안개 계산을
            // 계속 돌린다.
            RenderSettings.fog = density > 0.0005f;
        }
    }
}
