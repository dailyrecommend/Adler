using Adler.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 안에 있는 동안 나쁜 상태를 거는 구역.
    /// <para>
    /// 트리거 콜라이더의 모양이 곧 구역이다. 협곡이든 구름층이든 원하는 형태로 잡아두면
    /// 되고, 새 구역을 만드는 데 코드를 고칠 일은 없다 — 에셋 하나와 콜라이더 하나다.
    /// </para>
    /// <para>
    /// 들어오고 나가는 것을 그대로 켜고 끄지 않는다. 나가는 신호는 믿을 수 없기 때문이다.
    /// 재출격으로 순간이동하거나 격추로 오브젝트가 꺼지면 나갔다는 통보가 오지 않고,
    /// 그러면 구역 밖에서도 상태가 영영 걸린 채로 남는다.
    /// </para>
    /// <para>
    /// 대신 머무는 동안 계속 도장을 찍고, 도장이 낡으면 나간 것으로 본다. 어떤 경로로
    /// 빠져나가든 저절로 풀린다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebuffZone : MonoBehaviour
    {
        private static readonly List<DebuffZone> Active = new();

        [Tooltip("이 안에 있는 동안 걸릴 것들. 여러 개를 함께 걸 수 있다.")]
        [SerializeField] private DebuffDefinition[] _debuffs;

        [Tooltip("마지막으로 닿은 뒤 이만큼 지나면 나간 것으로 본다(초).\n" +
                 "물리 갱신 몇 번을 견딜 만큼만 두면 된다. 길면 나온 뒤에도 남는다.")]
        [Min(0.05f)]
        [SerializeField] private float _graceSeconds = 0.25f;

        // 안에 있는 기체와 마지막으로 닿은 시각.
        private readonly Dictionary<AircraftDebuffs, float> _inside = new();
        private Clock _clock;

        private void OnEnable() => Active.Add(this);

        private void OnDisable()
        {
            Active.Remove(this);

            // 구역이 사라지면 걸어둔 것도 함께 사라진다. 남겨두면 없어진 구역의
            // 상태가 화면에 붙어 있는다.
            _inside.Clear();
        }

        private void Awake()
        {
            _clock = TimeScale.For(this);
            var collider = GetComponent<Collider>();

            if (collider == null || !collider.isTrigger)
            {
                Debug.LogError(
                    $"{nameof(DebuffZone)}: Is Trigger가 켜진 Collider가 있어야 합니다.", this);
            }
        }

        private void OnTriggerEnter(Collider other) => Touch(other);

        // 머무는 동안 계속 찍는다. 이것이 나갔는지를 판단하는 유일한 근거다.
        private void OnTriggerStay(Collider other) => Touch(other);

        private void Touch(Collider other)
        {
            AircraftDebuffs debuffs = other.GetComponentInParent<AircraftDebuffs>();

            if (debuffs != null)
            {
                _inside[debuffs] = _clock.Now;
            }
        }

        /// <summary>이 기체에 걸려 있는 구역 효과를 모은다.</summary>
        public static void CollectFor(AircraftDebuffs owner, List<DebuffDefinition> into)
        {
            foreach (DebuffZone zone in Active)
            {
                if (!zone._inside.TryGetValue(owner, out float seen)
                    || zone._clock.Now - seen > zone._graceSeconds)
                {
                    continue;
                }

                foreach (DebuffDefinition debuff in zone._debuffs)
                {
                    if (debuff != null && !into.Contains(debuff))
                    {
                        into.Add(debuff);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            var collider = GetComponent<Collider>();

            if (collider == null)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Bounds bounds = collider.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
