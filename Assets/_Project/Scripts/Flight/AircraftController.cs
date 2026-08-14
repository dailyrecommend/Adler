using System.Collections.Generic;
using Adler.Aircraft;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("기체의 소재 성능. 정비 결과는 여기가 아니라 런타임 스탯 시트에 얹힌다.")]
        [SerializeField] private AirframeDefinition _airframe;

        [Header("초기 장착 부품")]
        [Tooltip("출격 시점의 부품 구성. 비행 중 교체는 Stats.Equip()으로 한다.")]
        [SerializeField] private List<PartDefinition> _initialParts = new();

        [Header("조작 설정")]
        [Tooltip("스틱을 밀 때 기수가 올라간다.")]
        [SerializeField] private bool _invertPitch;

        private Rigidbody _body;
        private ArcadeFlightModel _model;

        private InputActionMap _flightMap;
        private InputAction _pitchAction;
        private InputAction _rollAction;
        private InputAction _yawAction;
        private InputAction _throttleAction;
        private InputAction _boostAction;

        /// <summary>HUD와 카메라가 속도를 읽는 통로.</summary>
        public IFlightModel Model => _model;

        /// <summary>정비창과 전투 로직이 성능을 읽고 바꾸는 통로.</summary>
        public AircraftStatSheet Stats { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();

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

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(AircraftController)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            _flightMap = _controls.FindActionMap("Flight", throwIfNotFound: true);
            _pitchAction = _flightMap.FindAction("Pitch", throwIfNotFound: true);
            _rollAction = _flightMap.FindAction("Roll", throwIfNotFound: true);
            _yawAction = _flightMap.FindAction("Yaw", throwIfNotFound: true);
            _throttleAction = _flightMap.FindAction("Throttle", throwIfNotFound: true);
            _boostAction = _flightMap.FindAction("Boost", throwIfNotFound: true);

            _flightMap.Enable();
        }

        private void OnDisable()
        {
            _flightMap?.Disable();
        }

        private void FixedUpdate()
        {
            FlightInput input = ReadInput();
            _model.Tick(in input, Time.fixedDeltaTime);
        }

        private FlightInput ReadInput()
        {
            return new FlightInput
            {
                Pitch = _pitchAction.ReadValue<float>(),
                Roll = _rollAction.ReadValue<float>(),
                Yaw = _yawAction.ReadValue<float>(),
                Throttle = _throttleAction.ReadValue<float>(),
                Boost = _boostAction.IsPressed(),
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
