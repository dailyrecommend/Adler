using System;
using System.Collections.Generic;
using Adler.Core;
using Adler.Abilities;
using Adler.Controls;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체가 든 무기들을 자리별로 세우고, 자리마다 방아쇠를 당긴다.
    /// <para>
    /// 주무기는 하나, 보조무기는 셋까지다. 주무기는 늘 그 자리에 있고 보조무기만
    /// 골라 쓴다 — 언제나 할 수 있는 일이 하나 있어야 고르는 일이 판단이 된다.
    /// </para>
    /// <para>
    /// 고르는 것은 보조무기 <b>안에서</b>다. 주무기와 보조무기 사이를 오가는 것이
    /// 아니라 둘 다 늘 손에 있고, 각자의 방아쇠로 동시에 나갈 수 있다.
    /// </para>
    /// <para>
    /// 어느 자리에 걸릴지는 묻지 않는다. 무기가 자기 성능 에셋에 적어두고 오므로,
    /// 여기서는 달려 있는 것을 모아 자리에 세우기만 한다. 실은 쪽이 정하게 두면
    /// 에셋과 인스펙터가 서로 다른 말을 할 수 있고, 그때 어느 쪽이 맞는지 알 길이 없다.
    /// </para>
    /// <para>
    /// 방아쇠를 여기서 읽는다. 사격은 쿨타임도, 지속시간도, 출격당 횟수도 없어서
    /// 행동 체계에 얹으면 그 체계가 하는 일을 전부 꺼둔 껍데기만 남는다 —
    /// 발사 간격과 탄은 이미 무기 자신의 것이다.
    /// </para>
    /// </summary>
    /// <remarks>
    /// 뿌리(-100) 다음, 나머지(0)보다 앞서 돈다. 걸린 무기 목록을 여기서 세우는데,
    /// 그 목록에 붙는 쪽들이 자기 OnEnable이나 Start에서 읽기 때문이다.
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class WeaponBay : MonoBehaviour, IWeaponHost
    {
        /// <summary>보조무기를 실을 수 있는 칸 수.</summary>
        public const int SecondaryCapacity = 3;

        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRoot _root;

        [Tooltip("입력을 읽어오는 곳. 비워두면 이 기체에서 찾는다.")]
        [SerializeField] private PilotInput _input;

        [Header("보조무기")]
        [Tooltip("이만큼 쓰지 않으면 고른 것을 놓는다(초). 0이면 놓지 않는다.\n\n" +
                 "쥔 채로 잊고 다니면 다음에 우클릭할 때 무엇이 나갈지 모르는 상태가 된다.\n" +
                 "손을 놓게 해두면 쏘는 것이 언제나 방금 고른 것이 된다.\n\n" +
                 "놓은 뒤에도 잃는 것은 없다. 휠을 돌리거나 우클릭하면 놓기 전에\n" +
                 "들고 있던 것을 그대로 다시 든다.")]
        [Min(0f)]
        [SerializeField] private float _holsterSeconds = 5f;

        private AircraftWeapon _primary;
        private readonly List<AircraftWeapon> _secondaries = new(SecondaryCapacity);

        // 고른 칸. -1은 아무것도 안 든 상태다.
        private int _selected;

        // 다시 들 때 돌아갈 자리. 놓기 전에 들고 있던 것을 기억해둔다.
        private int _lastSelected;

        private float _holsterAt;

        // 걸려 있는 것을 통째로 모아둔다. 소리와 화면이 무기 하나하나에 붙을 때 쓰고,
        // 부를 때마다 만들면 매 프레임 쓰레기가 쌓인다.
        private readonly List<AircraftWeapon> _mounted = new();

        private LockOnTargeting _targeting;
        private Clock _clock;

        /// <summary>고른 보조무기가 바뀔 때. 화면이 따라붙는다.</summary>
        public event Action<AircraftWeapon> SecondaryChanged;

        /// <summary>주무기. 없으면 null.</summary>
        public AircraftWeapon Primary => _primary;

        /// <summary>실려 있는 보조무기들. 순서가 곧 칸 순서다.</summary>
        public IReadOnlyList<AircraftWeapon> Secondaries => _secondaries;

        /// <summary>지금 고른 보조무기의 칸 번호. 아무것도 안 들었으면 -1.</summary>
        public int SelectedSecondary => _selected;

        /// <summary>지금 고른 보조무기. 안 들었거나 실은 것이 없으면 null.</summary>
        public AircraftWeapon Secondary =>
            _selected >= 0 && _selected < _secondaries.Count ? _secondaries[_selected] : null;

        /// <summary>그 자리의 무기. 보조무기는 고른 것을 준다.</summary>
        public AircraftWeapon this[WeaponSlot slot] =>
            slot == WeaponSlot.Secondary ? Secondary : _primary;

        /// <summary>걸려 있는 무기 전부. 고르지 않은 보조무기도 들어 있다.</summary>
        public IReadOnlyList<AircraftWeapon> Weapons => _mounted;

        /// <summary>
        /// 어느 자리든 지금 탄이 나가고 있는지. 총구 화염처럼 기체 전체에 걸리는
        /// 연출이 본다. 자리를 가려야 하는 연출은 무기에게 직접 묻는다.
        /// </summary>
        public bool IsFiring
        {
            get
            {
                foreach (AircraftWeapon weapon in _mounted)
                {
                    if (weapon.IsFiring)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            _root = AircraftRoot.Resolve(this, _root);
            _clock = TimeScale.For(this);

            if (_root == null)
            {
                Debug.LogError($"{nameof(WeaponBay)}: 기체 뿌리를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _root.GetComponentsInChildren(includeInactive: true, _mounted);
            _mounted.RemoveAll(weapon => weapon == null || weapon.Definition == null);

            foreach (AircraftWeapon weapon in _mounted)
            {
                Assign(weapon);
            }

            if (_primary == null && _secondaries.Count == 0)
            {
                Debug.LogError($"{nameof(WeaponBay)}: 기체에 무기가 하나도 없습니다.", this);
            }

            // 출격은 첫 칸을 든 채로 시작한다. 쓰지 않으면 곧 손을 놓겠지만, 처음부터
            // 빈손이면 우클릭이 왜 안 먹는지 알 길이 없다.
            _selected = _secondaries.Count > 0 ? 0 : -1;
            _holsterAt = _clock.Now + _holsterSeconds;

            _targeting = _root.Find<LockOnTargeting>();
            _input = _input != null ? _input : _root.Find<PilotInput>();

            if (_input == null)
            {
                Debug.LogError($"{nameof(WeaponBay)}: {nameof(PilotInput)}을 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            // 꺼질 때 방아쇠를 놓은 것으로 친다. 격추된 뒤 되살아났을 때
            // 눌려 있던 상태가 남아 첫 발이 저절로 나가지 않게 한다.
            foreach (AircraftWeapon weapon in _mounted)
            {
                weapon.ReleaseTrigger();
            }
        }

        private void Update()
        {
            int step = _input.WeaponCycle;
            if (step != 0)
            {
                CycleSecondary(step);
            }

            // 쓰려고 하는 동안에는 놓지 않는다. 실제로 나갔는지가 아니라 방아쇠를
            // 당기고 있는지로 본다 — 탄이 없어 안 나가는 동안에도 쓰는 중이다.
            if (_input.IsHeld(PilotAction.FireSecondary))
            {
                // 빈손이면 방아쇠가 곧 다시 드는 신호다. 들기만 하고 넘기면 놓인 줄
                // 몰랐던 사람은 첫 클릭을 잃는데, 그 한 번이 대개 가장 급한 한 번이다.
                // 여기서 들어두면 아래 Pull이 같은 프레임에 그대로 쏜다.
                if (_selected < 0)
                {
                    SelectSecondary(_lastSelected);
                }

                _holsterAt = _clock.Now + _holsterSeconds;
            }

            Pull(WeaponSlot.Primary, PilotAction.Fire);
            Pull(WeaponSlot.Secondary, PilotAction.FireSecondary);

            Holster();

            if (_input.SwitchTargetPressed && _targeting != null)
            {
                _targeting.CycleTarget();
            }
        }

        /// <summary>
        /// 실려 있는 보조무기 안에서 칸을 옮긴다. 끝에서 넘어가면 처음으로 돈다.
        /// <para>
        /// 아무것도 안 든 상태에서 돌리면 <b>놓기 전에 들고 있던 것</b>을 다시 든다.
        /// 손을 놓는 것은 편의로 일어나는 일이지 판단이 아니므로, 되돌아오는 길이
        /// 가장 짧아야 한다 — 여기서 첫 칸으로 보내면 쓰던 무기로 돌아가는 데
        /// 휠을 몇 번 더 돌려야 한다.
        /// </para>
        /// </summary>
        public void CycleSecondary(int step)
        {
            int count = _secondaries.Count;

            if (count == 0 || step == 0)
            {
                return;
            }

            if (_selected < 0)
            {
                SelectSecondary(Mathf.Clamp(_lastSelected, 0, count - 1));
                return;
            }

            if (count < 2)
            {
                // 한 자루뿐이면 옮길 데가 없다. 놓지 않도록 시간만 다시 채운다.
                _holsterAt = _clock.Now + _holsterSeconds;
                return;
            }

            // 음수에도 도는 나머지. C#의 %는 음수를 그대로 두므로 한 바퀴 더해준다.
            SelectSecondary(((_selected + step) % count + count) % count);
        }

        /// <summary>지정한 칸의 보조무기를 고른다. -1을 넣으면 손을 놓는다.</summary>
        public void SelectSecondary(int index)
        {
            index = _secondaries.Count == 0 ? -1 : Mathf.Clamp(index, -1, _secondaries.Count - 1);

            _holsterAt = _clock.Now + _holsterSeconds;

            if (index == _selected)
            {
                return;
            }

            // 넘기기 전에 놓는다. 쥔 채로 넘기면 떠난 무기가 마지막 발사 간격을
            // 안은 채 남아서, 돌아왔을 때 밀린 몫이 한꺼번에 쏟아진다.
            Secondary?.ReleaseTrigger();

            if (index >= 0)
            {
                _lastSelected = index;
            }

            _selected = index;
            SecondaryChanged?.Invoke(Secondary);
        }

        /// <summary>시간이 다 됐으면 손을 놓는다.</summary>
        private void Holster()
        {
            if (_holsterSeconds > 0f && _selected >= 0 && _clock.Now >= _holsterAt)
            {
                SelectSecondary(-1);
            }
        }

        /// <summary>이 성능 에셋으로 걸려 있는 무기. 없으면 null.</summary>
        public AircraftWeapon Mounted(WeaponDefinition definition)
        {
            foreach (AircraftWeapon weapon in _mounted)
            {
                if (weapon.Definition == definition)
                {
                    return weapon;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void ResupplyAll(float percent)
        {
            foreach (AircraftWeapon weapon in _mounted)
            {
                weapon.Ammo?.Restore(percent);
            }
        }

        /// <summary>모든 무기의 탄을 가득 채운다. 출격을 다시 시작할 때 부른다.</summary>
        public void RestockAll()
        {
            foreach (AircraftWeapon weapon in _mounted)
            {
                weapon.Ammo?.Refill();
            }
        }

        /// <summary>
        /// 누르고 있으면 당기고, 놓았으면 놓는다.
        /// <para>
        /// 놓는 쪽도 매 프레임 부른다. 눌린 순간만 보고 넘기면 손을 뗀 것이 언제인지
        /// 아는 곳이 없어져서, 무기가 마지막 발사 간격을 안은 채로 남는다.
        /// </para>
        /// </summary>
        private void Pull(WeaponSlot slot, PilotAction action)
        {
            AircraftWeapon weapon = this[slot];

            if (weapon == null)
            {
                return;
            }

            if (_input.IsHeld(action))
            {
                weapon.HoldTrigger(_clock.Delta);
            }
            else
            {
                weapon.ReleaseTrigger();
            }
        }

        /// <summary>
        /// 무기를 자기 자리에 세운다. 자리가 모자라면 먼저 온 쪽이 남는다.
        /// <para>
        /// 조용히 흘리지 않는다. 넘친 무기는 영영 나가지 않는데, 그것을 알아채는 길이
        /// "왜 이게 안 나가지"뿐이면 원인에서 한참 떨어진 곳을 뒤지게 된다.
        /// </para>
        /// </summary>
        private void Assign(AircraftWeapon weapon)
        {
            string name = weapon.Definition.DisplayName;

            if (weapon.Definition.Slot == WeaponSlot.Primary)
            {
                if (_primary != null)
                {
                    Debug.LogError(
                        $"{nameof(WeaponBay)}: 주무기 자리를 {_primary.Definition.DisplayName}와 " +
                        $"{name}가 함께 노리고 있습니다. 뒤엣것은 걸리지 않습니다.", weapon);
                    return;
                }

                _primary = weapon;
                return;
            }

            if (_secondaries.Count >= SecondaryCapacity)
            {
                Debug.LogError(
                    $"{nameof(WeaponBay)}: 보조무기는 {SecondaryCapacity}개까지입니다. " +
                    $"{name}는 걸리지 않습니다.", weapon);
                return;
            }

            _secondaries.Add(weapon);
        }
    }
}
