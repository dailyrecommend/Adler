using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adler.Flight
{
    /// <summary>
    /// 일정 고도를 넘으면 얼어붙는다.
    /// <para>
    /// 하늘에 벽을 세우는 대신 대가를 붙인다. 벽에 막히면 게임이 자기를 붙잡는 것으로
    /// 느껴지지만, 올라갈 수는 있는데 얼어붙는다면 얼마나 버틸지가 판단거리가 된다.
    /// </para>
    /// <para>
    /// 재머처럼 위로 도망칠 수 있는 표적을 다룰 때 특히 그렇다 — 하늘로 빠지는 길이
    /// 막히는 것이 아니라 비싸지는 것이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltitudeFreeze : MonoBehaviour, IDebuffSource
    {
        [Tooltip("이 높이를 넘으면 얼어붙기 시작한다 (m).")]
        [SerializeField] private float _freezeAltitude = 400f;

        [Tooltip("풀리려면 기준선보다 이만큼 더 내려와야 한다 (m).\n" +
                 "0으로 두면 경계선에서 오르내릴 때마다 깜빡인다 — 기수를 조금만 들었다\n" +
                 "놔도 켜졌다 꺼졌다 하므로, 화면이 고장 난 것처럼 보인다.")]
        [Min(0f)]
        [SerializeField] private float _thawMargin = 25f;

        [Tooltip("얼어붙었을 때 목록에 올릴 것. FROZEN으로 만들어 둔 에셋.")]
        [SerializeField] private DebuffDefinition _definition;

        [Header("예고")]
        [Tooltip("이 높이부터 서리가 끼기 시작한다 (m). 기준선보다 낮아야 한다.\n" +
                 "여기서 0, 기준선에서 1까지 차오른다.\n" +
                 "예고가 없으면 아무 신호 없이 조종을 잃어서 버그로 읽힌다.")]
        [SerializeField] private float _chillAltitude = 175f;

        [Tooltip("서리 연출을 담은 Volume. Is Global을 켜고 Weight는 0으로 둘 것.")]
        [SerializeField] private Volume _volume;

        [Tooltip("녹은 뒤 서리가 걷히는 속도. 올라갈 때는 높이를 그대로 따라가고,\n" +
                 "풀릴 때만 이 속도로 빠진다 — 녹는 높이가 예고 구간보다 훨씬 아래라\n" +
                 "그대로 두면 화면이 한 프레임에 툭 걷힌다.")]
        [Min(0.1f)]
        [SerializeField] private float _fadeOutSpeed = 2f;

        private bool _frozen;

        /// <summary>지금 얼어붙어 있는지.</summary>
        public bool IsFrozen => _frozen;

        /// <summary>
        /// 서리가 낀 정도. 예고 고도에서 0, 기준선에서 1.
        /// <para>
        /// 얼어붙은 동안에는 계속 1이다. 녹는 높이가 예고 구간보다 한참 아래라, 높이만
        /// 따라가게 두면 얼어 있는 채로 서리가 걷혀 이미 풀린 것처럼 보인다.
        /// </para>
        /// </summary>
        public float Severity
        {
            get
            {
                if (_frozen)
                {
                    return 1f;
                }

                float span = _freezeAltitude - _chillAltitude;
                return span <= 0f
                    ? 0f
                    : Mathf.Clamp01((transform.position.y - _chillAltitude) / span);
            }
        }

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            if (_definition == null)
            {
                Debug.LogWarning($"{nameof(AltitudeFreeze)}: 디버프 정의가 비어 있어 목록에 뜨지 않습니다.", this);
            }

            if (_chillAltitude >= _freezeAltitude)
            {
                Debug.LogWarning(
                    $"{nameof(AltitudeFreeze)}: 예고 고도가 기준선보다 낮아야 서리가 차오릅니다.", this);
            }

            if (_volume != null)
            {
                _volume.weight = 0f;
            }
        }

        private void OnDisable()
        {
            if (_volume != null)
            {
                _volume.weight = 0f;
            }
        }

        /// <summary>
        /// 켜지는 높이와 꺼지는 높이를 다르게 둔다.
        /// <para>
        /// 하나로 두면 그 선에 걸친 채로 나는 동안 매 프레임 켜졌다 꺼진다. 기수를
        /// 조금만 흔들어도 그렇게 되므로, 걸린 것인지 아닌지 읽을 수가 없다.
        /// </para>
        /// </summary>
        private void Update()
        {
            float altitude = transform.position.y;

            _frozen = _frozen
                ? altitude > _freezeAltitude - _thawMargin
                : altitude >= _freezeAltitude;

            ApplyVolume();
        }

        /// <summary>
        /// 서리를 화면에 얹는다.
        /// <para>
        /// 올라갈 때는 높이를 그대로 따라간다. 부드럽게 만들면 고도와 화면이 어긋나서,
        /// 얼마나 가까운지를 화면으로 가늠할 수 없게 된다.
        /// </para>
        /// <para>
        /// 걷힐 때만 서서히 뺀다. 녹는 높이가 예고 구간보다 훨씬 아래라 그대로 두면
        /// 한 프레임에 툭 사라진다.
        /// </para>
        /// </summary>
        private void ApplyVolume()
        {
            if (_volume == null)
            {
                return;
            }

            float target = Severity;

            _volume.weight = target >= _volume.weight
                ? target
                : Mathf.Lerp(_volume.weight, target, 1f - Mathf.Exp(-_fadeOutSpeed * _clock.Delta));
        }

        void IDebuffSource.CollectDebuffs(List<DebuffDefinition> into)
        {
            if (_frozen && _definition != null)
            {
                into.Add(_definition);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var size = new Vector3(600f, 0f, 600f);
            float x = transform.position.x;
            float z = transform.position.z;

            // 얼어붙는 선
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(x, _freezeAltitude, z), size);

            // 서리가 끼기 시작하는 선
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.3f);
            Gizmos.DrawWireCube(new Vector3(x, _chillAltitude, z), size);

            // 녹는 선
            Gizmos.color = new Color(1f, 0.8f, 0.4f, 0.3f);
            Gizmos.DrawWireCube(new Vector3(x, _freezeAltitude - _thawMargin, z), size);
        }
    }
}
