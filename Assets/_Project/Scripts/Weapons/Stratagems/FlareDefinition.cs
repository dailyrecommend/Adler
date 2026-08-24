using Adler.Abilities;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 미사일을 속여 떼어내는 조명탄 한 묶음.
    /// <para>
    /// 커맨드를 쳐서 장전해두고, 필요할 때 버튼 하나로 뿌린다. 폭탄과 같은 방식이다 —
    /// 미사일이 날아오는 몇 초 안에 방향키를 칠 수는 없으므로, 안전할 때 미리 받아두는
    /// 것이 이 물건의 절차가 된다.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Flare", menuName = "Adler/Stratagems/Flare Definition")]
    public sealed class FlareDefinition : StratagemDefinition
    {
        [Header("조명탄")]
        [Tooltip("만들어 둔 조명탄. Flare 컴포넌트와 Rigidbody가 붙어 있어야 한다.")]
        public GameObject Prefab;

        [Tooltip("한 번에 뿌리는 개수. SAM이 한 제차에 쏘는 발수보다 넉넉해야 한다.")]
        [Min(1)]
        public int Count = 4;

        [Tooltip("연달아 나가는 간격(초). 짧아야 한 뭉치로 보인다.")]
        [Min(0.01f)]
        public float Interval = 0.12f;

        [Header("사출")]
        [Tooltip("나오는 자리가 벌어지는 반경 (m).\n" +
                 "속도로 밀어내지 않으므로 흩어져 보이는 것은 이 값이 만든다.\n" +
                 "기체 크기의 두세 배면 한 뭉치로 보이면서도 개수가 읽힌다.")]
        [Min(0f)]
        public float SpawnRadius = 2f;

        [Tooltip("나올 때 실리는 속도 (m/s). 작게 둔다.\n\n" +
                 "세게 쏠 이유가 없다. 기체가 20m/s로 빠져나가므로 0.4초면 폭발 반경\n" +
                 "밖으로 벌어지고, 조명탄이 제자리에 멈춰 뒤로 흘러가는 모습이\n" +
                 "실제로도 그렇고 보기에도 낫다.")]
        [Min(0f)]
        public float EjectSpeed = 4f;

        [Tooltip("뒤로 밀리는 정도. 1이면 정확히 뒤로 나간다.")]
        [Range(0f, 1f)]
        public float BackwardBias = 0.45f;

        [Tooltip("아래로 처지는 정도. 중력이 마저 끌어내리므로 크지 않아도 된다.")]
        [Range(0f, 1f)]
        public float DownwardBias = 0.35f;

        [Tooltip("좌우로 벌어지는 각도. 부채꼴로 퍼져야 여러 발을 동시에 상대할 수 있다.")]
        [Range(0f, 80f)]
        public float SpreadAngle = 35f;

        [Tooltip("사출 방향에 섞는 흔들림(도). 0이면 매번 똑같은 모양이라 기계처럼 보인다.")]
        [Range(0f, 30f)]
        public float Scatter = 8f;

        [Tooltip("튀어나가며 도는 정도. 궤적이 흔들려 눈에 잘 띈다.")]
        [Min(0f)]
        public float Spin = 12f;

        [Tooltip("받을 중력의 배율. 0이면 뜬 자리에 머물고 1이면 그대로 떨어진다.\n\n" +
                 "저항을 올려 떠 있게 만들 수도 있지만 그러면 사출 속도까지 함께 죽어서,\n" +
                 "기체에서 벌어지기 전에 멈춰 폭발에 같이 휘말린다. 중력만 줄이면\n" +
                 "튀어나가는 힘은 그대로 두고 천천히 가라앉게 할 수 있다.")]
        [Range(0f, 1f)]
        public float GravityScale = 0.3f;

        /// <inheritdoc />
        public override Ability Create() => new FlareAbility(this);

        [Header("유혹")]
        [Tooltip("타는 시간(초). 이 시간이 지나면 미사일을 끌지 못한다.")]
        [Min(0.1f)]
        public float BurnSeconds = 4f;

        [Tooltip("미사일이 이 거리 안에 있어야 속는다 (m).")]
        [Min(1f)]
        public float SeduceRange = 80f;

        [Tooltip("미사일의 진행 방향 기준 이 각도 안에 있어야 속는다.\n" +
                 "미사일이 이미 지나간 뒤에 뿌리면 통하지 않는다는 뜻이고,\n" +
                 "그래서 언제 누르는가가 실력이 된다.")]
        [Range(5f, 120f)]
        public float SeduceAngle = 45f;
    }
}
