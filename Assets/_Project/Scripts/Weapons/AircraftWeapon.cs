using System;
using Adler.Core;
using Adler.Combat;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기체가 들고 쏘는 것들의 공통 기반.
    /// <para>
    /// 방아쇠를 직접 읽지 않는다. 어느 자리의 방아쇠가 당겨졌는지는 <see cref="WeaponBay"/>가
    /// 알고, 여기서는 "쏘라"는 요청이 왔을 때 쏠 수 있는지 판단하고 쏜다. 무기가 각자
    /// 입력을 읽으면 두 자리가 같은 키를 물고 늘어지는 일이 생긴다.
    /// </para>
    /// <para>
    /// 탄도 무기마다 따로 갖는다. 한 통에 담으면 기총을 갈긴 대가로 미사일이 없어져서,
    /// 자리를 나눈 뜻이 사라진다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class AircraftWeapon : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 위로 거슬러 올라가 찾는다.")]
        [SerializeField] protected AircraftRoot _root;

        [Tooltip("발사 위치. 여러 개면 번갈아 쓴다. 비어 있으면 이 오브젝트에서 나간다.")]
        [SerializeField] protected Transform[] _muzzles = Array.Empty<Transform>();

        [Header("판정")]
        [Tooltip("맞을 레이어. 기체 자신이 속한 레이어는 빼야 자기 무기에 맞지 않는다.")]
        [SerializeField] protected LayerMask _hitMask = ~0;

        private float _firingFor;
        private float _cooldown;
        private int _nextMuzzle;
        private Clock _clock;

        /// <summary>이 무기가 쓰는 성능 에셋.</summary>
        public abstract WeaponDefinition Definition { get; }

        /// <summary>
        /// 이 무기가 쥐고 있는 탄. 남은 발수도 차오르는 정도도 여기서 묻는다.
        /// <para>
        /// 얇게 감싸 다시 내주지 않는다. 남은 발수를 두 곳에서 답하게 두면 언젠가
        /// 한쪽만 고쳐지고, 그때 화면과 무기가 서로 다른 말을 하게 된다.
        /// </para>
        /// </summary>
        public AmmoStock Ammo { get; private set; }

        /// <summary>
        /// 발사할 때마다. 총구 화염과 소리가 구독한다. (쏜 무기, 발사 위치, 방향)
        /// <para>
        /// 쏜 무기를 함께 보낸다. 듣는 쪽이 무기고에게 "지금 든 것이 무엇이냐"고
        /// 되물으면 두 자리가 동시에 나가는 순간 답이 하나뿐이라 틀린다.
        /// </para>
        /// </summary>
        public event Action<AircraftWeapon, Vector3, Vector3> Fired;

        /// <summary>무언가를 맞혔을 때. 화면 표시가 구독한다.</summary>
        public event Action<RaycastHit, IDamageable, DamageResult> Hit;

        /// <summary>
        /// 지금 쏠 수 있는지. 탄 외에 조건이 있는 무기는 여기에 얹는다 —
        /// 미사일은 조준이 걸려 있어야 한다.
        /// </summary>
        public virtual bool CanFire => Ammo != null && Ammo.CanSpend;

        /// <summary>
        /// 지금 탄이 나가고 있는지. 총구 화염 같은 이어지는 연출이 이것을 본다.
        /// <para>
        /// 방아쇠가 당겨져 있는지와 다르다. 탄이 없거나 조준이 안 걸린 동안에도
        /// 방아쇠는 당겨져 있는데, 그때 총구가 계속 타오르면 화면이 나가지도 않은
        /// 탄을 쏘고 있다고 말하게 된다.
        /// </para>
        /// <para>
        /// 한 발 나갈 때마다 다음 발까지 버틸 만큼만 켜 둔다. 나가는 순간에만 참이면
        /// 연사 중에 매 발 꺼졌다 켜져서 깜빡이고, 방아쇠를 놓을 때까지 켜 두면
        /// 마지막 탄을 쏘고 나서도 남는다.
        /// </para>
        /// </summary>
        public bool IsFiring => _firingFor > 0f;

        protected virtual void Awake()
        {
            _root = AircraftRoot.Resolve(this, _root);
            _clock = TimeScale.For(this);

            if (Definition == null)
            {
                Debug.LogError($"{GetType().Name}: 성능 에셋이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            Ammo = new AmmoStock(Definition);
        }

        /// <summary>
        /// 탄은 쏘지 않는 동안에도 차오른다. 그래서 방아쇠와 무관하게 매 프레임 돈다.
        /// <para>
        /// 늦춰진 시계로 센다. 시간이 멎은 동안에도 탄이 차면 멎었다고 할 수 없고,
        /// 느려진 적기의 무기가 제 속도로 재장전되면 늦춘 뜻이 반쯤 사라진다.
        /// </para>
        /// </summary>
        protected virtual void Update() => Ammo?.Advance(_clock.Delta);

        /// <summary>
        /// 방아쇠가 당겨져 있는 동안 매 프레임 불린다. 발사 간격은 여기서 지킨다.
        /// </summary>
        public void HoldTrigger(float deltaTime)
        {
            _firingFor = Mathf.Max(0f, _firingFor - deltaTime);
            _cooldown -= deltaTime;

            OnTriggerHeld(deltaTime);

            if (_cooldown > 0f)
            {
                return;
            }

            if (!CanFire || !Ammo.TrySpend())
            {
                // 못 쏘는 동안 간격이 쌓이지 않게 한다. 조건이 풀리는 순간
                // 밀린 몫이 한꺼번에 쏟아지면 무기가 폭주한 것처럼 보인다.
                _cooldown = 0f;
                return;
            }

            FireOnce();
            _cooldown += Definition.ShotInterval;
            _firingFor = FiringTail;
        }

        /// <summary>
        /// 한 발 쏜 뒤 "쏘는 중"으로 남아 있는 시간(초).
        /// <para>
        /// 발사 간격보다 조금 길게 잡아 연사 중에 끊기지 않게 하되, 위로 한계를 둔다.
        /// 간격에만 비례시키면 몇 초에 한 발 나가는 발사기가 쏘고 나서 몇 초 동안
        /// 계속 쏘는 중이 되는데, 그런 무기는 한 발마다 번쩍하는 편이 맞다.
        /// </para>
        /// </summary>
        private float FiringTail => Mathf.Min(Definition.ShotInterval * 1.5f, 0.2f);

        /// <summary>방아쇠를 놓았을 때. 짧게 끊어 쏴도 첫 발이 즉시 나가게 한다.</summary>
        public void ReleaseTrigger()
        {
            _cooldown = Mathf.Min(_cooldown, 0f);

            // 놓는 순간 바로 끈다. 여기서 꼬리를 남기면 톡 눌렀을 때 총구가
            // 손을 뗀 뒤에도 타올라, 끊어 쏘는 것이 화면에 끊어져 보이지 않는다.
            _firingFor = 0f;
            OnTriggerReleased();
        }

        /// <summary>
        /// 방아쇠가 당겨져 있는 동안 매 프레임, 쏘기 전에 불린다. 간격과 탄에 막혀
        /// 한 발도 못 내보내는 프레임에도 당기고 있다는 것은 참이므로 불린다.
        /// <para>
        /// 쏘는 것 말고도 방아쇠에 반응해야 하는 무기가 얹는다 — 갈고리는 걸려 있는
        /// 동안의 두 번째 누름을 끊으라는 뜻으로 읽는다.
        /// </para>
        /// </summary>
        protected virtual void OnTriggerHeld(float deltaTime) { }

        /// <summary>방아쇠가 놓여 있는 동안. 놓인 채로도 매 프레임 불릴 수 있다.</summary>
        protected virtual void OnTriggerReleased() { }

        /// <summary>한 발을 실제로 내보낸다.</summary>
        protected abstract void FireOnce();

        protected void RaiseFired(Vector3 origin, Vector3 direction) => Fired?.Invoke(this, origin, direction);

        protected void RaiseHit(RaycastHit hit, IDamageable damaged, DamageResult result)
            => Hit?.Invoke(hit, damaged, result);

        protected Transform ResolveMuzzle()
        {
            if (_muzzles.Length == 0)
            {
                return transform;
            }

            Transform muzzle = _muzzles[_nextMuzzle];
            _nextMuzzle = (_nextMuzzle + 1) % _muzzles.Length;
            return muzzle != null ? muzzle : transform;
        }

        /// <summary>기체가 움직이는 만큼 발사체에 얹어줄 속도.</summary>
        protected Vector3 CarrierVelocity =>
            _root != null && _root.Body != null ? _root.Body.linearVelocity : Vector3.zero;

        protected GameObject Owner => _root != null ? _root.gameObject : gameObject;
    }
}
