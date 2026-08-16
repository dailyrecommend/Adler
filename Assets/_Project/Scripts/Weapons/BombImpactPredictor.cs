using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 지금 투하하면 폭탄이 어디에 떨어질지 미리 짚어준다.
    /// <para>
    /// 폭탄은 기체 속도를 물려받은 뒤 중력만 받으므로 궤적이 정해져 있다. 문제는 그 궤적이
    /// 지면과 만나는 지점인데, 해석적으로 풀면 지면이 평평하다고 가정해야 한다. 언덕이나
    /// 건물 옥상에 떨어질 폭탄을 땅바닥에 표시하면 조준할 수가 없다.
    /// </para>
    /// <para>
    /// 그래서 궤적을 잘게 나눠 점을 찍고, 연속한 두 점 사이를 훑어 처음 닿는 곳을 찾는다.
    /// 실제 폭탄이 지면을 통과하지 않도록 쓰는 방식과 같으므로, 예측과 실제가 어긋나지 않는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombImpactPredictor : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private StratagemBay _stratagemBay;

        [Tooltip("폭탄이 떨어져 나오는 자리. StratagemBay에 지정한 것과 같아야 한다.")]
        [SerializeField] private Transform _dropPoint;

        [Tooltip("투하 순간의 속도를 넘겨줄 기체. 비워두면 부모에서 찾는다.")]
        [SerializeField] private Rigidbody _carrier;

        [Header("표시")]
        [Tooltip("착탄 지점으로 옮길 오브젝트. 지면에 눕도록 회전도 맞춰준다.")]
        [SerializeField] private Transform _marker;

        [Tooltip("지면에 파묻히지 않도록 띄우는 거리 (m).")]
        [SerializeField] private float _markerSurfaceOffset = 0.05f;

        [Tooltip("장전됐을 때만 표시한다. 끄면 항상 보인다.")]
        [SerializeField] private bool _onlyWhenArmed = true;

        [Header("표식 크기")]
        [Tooltip("거리와 상관없이 화면에서 같은 크기로 보이게 한다.\n" +
                 "끄면 원근에 따라 멀수록 작아진다.")]
        [SerializeField] private bool _constantScreenSize = true;

        [Tooltip("표식이 차지할 화면 높이의 비율. 0.06이면 화면 높이의 6%.")]
        [Range(0.005f, 0.5f)]
        [SerializeField] private float _screenHeightFraction = 0.06f;

        [Tooltip("표식이 가질 수 있는 최소·최대 실제 크기 (m).\n" +
                 "지나치게 가까우면 점이 되고, 멀면 지형을 덮어버린다.")]
        [SerializeField] private Vector2 _worldSizeRange = new Vector2(1f, 60f);

        [Tooltip("비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Camera _camera;

        [Header("계산")]
        [Tooltip("궤적이 닿는 것으로 볼 레이어. 폭탄의 Impact Mask와 맞추면 된다.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("궤적을 나누는 간격(초). 잘게 나눌수록 정확하지만 광선을 많이 쏜다.")]
        [Range(0.01f, 0.25f)]
        [SerializeField] private float _stepSeconds = 0.05f;

        [Tooltip("이 시간 안에 아무 데도 닿지 않으면 포기한다(초).")]
        [Min(1f)]
        [SerializeField] private float _maxFlightTime = 15f;

        [Tooltip("궤적을 훑는 굵기 (m). 0이면 가느다란 광선을 쓴다.")]
        [Min(0f)]
        [SerializeField] private float _castRadius = 0.1f;

        /// <summary>착탄 지점을 찾았는지.</summary>
        public bool HasImpact { get; private set; }

        public Vector3 ImpactPoint { get; private set; }

        public Vector3 ImpactNormal { get; private set; }

        /// <summary>투하부터 착탄까지 걸리는 시간(초).</summary>
        public float TimeToImpact { get; private set; }

        private Vector3 _markerBaseScale = Vector3.one;

        private void Awake()
        {
            if (_carrier == null)
            {
                _carrier = GetComponentInParent<Rigidbody>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_marker != null)
            {
                // 작업하신 비율을 그대로 두고 배율만 곱한다.
                _markerBaseScale = _marker.localScale;
            }

            if (_stratagemBay == null || _dropPoint == null || _carrier == null)
            {
                Debug.LogError($"{nameof(BombImpactPredictor)}: StratagemBay, Drop Point, 기체 중 빠진 것이 있습니다.", this);
                enabled = false;
                return;
            }

            // 표식을 껐다 켜는 방식이라, 이 스크립트가 표식 안에 있으면 자기를 끄게 된다.
            if (_marker != null && transform.IsChildOf(_marker))
            {
                Debug.LogError(
                    $"{nameof(BombImpactPredictor)}: 표식 오브젝트 안에 두면 표식을 끌 때 " +
                    "이 스크립트도 함께 멈춰 다시 켜지지 않습니다. 기체 쪽에 두세요.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            // 기체가 자리를 잡은 뒤에 계산해야 표식이 한 프레임 늦지 않는다.
            HasImpact = _onlyWhenArmed && !_stratagemBay.IsArmed
                ? false
                : Predict();

            if (_marker == null)
            {
                return;
            }

            if (!HasImpact)
            {
                SetMarkerVisible(false);
                return;
            }

            SetMarkerVisible(true);
            _marker.SetPositionAndRotation(
                ImpactPoint + (ImpactNormal * _markerSurfaceOffset),
                Quaternion.FromToRotation(Vector3.up, ImpactNormal));

            ScaleMarker();
        }

        /// <summary>
        /// 거리만큼 표식을 키워 화면에서의 크기를 일정하게 유지한다.
        /// <para>
        /// 착탄 지점은 대개 수십 미터 앞이라, 실제 크기를 고정해두면 멀어질수록 작아져
        /// 정작 조준이 필요한 순간에 보이지 않는다.
        /// </para>
        /// <para>
        /// 화각을 함께 보는 이유는 속도에 따라 화각이 벌어지기 때문이다. 화각만 넓어져도
        /// 화면 속 물체는 작아지므로, 부스터를 켤 때마다 표식이 쪼그라들게 된다.
        /// </para>
        /// </summary>
        private void ScaleMarker()
        {
            if (!_constantScreenSize || _camera == null)
            {
                _marker.localScale = _markerBaseScale;
                return;
            }

            float distance = Vector3.Distance(_camera.transform.position, _marker.position);

            float visibleHeight = _camera.orthographic
                ? _camera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float size = Mathf.Clamp(
                visibleHeight * _screenHeightFraction, _worldSizeRange.x, _worldSizeRange.y);

            _marker.localScale = _markerBaseScale * size;
        }

        private bool Predict()
        {
            Vector3 position = _dropPoint.position;
            Vector3 velocity = _carrier.linearVelocity;
            Vector3 gravity = Physics.gravity;
            float damping = ResolveDamping();
            float dt = _stepSeconds;

            for (float elapsed = 0f; elapsed < _maxFlightTime; elapsed += dt)
            {
                // Unity의 적분 순서를 따라간다. 중력과 감쇠를 먼저 반영한 뒤 움직인다.
                velocity += gravity * dt;
                velocity *= Mathf.Clamp01(1f - (damping * dt));

                Vector3 next = position + (velocity * dt);

                if (SweepSegment(position, next, out RaycastHit hit))
                {
                    ImpactPoint = hit.point;
                    ImpactNormal = hit.normal;
                    TimeToImpact = elapsed + dt;
                    return true;
                }

                position = next;
            }

            return false;
        }

        private bool SweepSegment(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;

            if (distance <= 0.0001f)
            {
                hit = default;
                return false;
            }

            Vector3 direction = travel / distance;

            return _castRadius > 0f
                ? Physics.SphereCast(from, _castRadius, direction, out hit, distance,
                    _groundMask, QueryTriggerInteraction.Ignore)
                : Physics.Raycast(from, direction, out hit, distance,
                    _groundMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 장전된 폭탄 프리팹의 감쇠를 그대로 쓴다. 지금은 0이지만, 나중에 공기 저항을
        /// 넣었을 때 예측만 옛 값으로 남아 어긋나는 일이 없도록 매번 읽는다.
        /// </summary>
        private float ResolveDamping()
        {
            BombDefinition bomb = _stratagemBay.ArmedBomb;
            if (bomb == null || bomb.Prefab == null)
            {
                return 0f;
            }

            return bomb.Prefab.TryGetComponent(out Rigidbody body) ? body.linearDamping : 0f;
        }

        private void SetMarkerVisible(bool visible)
        {
            if (_marker.gameObject.activeSelf != visible)
            {
                _marker.gameObject.SetActive(visible);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasImpact)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_dropPoint.position, ImpactPoint);
            Gizmos.DrawWireSphere(ImpactPoint, 1f);
        }
    }
}
