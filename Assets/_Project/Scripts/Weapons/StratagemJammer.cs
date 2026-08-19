using System;
using System.Collections.Generic;
using Adler.Combat;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 주변의 스트라타젬 요청을 봉인한다.
    /// <para>
    /// 맵의 모든 지점이 같았던 것에 성격을 붙인다. 어디서 폭탄을 부를 수 있는지가 달라지면
    /// 진입 경로가 판단거리가 되고, 재머 자체가 가장 먼저 처리해야 할 표적이 된다.
    /// </para>
    /// <para>
    /// 부수는 데 스트라타젬이 필요하면 안 된다. 폭탄을 봉인해놓고 폭탄으로만 부술 수 있게
    /// 두면 길이 막힌다. 그래서 돌아가는 안테나를 기총이나 미사일로 때리면 잠시 멈추고,
    /// 그 틈에 폭탄을 불러 본체를 부수는 순서로 풀린다 — 두 무기가 한 표적에서 맞물린다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StratagemJammer : MonoBehaviour
    {
        private static readonly List<StratagemJammer> Active = new();

        [Header("범위")]
        [Tooltip("이 안에서는 스트라타젬을 부를 수 없다 (m).\n" +
                 "구체이므로 충분히 높이 날면 벗어난다 — 대신 대공포에 훤히 드러난다.")]
        [Min(1f)]
        [SerializeField] private float _radius = 150f;

        [Header("본체")]
        [Tooltip("이것이 부서지면 봉인이 영구히 풀린다. 비워두면 자기 내구도를 쓴다.")]
        [SerializeField] private Health _body;

        [Header("안테나")]
        [Tooltip("맞으면 봉인이 잠시 멈추는 부분. 자기 내구도를 따로 가져야 한다 —\n" +
                 "없으면 탄이 본체를 때린 것으로 처리된다.")]
        [SerializeField] private Health _antenna;

        [Tooltip("안테나를 맞고 봉인이 멈춰 있는 시간(초).\n" +
                 "폭탄을 부르고 떨구기까지 걸리는 시간보다 넉넉해야 길이 열린다.")]
        [Min(0.1f)]
        [SerializeField] private float _suspendSeconds = 6f;

        [Tooltip("체크하면 안테나를 끝까지 부숴 봉인을 영구히 끌 수 있다.\n" +
                 "끄면 안테나는 부술 수 없는 약점이 되어, 몇 번을 때리든 잠시 멈추기만 한다.\n" +
                 "켜두면 미사일로 안테나만 부수는 것이 최선이 되어 폭탄을 쓸 이유가 사라진다.")]
        [SerializeField] private bool _antennaDestructible;

        [Tooltip("본체가 부서질 때 통째로 치울 오브젝트. 비워두면 이 오브젝트를 끈다.\n" +
                 "Health의 Deactivate On Death는 자기가 붙은 칸만 끄므로, 본체에만 맡기면\n" +
                 "모델과 안테나와 루트의 콜라이더가 그대로 남아 계속 탄을 받는다.")]
        [SerializeField] private GameObject _wreckRoot;

        [Header("연출")]
        [Tooltip("돌아가는 부분. 봉인이 멈추면 함께 멈춰 상태가 눈으로 읽힌다.")]
        [SerializeField] private Transform _spinner;

        [Tooltip("도는 속도 (도/초).")]
        [SerializeField] private float _spinSpeed = 120f;

        [Tooltip("멈췄다 다시 돌기까지 속도가 붙고 빠지는 빠르기.")]
        [Min(0.1f)]
        [SerializeField] private float _spinRamp = 2.5f;

        private float _suspendRemaining;
        private float _spin;
        private bool _antennaDestroyed;
        private bool _restoreAntenna;

        /// <summary>봉인이 멈추거나 다시 걸릴 때. 화면 표시가 구독한다.</summary>
        public event Action<StratagemJammer, bool> OperationalChanged;

        public float Radius => _radius;

        /// <summary>봉인이 남아 있는 시간(초). 걸려 있으면 0.</summary>
        public float SuspendRemaining => _suspendRemaining;

        /// <summary>지금 봉인을 걸고 있는지.</summary>
        public bool IsOperational =>
            isActiveAndEnabled
            && _suspendRemaining <= 0f
            && !_antennaDestroyed
            && (_body == null || _body.IsAlive);

        /// <summary>이 지점이 어느 재머에게든 봉인당하고 있는지.</summary>
        public static bool IsJammed(Vector3 point) => NearestJammer(point) != null;

        /// <summary>
        /// 이 지점을 봉인하고 있는 재머 중 가장 가까운 것. 없으면 null.
        /// 화면에 "어느 쪽으로 가야 벗어나는가"를 보여줄 때 쓴다.
        /// </summary>
        public static StratagemJammer NearestJammer(Vector3 point)
        {
            StratagemJammer nearest = null;
            float closest = float.MaxValue;

            foreach (StratagemJammer jammer in Active)
            {
                if (!jammer.IsOperational)
                {
                    continue;
                }

                float distance = Vector3.Distance(point, jammer.transform.position);
                if (distance <= jammer._radius && distance < closest)
                {
                    closest = distance;
                    nearest = jammer;
                }
            }

            return nearest;
        }

        private void Awake()
        {
            if (_body == null)
            {
                _body = GetComponent<Health>();
            }
        }

        private void OnEnable()
        {
            Active.Add(this);

            if (_antenna != null)
            {
                _antenna.Died += OnAntennaDepleted;
            }

            if (_body != null)
            {
                _body.Died += OnBodyDestroyed;
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);

            if (_antenna != null)
            {
                _antenna.Died -= OnAntennaDepleted;
            }

            if (_body != null)
            {
                _body.Died -= OnBodyDestroyed;
            }
        }

        /// <summary>
        /// 안테나의 내구도가 바닥났을 때.
        /// <para>
        /// 기본값에서는 부서지지 않는다. 부술 수 있게 두면 미사일로 안테나만 노리는 것이
        /// 최선이 되어, 창을 열고 폭탄으로 마무리한다는 순서가 통째로 사라진다. 안테나는
        /// 무력화하는 스위치가 아니라 잠시 재우는 자리다.
        /// </para>
        /// <para>
        /// 그래서 내구도를 되돌리고 멈춤만 건다. 여러 번 때려도 계속 창이 열릴 뿐이다.
        /// </para>
        /// </summary>
        private void OnAntennaDepleted(Health health, DamageInfo damage)
        {
            bool was = IsOperational;

            if (_antennaDestructible)
            {
                _antennaDestroyed = true;
            }
            else
            {
                // 여기서 바로 되살리면 안 된다. Health는 Died를 알린 다음에 오브젝트를 끄므로
                // 지금 켜봐야 곧바로 다시 꺼진다. 한 프레임 미뤄야 살아남는다.
                _restoreAntenna = true;
                _suspendRemaining = _suspendSeconds;
            }

            if (was)
            {
                OperationalChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 본체가 부서졌을 때. 재머를 통째로 치운다.
        /// <para>
        /// 본체의 Health에만 맡길 수 없다. 그쪽은 자기가 붙은 칸만 끄는데, 재머는 모델과
        /// 안테나와 루트의 콜라이더가 저마다 다른 칸에 있어서 껍데기가 그대로 남는다.
        /// 봉인은 풀렸는데 형체는 남아 계속 탄을 받으면, 부순 것인지 아닌지 알 수 없다.
        /// </para>
        /// </summary>
        private void OnBodyDestroyed(Health health, DamageInfo damage)
        {
            OperationalChanged?.Invoke(this, false);

            // 되살릴 차례였던 안테나를 취소한다. 이 뒤에 살아나면 부서진 재머에
            // 약점만 남아 떠 있게 된다.
            _restoreAntenna = false;

            if (_antenna != null)
            {
                _antenna.gameObject.SetActive(false);
            }

            GameObject wreck = _wreckRoot != null ? _wreckRoot : gameObject;
            wreck.SetActive(false);
        }

        private void Update()
        {
            if (_restoreAntenna)
            {
                _restoreAntenna = false;
                _antenna.Revive();
            }

            if (_suspendRemaining > 0f)
            {
                _suspendRemaining -= Time.deltaTime;

                if (_suspendRemaining <= 0f)
                {
                    _suspendRemaining = 0f;

                    if (IsOperational)
                    {
                        OperationalChanged?.Invoke(this, true);
                    }
                }
            }

            Spin();
        }

        /// <summary>
        /// 봉인이 걸려 있을 때만 돈다.
        /// <para>
        /// 멈추는 것 자체가 신호다. 안테나를 맞혔더니 회전이 멎으면 화면에 글자를 띄우지
        /// 않아도 지금이 기회라는 것을 알 수 있다.
        /// </para>
        /// </summary>
        private void Spin()
        {
            if (_spinner == null)
            {
                return;
            }

            float target = IsOperational ? _spinSpeed : 0f;
            _spin = Mathf.Lerp(_spin, target, 1f - Mathf.Exp(-_spinRamp * Time.deltaTime));

            if (Mathf.Abs(_spin) > 0.01f)
            {
                _spinner.Rotate(Vector3.up, _spin * Time.deltaTime, Space.Self);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
