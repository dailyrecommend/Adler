using System;
using System.Collections.Generic;
using Adler.Combat;
using Adler.Core;
using Adler.Flight;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adler.CameraRig
{
    /// <summary>
    /// 기체의 상태에 따라 화면 효과의 세기를 올리고 내린다.
    /// <para>
    /// 효과마다 컴포넌트를 두지 않는다. 부스터든 디버프든 수리든 하는 일은 하나
    /// ─ "조건이 참이면 세기를 올리고 아니면 내린다" ─ 이고 다른 것은 <b>무엇을
    /// 보느냐</b>뿐이다. 같은 기계를 조건마다 복사하면 세기를 다루는 방식을 고칠 때
    /// 세 곳을 함께 고쳐야 하고, 그중 하나를 빠뜨려도 겉으로는 드러나지 않는다.
    /// </para>
    /// <para>
    /// 그래서 효과를 늘리는 일이 클래스가 아니라 목록의 한 줄이 된다. 새 조건이
    /// 필요할 때만 <see cref="AircraftCondition"/>에 이름을 더한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftPostFx : MonoBehaviour
    {

        [Serializable]
        public struct Layer
        {
            [Tooltip("세기를 조절할 볼륨. Is Global은 꺼두고 Weight는 0에서 시작할 것.")]
            public Volume Volume;

            [Tooltip("무엇을 보고 켤지.")]
            public AircraftCondition When;

            [Tooltip("Debuff를 고른 경우에만 쓴다. 어느 디버프인지.")]
            public DebuffDefinition Debuff;

            [Tooltip("조건이 참일 때의 세기.")]
            [Range(0f, 1f)]
            public float ActiveWeight;

            [Tooltip("평상시 세기. 대개 0이다.")]
            [Range(0f, 1f)]
            public float IdleWeight;

            [Tooltip("올라오는 속도. 클수록 즉시 나타난다.")]
            [Min(0.1f)]
            public float RiseSpeed;

            [Tooltip("가라앉는 속도. 올라올 때보다 느려야 여운이 남는다.")]
            [Min(0.1f)]
            public float FallSpeed;
        }

        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        [Header("효과")]
        [SerializeField] private List<Layer> _layers = new();

        private Clock _clock;

        private void Awake()
        {
            _clock = TimeScale.For(this);
            _aircraft = AircraftRig.Resolve(this, _aircraft);

            if (_aircraft == null)
            {
                Debug.LogError($"{nameof(AircraftPostFx)}: 기체를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            // 시작할 때 평상시 값으로 맞춰둔다. 에디터에서 만지다 남은 세기가 그대로
            // 남아 있으면, 조건이 참이 되기 전까지 왜 화면이 그런지 알 수 없다.
            foreach (Layer layer in _layers)
            {
                if (layer.Volume != null)
                {
                    layer.Volume.weight = layer.IdleWeight;
                }
            }
        }

        private void Update()
        {
            foreach (Layer layer in _layers)
            {
                if (layer.Volume != null)
                {
                    Apply(in layer);
                }
            }
        }

        private void Apply(in Layer layer)
        {
            bool on = AircraftConditions.IsMet(_aircraft, layer.When, layer.Debuff);

            float target = on ? layer.ActiveWeight : layer.IdleWeight;
            float speed = on ? layer.RiseSpeed : layer.FallSpeed;

            layer.Volume.weight = Mathf.Lerp(
                layer.Volume.weight, target, 1f - Mathf.Exp(-speed * _clock.Delta));
        }
    }
}
