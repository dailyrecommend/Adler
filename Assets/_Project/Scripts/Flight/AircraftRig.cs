using Adler.Abilities;
using Adler.Aircraft;
using Adler.Controls;
using Adler.Combat;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 기체 한 대를 대표한다. 기체를 이루는 부품들을 한 번 찾아두고 나눠준다.
    /// <para>
    /// 이것이 없으면 부품마다 서로를 직접 참조하게 되고, 컴포넌트를 하나 붙일 때마다
    /// 인스펙터에서 이어야 할 선이 늘어난다. 기체 밖에서도 마찬가지여서, 화면 표시는
    /// 속도계는 조종을, 탄약계는 탄창을, 연료계는 부스터를 각각 가리키게 된다.
    /// </para>
    /// <para>
    /// 이제 이어야 할 선은 기체당 하나다. 기체 위에 있는 부품은 알아서 찾고, 밖에 있는
    /// 것은 이 컴포넌트만 받으면 된다.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class AircraftRig : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }

        public AircraftController Control { get; private set; }

        public AircraftLifecycle Lifecycle { get; private set; }

        /// <summary>조종석의 입력. 무엇을 하려는지는 여기서 읽는다.</summary>
        public PilotInput Input { get; private set; }

        public Health Health { get; private set; }

        public BoostFuel Boost { get; private set; }

        /// <summary>들고 있는 무기들. 지금 손에 든 것은 <c>Weapons.Active</c>로 읽는다.</summary>
        public WeaponBay Weapons { get; private set; }

        /// <summary>미사일 조준. 무기와 나뉘어 있어 기총을 들고 있을 때도 읽을 수 있다.</summary>
        public LockOnTargeting Targeting { get; private set; }

        /// <summary>기총 조준 보정. 없으면 탄이 겨눈 그대로 나간다.</summary>
        public SoftLock Aim { get; private set; }

        public StratagemBay Stratagems { get; private set; }

        /// <summary>이 기체가 할 수 있는 행동들. 스트라타젬의 실행도 여기서 돈다.</summary>
        public AbilityRunner Abilities { get; private set; }

        /// <summary>갈고리. 걸려 있는지는 <c>Grapple.IsAttached</c>로 읽는다.</summary>
        public GrapplingHook Grapple { get; private set; }

        /// <summary>몸으로 들이받는 것. 없으면 부딪히는 것은 그냥 죽는 일이다.</summary>
        public RamAttack Ram { get; private set; }

        /// <summary>지금 걸려 있는 나쁜 상태들. 무엇이 걸리는지는 각 시스템이 알린다.</summary>
        public AircraftDebuffs Debuffs { get; private set; }

        /// <summary>지금 비행 상태. 속도와 부스터 여부를 화면 표시와 연출이 읽어 간다.</summary>
        public IFlightModel Model => Control != null ? Control.Model : null;

        /// <summary>부품 보정이 반영된 실효 성능. 조종이 준비된 뒤에야 값이 있다.</summary>
        public AircraftStatSheet Stats => Control != null ? Control.Stats : null;

        /// <summary>부품으로 바뀌지 않는 기체 고유 특성.</summary>
        public AirframeDefinition Airframe => Control != null ? Control.Airframe : null;

        /// <summary>
        /// 다른 부품보다 먼저 돈다. 그래서 나머지는 자기 Awake에서 이 결과를 믿고 쓸 수 있다.
        /// </summary>
        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Control = GetComponentInChildren<AircraftController>(includeInactive: true);
            Lifecycle = GetComponentInChildren<AircraftLifecycle>(includeInactive: true);
            Input = GetComponentInChildren<PilotInput>(includeInactive: true);
            Health = GetComponentInChildren<Health>(includeInactive: true);
            Boost = GetComponentInChildren<BoostFuel>(includeInactive: true);
            Weapons = GetComponentInChildren<WeaponBay>(includeInactive: true);
            Targeting = GetComponentInChildren<LockOnTargeting>(includeInactive: true);
            Aim = GetComponentInChildren<SoftLock>(includeInactive: true);
            Stratagems = GetComponentInChildren<StratagemBay>(includeInactive: true);
            Abilities = GetComponentInChildren<AbilityRunner>(includeInactive: true);
            Grapple = GetComponentInChildren<GrapplingHook>(includeInactive: true);
            Ram = GetComponentInChildren<RamAttack>(includeInactive: true);
            Debuffs = GetComponentInChildren<AircraftDebuffs>(includeInactive: true);
        }

        /// <summary>
        /// 기체 위에 얹힌 부품이 자기 기체를 찾을 때 쓴다.
        /// 인스펙터에 넣어두면 그것을 쓰고, 비어 있으면 위로 거슬러 올라가 찾는다.
        /// </summary>
        public static AircraftRig Resolve(Component component, AircraftRig assigned)
        {
            return assigned != null ? assigned : component.GetComponentInParent<AircraftRig>();
        }
    }
}
