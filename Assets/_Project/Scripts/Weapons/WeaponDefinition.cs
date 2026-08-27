using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 무기 한 종류의 공통 성능. 기총과 미사일이 여기서 갈라진다.
    /// <para>
    /// 주무기든 보조무기든 화면과 탄, 재보급은 같은 방식으로 다뤄야 한다. 무기마다
    /// 다른 이름의 값을 두면 무기를 하나 늘릴 때마다 그것들을 전부 고치게 된다.
    /// </para>
    /// <para>
    /// 탄은 실어 나가는 것이 아니라 차오르는 것이다. 어느 자리에 다느냐가 아니라
    /// 여기 적힌 수치가 무기의 성격을 정한다 — 많이 싣고 빨리 차면 기총이고,
    /// 적게 싣고 느리게 차면 한 발이 곧 한 판단인 무기다.
    /// </para>
    /// </summary>
    public abstract class WeaponDefinition : ScriptableObject
    {
        [Header("표시")]
        public string DisplayName = "Unnamed Weapon";

        [Tooltip("화면에 띄울 한 줄 설명. 무엇에 쓰는 무기인지.")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("무기 분류 글리프. 미사일이냐 로켓이냐 폭탄이냐를 나타내는 기호.\n" +
                 "같은 갈래의 무기끼리는 같은 그림을 쓴다 — 이름을 읽기 전에\n" +
                 "무엇이 걸려 있는지 알아보는 용도다.")]
        public Sprite Icon;

        [Tooltip("무기 그림. 글리프와 달리 그 무기 하나를 그린 것이다.\n" +
                 "글리프가 갈래를 알려준다면 이쪽은 무엇이 걸려 있는지를 보여준다 —\n" +
                 "같은 갈래의 무기라도 서로 다른 그림을 쓴다.")]
        public Sprite Picture;

        [Header("장비")]
        [Tooltip("이 무기의 몸. 무기 컴포넌트와 총구, 딸린 것들을 담은 프리팹.\n\n" +
                 "실었을 때 기체에 찍혀 나오고, 벗으면 통째로 사라진다. 그래서 안 실은\n" +
                 "무기는 기체에 코드 한 줄도 남기지 않는다.\n\n" +
                 "비워두면 이 무기는 실을 수 없다.")]
        public GameObject Equipment;

        [Header("자리")]
        [Tooltip("이 무기가 걸리는 자리.\n" +
                 "주무기는 늘 쥐고 있는 쪽, 보조무기는 때를 골라 쓰는 쪽이다.\n" +
                 "한 기체에 같은 자리를 노리는 무기가 둘이면 나중 것은 걸리지 않는다.")]
        public WeaponSlot Slot = WeaponSlot.Primary;

        [Header("소리")]
        [Tooltip("한 발 나갈 때의 소리. 연사 루프가 있으면 그쪽이 우선한다.")]
        public AudioClip FireSound;

        [Tooltip("연사 중에 도는 루프. 기총처럼 발 사이가 붙은 무기가 쓴다.\n" +
                 "발마다 울리면 초당 수십 개가 쌓여 동시 재생 한도에 잘려 나간다.\n" +
                 "비워두면 발마다 위 소리가 난다.")]
        public AudioClip FireLoop;

        [Tooltip("루프의 재생 속도. 1이면 녹음 그대로다.\n\n" +
                 "녹음된 연사 박자가 실제 분당 발사 수와 안 맞을 때 여기서 맞춘다 —\n" +
                 "빠르게 돌리면 음도 함께 높아지고, 늦추면 낮아진다. 소리란 그런 것이라\n" +
                 "크게 벗어나면 다른 총처럼 들리니, 그때는 클립을 바꾸는 편이 맞다.")]
        [Range(0.25f, 3f)]
        public float LoopPitch = 1f;

        [Tooltip("소리 크기.")]
        [Range(0f, 1f)]
        public float SoundVolume = 0.6f;

        [Tooltip("한 발마다 흔들리는 음높이 폭. 같은 소리의 반복이 기계음으로 들리지 않게 한다.")]
        [Range(0f, 0.5f)]
        public float PitchJitter = 0.08f;

        [Header("발사")]
        [Tooltip("분당 발사 수.")]
        [Min(1f)]
        public float RoundsPerMinute = 900f;

        [Tooltip("날아갈 것의 프리팹.")]
        public GameObject Prefab;

        [Tooltip("이만큼 날아가면 사라진다 (m).")]
        [Min(1f)]
        public float Range = 300f;

        [Header("탄")]
        [Tooltip("한 번에 쥐고 있을 수 있는 최대 발수.\n" +
                 "쏘면 줄고 시간이 지나면 한 발씩 돌아온다. 다 쓰면 잠깐 못 쏠 뿐,\n" +
                 "재보급을 기다려야 하는 것은 아니다.")]
        [Min(1)]
        public int AmmoCapacity = 600;

        [Tooltip("한 발이 돌아오는 데 걸리는 시간(초).\n" +
                 "0이면 지연이 끝나는 순간 통째로 찬다.\n\n" +
                 "발사 간격과 함께 봐야 한다 — 이 값이 발사 간격보다 짧으면\n" +
                 "쥐고 있는 내내 차는 쪽이 빨라서 탄이 줄지 않는다.")]
        [Min(0f)]
        public float RechargeSeconds = 0.05f;

        [Tooltip("마지막 한 발을 쓴 뒤 채우기 시작할 때까지의 시간(초).\n\n" +
                 "이것이 있어야 쥐고 있는 동안에는 줄기만 하고, 손을 떼야 찬다.\n" +
                 "0으로 두면 쏘는 중에도 차올라서 바닥이 잘 보이지 않는다.")]
        [Min(0f)]
        public float RechargeDelay = 0.8f;

        [Tooltip("바닥난 뒤 다시 쏠 수 있으려면 차 있어야 하는 발수.\n\n" +
                 "1이면 잠기지 않는다 — 한 발 돌아오는 즉시 쏠 수 있고, 쥐고 있으면\n" +
                 "돌아오는 족족 한 발씩 새어 나간다. 기총처럼 계속 쥐고 있는 무기는\n" +
                 "여유를 두는 편이 낫다.")]
        [Min(1)]
        public int ResumeRounds = 1;

        [Header("위력")]
        [Tooltip("관문을 통과했을 때 들어가는 피해량.")]
        public float Damage = 12f;

        [Tooltip("관통력. 표적의 장갑 이상이어야 피해가 들어간다.")]
        [Min(0f)]
        public float Penetration = 5f;

        [Tooltip("철거력. 건물이 요구하는 수준 이상이어야 부술 수 있다.")]
        [Min(0f)]
        public float Demolition;

        /// <summary>한 발과 다음 발 사이의 간격(초).</summary>
        public float ShotInterval => 60f / RoundsPerMinute;

        /// <summary>가득 쥔 탄을 쉬지 않고 쏟아부었을 때 버티는 시간(초).</summary>
        public float SustainedFireSeconds => AmmoCapacity * ShotInterval;

        /// <summary>텅 빈 상태에서 가득 차기까지 걸리는 시간(초). 지연까지 친 값이다.</summary>
        public float FullRechargeSeconds => RechargeDelay + (AmmoCapacity * RechargeSeconds);
    }
}
