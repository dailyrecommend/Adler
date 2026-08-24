using System.Collections.Generic;
using Adler.Aircraft;
using Adler.Controls;
using Adler.Combat;
using Adler.Core;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 플레이어 기체. 입력 장치에서 <see cref="FlightInput"/>을 채워
    /// <see cref="IFlightModel"/>에 넘기는 일만 한다.
    /// <para>
    /// 비행 로직은 모델에, 성능 수치는 <see cref="AircraftStatSheet"/>에 있다.
    /// 물리 기반 모델로 갈아탈 때 바꿀 곳은 <see cref="CreateModel"/> 한 곳이다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class AircraftController : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("입력을 읽어오는 곳. 비워두면 이 오브젝트에서 찾는다.")]
        [SerializeField] private PilotInput _input;

        [Tooltip("기체의 소재 성능. 정비 결과는 여기가 아니라 런타임 스탯 시트에 얹힌다.")]
        [SerializeField] private AirframeDefinition _airframe;

        [Header("초기 장착 부품")]
        [Tooltip("출격 시점의 부품 구성. 비행 중 교체는 Stats.Equip()으로 한다.")]
        [SerializeField] private List<PartDefinition> _initialParts = new();


        [Header("조작 설정")]
        [Tooltip("스틱을 밀 때 기수가 올라간다.")]
        [SerializeField] private bool _invertPitch;

        [Tooltip("이 디버프가 걸려 있으면 조종과 추력이 끊긴다.\n" +
                 "고도든 다른 무엇이든, 이것을 목록에 올리는 쪽이면 모두 같은 결과를 낸다.")]
        [SerializeField] private DebuffDefinition _frozenDebuff;

        private Clock _clock;
        private Rigidbody _body;
        private ArcadeFlightModel _model;

        // 같은 오브젝트에 있으면 연료 제한이 걸리고, 없으면 무제한이다.
        private BoostFuel _boostFuel;

        // 없으면 얼어붙는 일도 없다.
        private AircraftDebuffs _debuffs;

        // 키보드를 가져간 것이 있는지 알아야 WASD를 조종에서 뗄 수 있다.


        /// <summary>HUD와 카메라가 속도를 읽는 통로.</summary>
        public IFlightModel Model => _model;

        /// <summary>정비창과 전투 로직이 성능을 읽고 바꾸는 통로.</summary>
        public AircraftStatSheet Stats { get; private set; }

        /// <summary>부품으로 바뀌지 않는 기체 고유 특성을 읽는 통로.</summary>
        public AirframeDefinition Airframe => _airframe;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _body = GetComponent<Rigidbody>();

            _boostFuel = GetComponent<BoostFuel>();
            _debuffs = GetComponent<AircraftDebuffs>();
            if (_input == null)
            {
                _input = GetComponent<PilotInput>();
            }

            if (_airframe == null)
            {
                Debug.LogError($"{nameof(AircraftController)}: Airframe이 비어 있어 기체를 조종할 수 없습니다.", this);
                enabled = false;
                return;
            }

            Stats = new AircraftStatSheet(_airframe);
            EquipInitialParts();

            _model = CreateModel();
            _model.Initialize(_body);
        }

        private void EquipInitialParts()
        {
            foreach (PartDefinition part in _initialParts)
            {
                if (part == null)
                {
                    continue;
                }

                PartDefinition replaced = Stats.Equip(part);
                if (replaced != null)
                {
                    Debug.LogWarning(
                        $"{nameof(AircraftController)}: {part.Slot} 슬롯에 부품이 둘 이상 지정되어 " +
                        $"'{replaced.DisplayName}'이(가) '{part.DisplayName}'으로 대체되었습니다.", this);
                }
            }
        }

        /// <summary>비행 모델을 교체하려면 이 메서드만 바꾸면 된다.</summary>
        private ArcadeFlightModel CreateModel()
        {
            return new ArcadeFlightModel(Stats) { InvertPitch = _invertPitch };
        }


        /// <summary>
        /// 비행 상태를 처음으로 되돌린다. 리스폰이 기체를 옮긴 뒤에 부른다.
        /// <para>
        /// 위치만 옮기고 이걸 부르지 않으면, 추락하던 속도와 자세가 그대로 남은 채
        /// 출발 지점에서 다시 시작하게 된다.
        /// </para>
        /// </summary>
        public void ResetFlight()
        {
            _model?.Initialize(_body);
        }

        private void FixedUpdate()
        {
            // 무엇이 얼렸는지는 묻지 않는다. 고도든 적의 무기든 목록에 이것이 올라와
            // 있으면 결과는 같고, 새로 얼리는 것이 생겨도 여기는 그대로다.
            _model.SetFrozen(_frozenDebuff != null
                             && _debuffs != null
                             && _debuffs.IsActive(_frozenDebuff));

            FlightInput input = ReadInput();
            _model.Tick(in input, _clock);
        }

        private FlightInput ReadInput()
        {
            // 연료 쪽은 누르지 않을 때도 불러야 한다. 회복을 그쪽에서 함께 처리하므로,
            // 쓰지 않는 동안 부르지 않으면 연료가 영영 차지 않는다.
            //
            // 얼어붙은 동안에는 누르지 않은 것으로 넘긴다. 엔진이 죽었는데 연료만
            // 타들어가면, 녹은 뒤에 쓸 것이 남아 있지 않아 두 번 벌을 받는다.
            bool wantsBoost = !_model.IsFrozen && _input.Boost;
            bool boosting = _boostFuel != null
                ? _boostFuel.RequestBoost(wantsBoost, _clock.FixedDelta)
                : wantsBoost;

            return new FlightInput
            {
                Pitch = _input.Pitch,
                Roll = _input.Roll,
                Boost = boosting,
            };
        }


#if UNITY_EDITOR
        /// <summary>인스펙터에서 Invert Pitch를 켜고 끄면 플레이 중에도 즉시 반영한다.</summary>
        private void OnValidate()
        {
            if (_model != null)
            {
                _model.InvertPitch = _invertPitch;
            }
        }
#endif
    }
}
