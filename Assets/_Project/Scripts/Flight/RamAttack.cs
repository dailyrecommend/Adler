using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 부스터를 켠 채 들이받으면 상대가 깎이고 나는 멀쩡하다.
    /// <para>
    /// 평소에 적과 부딪히는 것은 그냥 죽는 일이다. 부스터가 그것을 뒤집으므로, 정면으로
    /// 파고드는 것이 도박이 아니라 선택이 된다 — 다만 연료를 쓰고, 그동안 선회가 무뎌지고,
    /// 빗나가면 원래대로 죽는다.
    /// </para>
    /// <para>
    /// 얼마나 세게 붙었는지는 묻지 않는다. 문턱을 두면 갈고리에 끌려가 닿는 것처럼
    /// 스스로 속도를 내지 않은 접촉이 걸리지 않고, 무엇보다 때릴 조건과 안 죽을 조건이
    /// 갈라진다 — 살살 닿아 때리지 못하는 그 순간에도 방패는 내려가면 안 되기 때문이다.
    /// 갈라진 만큼이 곧 <b>상대는 멀쩡한데 나만 죽는</b> 틈이 된다.
    /// </para>
    /// <para>
    /// 무엇으로 뒤집을지는 고정하지 않는다. 조합마다 한 줄이라, 나중에 들이받게 해주는
    /// 스트라타젬이 생겨도 줄을 하나 더하면 된다.
    /// </para>
    /// <para>
    /// <b>콜라이더가 있는 그 오브젝트에 붙여야 한다.</b> 유니티는 충돌을 콜라이더가
    /// 달린 오브젝트의 컴포넌트에만 알리므로, 빈 자식에 붙이면 아무것도 일어나지 않는다.
    /// Rigidbody를 요구하지 않는 것도 그래서다 — 요구해 두면 엉뚱한 자식에 붙였을 때
    /// 유니티가 거기에 물리 바디를 하나 몰래 만들어, 증상이 조용해지는 대신 나빠진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamAttack : MonoBehaviour, IImpactShield
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Serializable]
        public struct Trigger
        {
            [Tooltip("이것들이 <b>모두</b> 참일 때 이 줄이 걸린다.\n" +
                     "비워두면 이 줄은 아무 때도 걸리지 않는다.")]
            public AircraftCondition[] All;

            [Tooltip("Debuff를 고른 경우에만 쓴다. 어느 디버프인지.")]
            public DebuffDefinition Debuff;
        }

        [Header("언제")]
        [Tooltip("들이받기가 되는 조합들. 하나라도 걸리면 된다.\n\n" +
                 "한 줄 안의 조건은 모두 참이어야 하고, 줄끼리는 아무거나 하나면 된다.\n" +
                 "속도는 묻지 않는다 — 걸려 있는 동안 닿으면 그것이 들이받기다.")]
        [SerializeField] private List<Trigger> _triggers = new()
        {
            new Trigger { All = new[] { AircraftCondition.Boosting } },
        };

        [Tooltip("들이받을 수 있는 상대의 레이어.\n" +
                 "지면과 건물은 빼둘 것 — 절벽에 처박는 것까지 무기가 되면 안 된다.")]
        [SerializeField] private LayerMask _targetMask;

        [Header("얼마나")]
        [Tooltip("한 번 들이받을 때 들어가는 피해.")]
        [Min(0f)]
        [SerializeField] private float _damage = 100f;

        [Tooltip("장갑을 뚫는 힘. 상대의 장갑보다 낮으면 아무것도 들어가지 않는다.\n" +
                 "기총으로는 못 여는 것을 몸으로 여는 길을 두고 싶으면 높게 잡는다.")]
        [Min(0f)]
        [SerializeField] private float _penetration = 999f;

        [Tooltip("구조를 부수는 힘. 모자라면 그만큼 깎여서 들어간다.")]
        [Min(0f)]
        [SerializeField] private float _demolition = 999f;

        [Tooltip("한 번 들이받고 다시 들이받기까지의 시간(초).\n\n" +
                 "이게 없으면 상대를 긁고 지나가는 동안 충돌이 여러 번 잡혀서,\n" +
                 "스쳤을 뿐인데 몇 배로 들어간다.")]
        [Min(0f)]
        [SerializeField] private float _interval = 0.25f;

        [Header("통과")]
        [Tooltip("들이받은 상대를 뚫고 지나간다.\n\n" +
                 "끄면 안 죽은 상대가 그 자리에 벽으로 남아 비비게 된다. 켜면 밀어붙인\n" +
                 "기세가 끊기지 않지만, 기체가 상대를 관통하는 그림이 잠깐 나온다.")]
        [SerializeField] private bool _passThrough = true;

        [Tooltip("이만큼 벌어지면 충돌을 되살린다(m).\n" +
                 "겹쳐 있는 동안 되살리면 물리가 둘을 떼어내며 세게 튕겨낸다.")]
        [Min(1f)]
        [SerializeField] private float _clearRange = 20f;

        [Tooltip("벌어지지 않아도 이 시간이 지나면 되살린다(초).\n" +
                 "상대가 나를 따라오거나 제자리에 멈춰 있으면 영영 벌어지지 않는다.")]
        [Min(0.5f)]
        [SerializeField] private float _clearTimeout = 4f;

        [Header("보호")]
        [Tooltip("조건이 풀린 뒤에도 이만큼은 충돌 피해를 막는다(초).\n\n" +
                 "들이받은 상대가 안 죽으면 그 자리에 단단한 것이 남는다. 그때 마침\n" +
                 "부스터 연료가 떨어지면 방패가 내려간 채로 아직 닿아 있어서, 밀어붙인\n" +
                 "그 순간이 아니라 그 직후에 죽는다 — 플레이어에게는 이유 없는 죽음이다.")]
        [Min(0f)]
        [SerializeField] private float _shieldGrace = 0.5f;

        [Header("되돌려주기")]
        [Tooltip("성공하면 채워주는 부스터 연료. 용량에 대한 비율이다. 0이면 안 준다.\n\n" +
                 "들이받으려면 부스터가 켜져 있어야 하므로, 되돌려주지 않으면 한 번\n" +
                 "박고 나면 연료가 없어 다음이 없다.\n\n" +
                 "가득 채우면 값을 치르지 않는 것이 된다 — 절반쯤 돌려주면 두 번은\n" +
                 "이어갈 수 있어도 계속 물고 늘어질 수는 없어서, 어디서 멈출지가 판단이 된다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _boostReturn = 0.5f;

        [Tooltip("성공하면 깎아주는 갈고리 쿨다운. 전체 쿨다운에 대한 비율이다.\n\n" +
                 "갈고리로 붙어서 박고, 박은 값으로 다시 걸고, 또 박는다.\n" +
                 "이 고리가 돌아가려면 두 값이 함께 돌아와야 한다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _grappleReturn = 0.5f;

        [Header("여운")]
        [Tooltip("들이받은 뒤 이만큼은 '들이받는 중'으로 친다(초).\n\n" +
                 "부딪히는 것은 한 순간이라 그것만으로는 켜고 끌 상태가 없다. 잔상처럼\n" +
                 "이어져야 하는 연출은 이 창을 보고 붙는다 — 짧으면 깜빡이고, 길면\n" +
                 "부딪힌 것과 상관없이 계속 켜져 있는 것처럼 보인다.")]
        [Min(0f)]
        [SerializeField] private float _afterSeconds = 0.4f;

        private Clock _clock;
        private float _readyAt;
        private float _shieldedUntil;
        private float _rammedUntil;

        // 지금 뚫고 지나가는 중인 상대들. 한 번에 여럿을 꿰뚫을 수 있으므로 하나로
        // 두지 않는다 — 뒤엣것이 앞엣것의 짝을 지우면 앞의 상대와 다시 부딪힌다.
        private readonly List<Passage> _passages = new();
        private readonly List<Collider> _mine = new();
        private readonly List<Collider> _theirs = new();

        /// <summary>
        /// 들이받아 피해가 들어갔을 때. 연출이 구독한다.
        /// <para>
        /// 여기서 소리나 화면을 직접 부르지 않는다. 부르는 순간 이 부품이 연출을 알게
        /// 되고, 연출을 하나 붙일 때마다 판정 코드를 고치게 된다.
        /// </para>
        /// </summary>
        public event Action<Collision, DamageResult> Rammed;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(RamAttack)}: 기체를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            // 잘못 붙었을 때의 증상이 "아무 일도 안 일어난다"라 원인을 여기서 찾지
            // 못한다. 충돌은 콜라이더가 달린 오브젝트에만 전해지므로, 그것이 없으면
            // 때리지도 막지도 못한 채 조용히 죽는다.
            if (GetComponent<Collider>() == null)
            {
                Debug.LogError(
                    $"{nameof(RamAttack)}: 이 오브젝트에 Collider가 없어 충돌을 받지 못합니다. " +
                    "기체의 Collider와 Rigidbody가 있는 오브젝트에 붙이세요.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 들이받는 중에는 충돌 피해를 받지 않는다.
        /// <para>
        /// <b>때리는 조건보다 넓다.</b> 같게 두면 안 된다 — 부딪힌 뒤 속도가 떨어져
        /// 때릴 만큼은 아니게 된 순간에 방패까지 같이 내려가서, 상대는 멀쩡한데 나만
        /// 죽는 충돌이 생긴다. 막는 쪽은 반드시 때리는 쪽을 품고 있어야 한다.
        /// </para>
        /// <para>
        /// 그래서 속도를 보지 않는다. 살살 스친 것은 상대를 깎지 않지만, 그렇다고
        /// 나를 죽여서도 안 된다.
        /// </para>
        /// </summary>
        public bool Blocks(Collision collision)
            => Targets(collision) && (Armed || _clock.Now < _shieldedUntil);

        /// <summary>
        /// 지금 들이받을 수 있는 상태인지. 갈고리가 이것을 보고 통과시킬지 정한다.
        /// </summary>
        public bool IsArmed => Armed;

        /// <summary>
        /// 방금 들이받았는지. 부딪힌 뒤 잠깐 참으로 남는다.
        /// <para>
        /// 들이받는 것은 한 순간이라 그 자체로는 이어지는 상태가 없다. 소리나 번쩍임은
        /// 사건으로 받으면 되지만, 잔상처럼 <em>켜져 있어야</em> 하는 연출에는 붙잡을
        /// 것이 필요해서 짧은 창을 남겨둔다.
        /// </para>
        /// </summary>
        public bool IsRamming => _clock != null && _clock.Now < _rammedUntil;

        /// <summary>어느 줄이든 걸려 있는지.</summary>
        private bool Armed
        {
            get
            {
                foreach (Trigger trigger in _triggers)
                {
                    if (Holds(in trigger))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 이 줄의 조건이 모두 참인지.
        /// <para>
        /// 비어 있는 줄은 걸리지 않는다. 아무것도 안 적힌 것을 "언제나"로 읽으면,
        /// 인스펙터에서 줄을 하나 더했다가 채우기 전에 항상 들이받는 상태가 된다.
        /// </para>
        /// </summary>
        private bool Holds(in Trigger trigger)
        {
            if (!enabled || _aircraft == null || trigger.All == null || trigger.All.Length == 0)
            {
                return false;
            }

            foreach (AircraftCondition condition in trigger.All)
            {
                if (!AircraftConditions.IsMet(_aircraft, condition, trigger.Debuff))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 조건이 참인 동안 보호 기한을 계속 밀어둔다. 풀리는 순간부터 유예가 흐른다.
        /// </summary>
        private void Update()
        {
            if (Armed)
            {
                _shieldedUntil = _clock.Now + _shieldGrace;
            }

            UpdatePassages();
        }

        /// <summary>
        /// 남아 있는 것을 되돌린다. 꺼질 때 짝을 남겨두면 다음에 같은 상대를 만났을 때
        /// 이유 없이 통과한다.
        /// </summary>
        private void OnDisable()
        {
            for (int i = _passages.Count - 1; i >= 0; i--)
            {
                Restore(i);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsRam(collision) || _clock.Now < _readyAt)
            {
                return;
            }

            IDamageable target = collision.collider.GetComponentInParent<IDamageable>();

            if (target == null || !target.IsAlive)
            {
                return;
            }

            _readyAt = _clock.Now + _interval;

            ContactPoint contact = collision.GetContact(0);

            DamageResult result = target.TakeDamage(new DamageInfo(
                _damage,
                _penetration,
                _demolition,
                contact.point,
                contact.normal,
                gameObject));

            if (result.Landed)
            {
                _rammedUntil = _clock.Now + _afterSeconds;

                Reward();
                Rammed?.Invoke(collision, result);
            }

            // 죽은 상대는 곧 사라지므로 뚫을 것도 없다.
            if (_passThrough && !result.Killed)
            {
                Pierce(collision.collider.transform.root);
            }
        }

        /// <summary>
        /// 맞힌 값을 돌려준다.
        /// <para>
        /// 절반만 돌려준다. 전부 돌려주면 맞히는 한 값을 치르지 않는 것이 되어, 한 번
        /// 물면 끝까지 물고 늘어지는 것이 언제나 옳은 답이 된다. 절반이면 두 번쯤은
        /// 이어갈 수 있어도 그 이상은 바닥나므로, <b>어디서 멈출지</b>가 판단으로 남는다.
        /// </para>
        /// <para>
        /// 무엇을 되돌릴지는 각자에게 맡긴다. 연료가 어떻게 차오르는지, 쿨다운이 언제부터
        /// 흐르는지는 그쪽의 사정이고, 여기서 값을 직접 써넣으면 그 사정이 바뀔 때마다
        /// 이곳도 함께 틀어진다.
        /// </para>
        /// </summary>
        private void Reward()
        {
            _aircraft.Boost?.Restore(_boostReturn);
            _aircraft.Grapple?.ReduceCooldown(_grappleReturn);
        }

        /// <summary>
        /// 이 상대와의 충돌을 잠시 없앤다.
        /// <para>
        /// 안 죽은 상대는 그 자리에 단단한 것으로 남는다. 밀어붙여 뚫는 연출인데 정작
        /// 거기서 멈춰 비비게 되면, 들이받기가 돌파가 아니라 사고로 읽힌다.
        /// </para>
        /// </summary>
        private void Pierce(Transform target)
        {
            if (target == null || IndexOf(target) >= 0)
            {
                return;
            }

            // 줄에 매달아 둔 상대는 건드리지 않는다. 갈고리가 이미 그 상대와의 충돌을
            // 켜고 끄고 있는데, Physics.IgnoreCollision은 누가 몇 번 껐는지 세지 않아서
            // 여기서 되돌리는 순간 갈고리의 기록이 거짓말이 된다. 한 짝의 주인은 하나여야 한다.
            //
            // 매달린 채로 뚫고 나가는 그림도 이상하다 — 줄로 당겨 붙여놓고 통과해
            // 멀어지면, 붙잡은 것이 아니라 스쳐 지나간 것으로 보인다.
            // 루트끼리 견준다. 갈고리가 무는 것은 콜라이더가 달린 자식일 수 있고,
            // 충돌을 끄는 범위는 그 루트 전체다.
            Transform hooked = _aircraft.Grapple != null ? _aircraft.Grapple.Hooked : null;

            if (hooked != null && hooked.root == target)
            {
                return;
            }

            _aircraft.GetComponentsInChildren(includeInactive: false, _mine);
            target.GetComponentsInChildren(includeInactive: false, _theirs);

            if (_mine.Count == 0 || _theirs.Count == 0)
            {
                return;
            }

            Passage passage = new(target, _clock.Now + _clearTimeout, _mine.Count * _theirs.Count);

            foreach (Collider mine in _mine)
            {
                foreach (Collider theirs in _theirs)
                {
                    Physics.IgnoreCollision(mine, theirs, true);
                    passage.Pairs.Add((mine, theirs));
                }
            }

            _passages.Add(passage);
        }

        /// <summary>
        /// 충분히 벌어졌으면 충돌을 되살린다.
        /// <para>
        /// 겹쳐 있는 동안 되살리면 물리가 둘을 떼어내며 세게 튕겨내서, 뚫고 지나간 것이
        /// 아니라 걸려서 튕긴 것으로 보인다. 그래서 벌어질 때까지 기다린다 — 다만
        /// 상대가 따라붙거나 제자리에 멈춰 있으면 영영 벌어지지 않으므로 기한도 둔다.
        /// </para>
        /// </summary>
        private void UpdatePassages()
        {
            for (int i = _passages.Count - 1; i >= 0; i--)
            {
                Passage passage = _passages[i];

                bool gone = passage.Target == null || !passage.Target.gameObject.activeInHierarchy;
                bool apart = !gone
                             && Vector3.Distance(passage.Target.position, transform.position) > _clearRange;

                if (gone || apart || _clock.Now >= passage.Until)
                {
                    Restore(i);
                }
            }
        }

        private void Restore(int index)
        {
            foreach ((Collider mine, Collider theirs) in _passages[index].Pairs)
            {
                if (mine != null && theirs != null)
                {
                    Physics.IgnoreCollision(mine, theirs, false);
                }
            }

            _passages.RemoveAt(index);
        }

        private int IndexOf(Transform target)
        {
            for (int i = 0; i < _passages.Count; i++)
            {
                if (_passages[i].Target == target)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>뚫고 지나가는 중인 상대 하나와, 되돌려야 할 짝들.</summary>
        private readonly struct Passage
        {
            public readonly Transform Target;
            public readonly float Until;
            public readonly List<(Collider Mine, Collider Theirs)> Pairs;

            public Passage(Transform target, float until, int capacity)
            {
                Target = target;
                Until = until;
                Pairs = new List<(Collider, Collider)>(capacity);
            }
        }

        /// <summary>
        /// 이 충돌로 상대를 깎는지. <see cref="Blocks"/>가 품고 있는 좁은 쪽이다.
        /// <para>
        /// 속도를 묻지 않는다. 문턱을 두면 살살 닿았을 때 때리지 못하는데, 그 순간에도
        /// 방패는 내려가면 안 되므로 두 판정이 갈라진다 — 갈라진 만큼이 곧 상대는
        /// 멀쩡한데 나만 죽는 틈이었다.
        /// </para>
        /// <para>
        /// 쉬는 시간도 여기서 보지 않는다. 그것까지 넣으면 두 번째 충돌이 들이받기가
        /// 아닌 것이 되고, 그 판정이 방패에까지 쓰이면 같은 틈이 다시 생긴다.
        /// </para>
        /// </summary>
        private bool IsRam(Collision collision) => Targets(collision) && Armed;

        /// <summary>들이받기가 걸리는 상대인지. 속도도 상태도 보지 않고 대상만 가린다.</summary>
        private bool Targets(Collision collision)
            => (_targetMask.value & (1 << collision.gameObject.layer)) != 0;
    }
}
