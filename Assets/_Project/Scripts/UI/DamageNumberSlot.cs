using TMPro;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 떠오르는 피해량 하나.
    /// <para>
    /// 맞은 자리 둘레의 원 안에 흩어져 나타난다. 기총은 초당 열다섯 발을 쏟아내므로
    /// 전부 한 점에서 솟으면 덩어리로 뭉쳐 아무것도 읽히지 않는다.
    /// </para>
    /// <para>
    /// 그렇다고 원 안을 아무 데나 고르지는 않는다. 반지름 60px 원에 아홉 개를 무작위로
    /// 뿌리면 겹치는 쌍이 평균 스무 쌍쯤 나온다 — 두 숫자가 겹치는 중심 거리 범위가
    /// 원 넓이의 절반이 넘기 때문이다. 겹침을 한 쌍으로 낮추려면 반지름을 270px까지
    /// 키워야 하는데, 그러면 적 하나가 화면 사분의 일을 차지한다.
    /// </para>
    /// <para>
    /// 대신 원을 자리로 나눠 쓴다. 띄우는 쪽이 비어 있는 자리 번호를 주고, 이쪽은 그
    /// 번호가 가리키는 곳 <em>둘레에서만</em> 무작위로 흔든다. 자리는 겹치지 않으니
    /// 뭉치지 않고, 흔들림이 있으니 같은 연사가 두 번 똑같이 보이지 않는다.
    /// </para>
    /// <para>
    /// 스스로 시간을 재지 않는다. 띄우는 쪽이 자기 시계로 밀어준다. 그래야 히트스톱이
    /// 걸린 동안 숫자도 함께 멈춘다 — 화면이 멈췄는데 숫자만 흘러가면 그 순간이
    /// 멈춘 것으로 읽히지 않는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSlot : MonoBehaviour
    {
        [Header("조각")]
        [Tooltip("숫자가 들어갈 글상자. 비워두면 자식에서 찾는다.")]
        [SerializeField] private TMP_Text _label;

        [Header("표기")]
        [Tooltip("소수점 아래 자릿수. 0이면 정수로만 보인다.\n" +
                 "전투 중에 읽는 숫자라 자리가 늘수록 읽는 데 시간이 걸린다.")]
        [Range(0, 2)]
        [SerializeField] private int _decimals;

        [Header("색")]
        [Tooltip("보통 피해.")]
        [SerializeField] private Color _normalColor = new(1f, 0.95f, 0.8f, 1f);

        [Tooltip("이 한 방으로 끝냈을 때.\n" +
                 "격추는 숫자보다 먼저 눈에 들어와야 해서 색으로 가른다.")]
        [SerializeField] private Color _killColor = new(1f, 0.35f, 0.25f, 1f);

        [Header("수명")]
        [Tooltip("나타나서 사라지기까지의 시간(초).\n\n" +
                 "겹침을 푸는 가장 센 손잡이다. 초당 열다섯 발을 쏘는데 1초를 살면\n" +
                 "한 표적 위에 늘 열다섯 개가 떠 있다 — 어떻게 흩어도 빽빽하다.")]
        [Min(0.1f)]
        [SerializeField] private float _lifetime = 0.6f;

        [Tooltip("수명의 어디쯤부터 흐려지기 시작할지(0~1).\n" +
                 "처음부터 흐려지면 정작 읽어야 할 때 옅다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _fadeFrom = 0.45f;

        [Header("흩뿌리기")]
        [Tooltip("숫자가 나타날 수 있는 원의 반지름(px).\n" +
                 "키우면 넉넉해지지만 어느 적이 맞은 것인지 흐려진다.")]
        [Min(0f)]
        [SerializeField] private float _scatterRadius = 72f;

        [Tooltip("원을 몇 자리로 나눌지.\n\n" +
                 "한 표적 위에 동시에 떠 있을 법한 개수로 잡는다 — 초당 발수 × 수명이다.\n" +
                 "작게 잡으면 자리들이 가운데로 몰려 붙고, 크게 잡으면 몇 개 안 될 때도\n" +
                 "원 가장자리까지 퍼져 성기게 보인다.")]
        [Min(1)]
        [SerializeField] private int _scatterSlots = 10;

        [Tooltip("자리 둘레에서 흔들리는 폭(px).\n\n" +
                 "이게 없으면 같은 연사가 늘 똑같은 모양으로 찍혀 기계처럼 보인다.\n" +
                 "자리 간격의 절반을 넘기면 이웃 자리를 침범해 다시 겹치기 시작한다.")]
        [Min(0f)]
        [SerializeField] private float _jitter = 11f;

        [Header("떠오름")]
        [Tooltip("사는 동안 떠오르는 거리(px). 화면 기준이라 거리와 무관하게 일정하다.")]
        [SerializeField] private float _riseDistance = 46f;

        [Tooltip("떠오르는 모양. 가로축이 수명, 세로축이 올라간 정도(0~1)다.\n" +
                 "처음에 빠르고 나중에 느려야 튀어나온 것처럼 보인다.")]
        [SerializeField] private AnimationCurve _rise = new(
            new Keyframe(0f, 0f, 2.5f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("더해질 때")]
        [Tooltip("피해가 더해지는 순간 부풀었다가 돌아오는 배율.\n" +
                 "묶어서 띄울 때만 쓰인다 — 따로 띄우면 나타나는 순간 한 번만 튄다.")]
        [Min(1f)]
        [SerializeField] private float _punchScale = 1.35f;

        [Tooltip("부푼 것이 돌아오는 데 걸리는 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _punchSeconds = 0.12f;

        /// <summary>이 숫자가 매달려 있는 대상. 사라지면 마지막 자리에 멈춘다.</summary>
        public Transform Target { get; private set; }

        /// <summary>대상이 사라졌을 때 쓸 마지막 자리.</summary>
        public Vector3 LastPoint { get; private set; }

        /// <summary>나타난 뒤 흐른 시간(초). 더 받아줄지는 띄우는 쪽이 이것으로 판단한다.</summary>
        public float Age { get; private set; }

        /// <summary>수명이 다했는지.</summary>
        public bool Finished => Age >= _lifetime;

        /// <summary>지금까지 쌓인 피해량.</summary>
        public float Total { get; private set; }

        public RectTransform Rect { get; private set; }

        private Vector2 _anchor;
        private Vector2 _offset;
        private float _punchRemaining;

        /// <summary>
        /// 새로 띄운다. 돌려 쓰는 조각이라 지난 값이 남지 않게 전부 되돌린다.
        /// </summary>
        /// <param name="spot">
        /// 이 대상 위에서 지금 비어 있는 자리 번호. 띄우는 쪽이 쓰이지 않는 것을 골라
        /// 주므로, 같은 번호가 두 번 나오지 않는 한 두 숫자가 겹치지 않는다.
        /// </param>
        public void Begin(Transform target, Vector3 point, float amount, bool killing, int spot)
        {
            Target = target;
            LastPoint = point;
            Age = 0f;
            Total = 0f;
            _punchRemaining = 0f;
            _offset = ScatterAt(spot);

            Add(amount, killing);
        }

        /// <summary>같은 대상을 또 맞혔다. 새로 띄우지 않고 여기에 더한다.</summary>
        public void Add(float amount, bool killing)
        {
            Total += amount;
            _punchRemaining = _punchSeconds;

            if (_label != null)
            {
                _label.text = Total.ToString($"F{_decimals}");
                _label.color = killing ? _killColor : _normalColor;
            }
        }

        /// <summary>대상이 살아 있는 동안 자리를 갱신해 둔다. 죽고 나면 이 자리에 멈춘다.</summary>
        public void Follow(Vector3 point) => LastPoint = point;

        /// <summary>화면 위 자리를 정한다. 흩어진 몫과 떠오른 몫은 여기서 얹는다.</summary>
        public void PlaceAt(Vector2 anchored) => _anchor = anchored;

        /// <summary>띄우는 쪽이 자기 시계로 밀어준다.</summary>
        public void Tick(float delta)
        {
            Age += delta;

            float life = Mathf.Clamp01(Age / _lifetime);

            Rect.anchoredPosition = _anchor + _offset + new Vector2(0f, _rise.Evaluate(life) * _riseDistance);

            if (_label != null)
            {
                // 흐려지기 시작하는 시점부터 0까지 고르게 내린다. _fadeFrom이 1이면
                // 나눗셈이 터지므로 그때는 끝까지 또렷하게 둔다.
                float span = 1f - _fadeFrom;

                Color color = _label.color;
                color.a = span > 0.0001f ? 1f - Mathf.Clamp01((life - _fadeFrom) / span) : 1f;
                _label.color = color;
            }

            if (_punchRemaining > 0f)
            {
                _punchRemaining = Mathf.Max(0f, _punchRemaining - delta);
                Rect.localScale = Vector3.one * Mathf.Lerp(1f, _punchScale, _punchRemaining / _punchSeconds);
            }
            else
            {
                Rect.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 자리 번호를 원 안의 한 점으로 바꾼다.
        /// <para>
        /// 해바라기 씨가 박히는 방식을 쓴다. 번호마다 황금각만큼 돌리고 반지름은
        /// 번호의 제곱근에 맞춰 늘리면, <b>몇 개를 쓰든 앞에서부터 잘라낸 것들이 늘
        /// 고르게 퍼진다.</b> 살아 있는 숫자의 수는 방아쇠를 당기는 대로 계속 바뀌므로,
        /// 특정 개수에만 예쁜 배치로는 모자란다.
        /// </para>
        /// <para>
        /// 제곱근을 쓰는 이유는 넓이 때문이다. 반지름을 번호에 그대로 비례시키면 바깥
        /// 고리가 안쪽보다 넓은데도 같은 수가 들어가서, 가운데만 빽빽해진다.
        /// </para>
        /// </summary>
        private Vector2 ScatterAt(int spot)
        {
            // 황금각(약 137.5도). 어떤 정수배로도 한 바퀴를 나누어 떨어뜨리지 못해서
            // 번호가 늘어도 앞의 것과 같은 방향에 겹쳐 서지 않는다.
            const float GoldenAngle = 2.39996323f;

            float radians = spot * GoldenAngle;
            float radius = _scatterRadius * Mathf.Sqrt(Mathf.Min(1f, (spot + 0.5f) / _scatterSlots));

            Vector2 seat = new(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);

            // 자리 안에서만 흔든다. 자리끼리는 겹치지 않으므로 흔드는 폭이
            // 자리 간격의 절반을 넘지 않는 한 이웃을 침범하지 않는다.
            return seat + (Random.insideUnitCircle * _jitter);
        }

        private void Awake()
        {
            Rect = transform as RectTransform;

            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>();
            }

            if (Rect == null || _label == null)
            {
                Debug.LogError(
                    $"{nameof(DamageNumberSlot)}: RectTransform 위에 있어야 하고 글상자가 있어야 합니다.", this);
                enabled = false;
            }
        }
    }
}
