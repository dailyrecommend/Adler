using System;
using System.Collections.Generic;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체가 든 무기들을 관리하고 방아쇠를 넘겨준다.
    /// <para>
    /// 방아쇠를 여기서만 읽는다. 무기가 각자 입력을 읽으면 교체한 뒤에도 이전 무기가
    /// 계속 나가고, 어느 것이 손에 들려 있는지 아는 곳이 없어진다.
    /// </para>
    /// <para>
    /// 바꾸는 순간 이전 무기에게 물러난다고 알린다. 미사일이 쌓아둔 조준이 그때 풀린다 —
    /// 안 그러면 기총을 쥔 채 조준을 채워두었다가 바꿔서 즉시 쏘는 것이 최선이 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponBay : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private InputActionAsset _controls;

        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("탑재 무기")]
        [Tooltip("들고 다니는 무기들. 순서대로 돌아가며 교체된다.\n" +
                 "비워두면 기체에 붙어 있는 것들을 알아서 모은다.")]
        [SerializeField] private List<AircraftWeapon> _weapons = new();

        private InputAction _fireAction;
        private InputAction _switchAction;
        private InputAction _cycleTargetAction;
        private LockOnTargeting _targeting;
        private int _activeIndex;

        /// <summary>손에 든 무기가 바뀔 때.</summary>
        public event Action<AircraftWeapon> WeaponChanged;

        /// <summary>지금 손에 든 무기. 하나도 없으면 null.</summary>
        public AircraftWeapon Active =>
            _weapons.Count > 0 ? _weapons[Mathf.Clamp(_activeIndex, 0, _weapons.Count - 1)] : null;

        public IReadOnlyList<AircraftWeapon> Weapons => _weapons;

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_weapons.Count == 0 && _aircraft != null)
            {
                _aircraft.GetComponentsInChildren(includeInactive: true, _weapons);
            }

            _weapons.RemoveAll(weapon => weapon == null);

            if (_aircraft != null)
            {
                _targeting = _aircraft.GetComponentInChildren<LockOnTargeting>(includeInactive: true);
            }
        }

        private void Start()
        {
            // 손에 드는 알림은 Start에서 보낸다. 무기들이 자기 Awake를 마친 뒤여야
            // 조준 규칙을 넘겨받을 준비가 되어 있다.
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (i != _activeIndex)
                {
                    _weapons[i].OnStowed();
                }
            }

            Active?.OnDrawn();
            WeaponChanged?.Invoke(Active);
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(WeaponBay)}: Controls 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _controls.FindActionMap("Flight", throwIfNotFound: true);
            _fireAction = map.FindAction("Fire", throwIfNotFound: true);
            _switchAction = map.FindAction("SwitchWeapon", throwIfNotFound: true);
            _cycleTargetAction = map.FindAction("SwitchTarget", throwIfNotFound: true);

            _fireAction.Enable();
            _switchAction.Enable();
            _cycleTargetAction.Enable();
        }

        private void OnDisable()
        {
            _fireAction?.Disable();
            _switchAction?.Disable();
            _cycleTargetAction?.Disable();

            // 꺼질 때 방아쇠를 놓은 것으로 친다. 격추된 뒤 되살아났을 때
            // 눌려 있던 상태가 남아 첫 발이 저절로 나가지 않게 한다.
            Active?.ReleaseTrigger();
        }

        private void Update()
        {
            if (_switchAction.WasPressedThisFrame())
            {
                SelectNext();
            }

            if (_cycleTargetAction.WasPressedThisFrame() && _targeting != null)
            {
                _targeting.CycleTarget();
            }

            AircraftWeapon weapon = Active;
            if (weapon == null)
            {
                return;
            }

            if (_fireAction.IsPressed())
            {
                weapon.HoldTrigger(_clock.Delta);
            }
            else
            {
                weapon.ReleaseTrigger();
            }
        }

        /// <summary>다음 무기로 넘어간다.</summary>
        public void SelectNext()
        {
            if (_weapons.Count < 2)
            {
                return;
            }

            Select((_activeIndex + 1) % _weapons.Count);
        }

        /// <summary>지정한 자리의 무기를 든다.</summary>
        public void Select(int index)
        {
            if (_weapons.Count == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, _weapons.Count - 1);
            if (index == _activeIndex)
            {
                return;
            }

            Active?.OnStowed();
            _activeIndex = index;
            Active?.OnDrawn();

            WeaponChanged?.Invoke(Active);
        }

        /// <summary>모든 무기를 비율만큼 채운다. 재보급이 부른다.</summary>
        public void ResupplyAll(float percent)
        {
            foreach (AircraftWeapon weapon in _weapons)
            {
                weapon.Resupply(percent);
            }
        }

        /// <summary>모든 무기를 가득 채운다. 출격을 다시 시작할 때 부른다.</summary>
        public void RestockAll()
        {
            foreach (AircraftWeapon weapon in _weapons)
            {
                weapon.Restock();
            }
        }
    }
}
