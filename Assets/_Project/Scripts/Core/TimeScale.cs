using System.Collections.Generic;
using UnityEngine;

namespace Adler.Core
{
    /// <summary>
    /// 이 오브젝트와 그 아래에 붙은 것들이 쓸 시계를 만든다.
    /// <para>
    /// 붙이지 않으면 세상 시계를 쓴다. 붙이는 순간 그 아래는 자기만의 속도를 갖는다 —
    /// 적기에 하나 달고 배율을 0.3으로 두면 그 기체만 느려진다.
    /// </para>
    /// <para>
    /// 물리는 따라오지 않는다. 리지드바디는 유니티가 굴리고 그쪽 시간은 하나뿐이라,
    /// 여기서 늦출 수 있는 것은 우리가 직접 세는 것들 — 쿨다운, 조준이 차오르는 시간,
    /// 사격 간격 같은 것들이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeScale : MonoBehaviour
    {
        [Tooltip("이 아래가 흐르는 속도. 1이면 바깥과 같고, 0.5면 절반으로 느리다.\n" +
                 "0이면 이 아래의 시간이 멎는다. 부모에 또 다른 시계가 있으면 그쪽 배율에 곱해진다.")]
        [Range(0f, 4f)]
        [SerializeField] private float _scale = 1f;

        /// <summary>
        /// 지금 살아 있는 시계들. 시간을 건드리는 것이 무엇을 늦출지 찾을 때 쓴다.
        /// <para>
        /// 늦출 대상의 목록을 손으로 들고 있으면 적을 하나 새로 만들 때 그것을 넣는
        /// 것을 잊게 되고, 그때 증상이 "멈춘 세상에서 저놈만 날아다닌다"라 눈에
        /// 띄기는 하지만 원인이 멀리 있다.
        /// </para>
        /// <para>
        /// 여기 오르는 것은 <b>시계를 가진 것들뿐</b>이다. 늦추고 싶은 것에는 이
        /// 컴포넌트가 붙어 있어야 한다는 뜻이고, 그게 이 방식이 치르는 값이다.
        /// </para>
        /// </summary>
        public static IReadOnlyList<TimeScale> All => Registry<TimeScale>.All;

        private Clock _clock;

        /// <summary>이 오브젝트 아래가 쓰는 시계.</summary>
        public Clock Clock => _clock ??= new Clock(ParentClock());

        /// <summary>
        /// 이 컴포넌트가 맡는 시간 배율. 디버프나 스킬이 실행 중에 바꿔도 된다.
        /// </summary>
        public float Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                Clock.LocalScale = value;
            }
        }

        /// <summary>
        /// 이 컴포넌트가 써야 할 시계를 찾아준다.
        /// <para>
        /// 위로 거슬러 올라가며 가장 가까운 것을 쓰고, 없으면 세상 시계를 쓴다. 그래서
        /// 시간을 세는 쪽은 자기가 누구에게 매달렸는지 몰라도 되고, 나중에 시계를
        /// 하나 끼워 넣어도 코드를 고칠 일이 없다.
        /// </para>
        /// </summary>
        public static Clock For(Component component)
        {
            TimeScale owner = component.GetComponentInParent<TimeScale>(includeInactive: true);

            return owner != null ? owner.Clock : Core.Clock.World;
        }

        /// <summary>
        /// 자기를 내보낸 쪽의 시계를 물려받는다.
        /// <para>
        /// 탄이나 폭탄처럼 부모 없이 태어나는 것이 쓴다. 자기 자리에서 찾으면 매달린
        /// 데가 없으니 늘 세상 시계가 되어, 쏜 놈이 아무리 늦춰져 있어도 탄만 제 속도로
        /// 날아간다. 시간이 멎은 적이 쏜 탄이 평소처럼 날아오면 멎었다고 할 수 없다.
        /// </para>
        /// </summary>
        public static Clock Inherited(GameObject owner, Component self)
            => owner != null ? For(owner.transform) : For(self);

        private void OnEnable() => Registry<TimeScale>.Add(this);

        private void OnDisable() => Registry<TimeScale>.Remove(this);

        private void Awake() => Clock.LocalScale = _scale;

        private Clock ParentClock()
        {
            Transform above = transform.parent;
            TimeScale owner = above != null
                ? above.GetComponentInParent<TimeScale>(includeInactive: true)
                : null;

            return owner != null ? owner.Clock : Core.Clock.World;
        }

#if UNITY_EDITOR
        /// <summary>인스펙터에서 배율을 돌리는 동안 바로 반영한다.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying && _clock != null)
            {
                _clock.LocalScale = _scale;
            }
        }
#endif
    }
}
