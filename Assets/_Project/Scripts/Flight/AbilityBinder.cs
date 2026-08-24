using System;
using System.Collections.Generic;
using Adler.Abilities;
using Adler.Core;
using Adler.Controls;
using Adler.Weapons;
using UnityEngine;

namespace Adler.Flight
{
    /// <summary>
    /// 이 기체가 행동을 쓰는 방식. 통로를 끼워 넣고, 어느 조작이 무엇을 부르는지 잇는다.
    /// <para>
    /// 잇는 일을 코드가 아니라 목록으로 두는 이유는, 그래야 행동을 늘릴 때 고칠 파일이
    /// 없기 때문이다. 새 행동은 스펙 에셋 하나와 여기 한 줄이면 붙고, 어느 키가 무엇을
    /// 하는지도 인스펙터에서 한눈에 보인다.
    /// </para>
    /// <para>
    /// 무엇을 할 수 있는지는 실행기가, 무엇을 하려는지는 입력이 안다. 이쪽은 둘을
    /// 마주 놓기만 하고 어느 쪽의 판단도 대신하지 않는다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(AbilityRunner))]
    [DisallowMultipleComponent]
    public sealed class AbilityBinder : MonoBehaviour
    {
        /// <summary>조작을 어떻게 받아들일지.</summary>
        public enum Style
        {
            /// <summary>누르는 순간 한 번 시작한다. 폭탄 투하처럼.</summary>
            Press,

            /// <summary>누르고 있는 동안 이어간다. 놓으면 끝난다. 기총처럼.</summary>
            Hold,

            /// <summary>누를 때마다 켜고 끈다. 그래플처럼.</summary>
            Toggle,
        }

        [Serializable]
        public struct Binding
        {
            [Tooltip("어느 조작으로.")]
            public PilotAction Action;

            [Tooltip("무엇을 시작할지.")]
            public AbilitySpec Ability;

            [Tooltip("어떻게 받아들일지.")]
            public Style Style;
        }

        [Header("참조")]
        [Tooltip("행동이 쓸 부품을 꺼내올 기체. 비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] private AircraftRig _aircraft;

        [Tooltip("비워두면 이 기체에서 찾는다.")]
        [SerializeField] private PilotInput _input;

        [Header("연결")]
        [SerializeField] private List<Binding> _bindings = new();

        private AbilityRunner _runner;

        private void Awake()
        {
            _runner = GetComponent<AbilityRunner>();
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _input = _input != null ? _input : GetComponentInParent<PilotInput>();

            if (_input == null || _aircraft == null)
            {
                Debug.LogError($"{nameof(AbilityBinder)}: 입력 또는 기체를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            // 행동이 세상에 닿는 통로를 여기서 끼워 넣는다. 실행기가 스스로
            // 찾게 두면 그쪽이 기체의 구성을 알아야 하고, 그러면 같은 실행기를
            // 다른 것 위에 얹을 수 없다.
            _runner.Context = new AbilityContext(
                _aircraft.gameObject,
                TimeScale.For(this),
                _aircraft.Model as IMovementDriver,
                _aircraft.Weapons,
                _aircraft.Targeting);
        }

        private void Update()
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Ability != null)
                {
                    Apply(in binding);
                }
            }
        }

        private void Apply(in Binding binding)
        {
            switch (binding.Style)
            {
                case Style.Press when _input.WasPressed(binding.Action):
                    _runner.TryUse(binding.Ability);
                    break;

                case Style.Hold:
                    Hold(in binding);
                    break;

                case Style.Toggle when _input.WasPressed(binding.Action):
                    Toggle(in binding);
                    break;
            }
        }

        /// <summary>
        /// 누르고 있는 동안 이어간다.
        /// <para>
        /// 놓았을 때 <b>이 행동이 돌고 있을 때만</b> 멈춘다. 그냥 멈추면 손을 뗀 김에
        /// 남이 시작한 행동까지 끊게 된다.
        /// </para>
        /// </summary>
        private void Hold(in Binding binding)
        {
            bool held = _input.IsHeld(binding.Action);
            bool mine = _runner.Running?.Spec == binding.Ability;

            if (held && !mine)
            {
                _runner.TryUse(binding.Ability);
            }
            else if (!held && mine)
            {
                _runner.Stop();
            }
        }

        private void Toggle(in Binding binding)
        {
            if (_runner.Running?.Spec == binding.Ability)
            {
                _runner.Stop();
                return;
            }

            _runner.TryUse(binding.Ability);
        }
    }
}
