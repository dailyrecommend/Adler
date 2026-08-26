using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 갈고리를 무기 체계 위에서 굴린다.
    /// <para>
    /// 얇은 것이 맞다. 줄의 물리와 단계는 <see cref="GrapplingHook"/>의 것이고, 여기는
    /// 무기의 언어 — 방아쇠·탄 — 를 그쪽의 언어 — 던짐·놓음 — 로 옮길 뿐이다.
    /// 여기에 줄의 사정이 스며들면 같은 규칙이 두 파일로 갈라진다.
    /// </para>
    /// <para>
    /// 방아쇠는 토글로 읽는다. 눌러서 던지고, 걸려 있는 동안 다시 누르면 끊는다.
    /// 줄은 스스로도 끊어진다 — 다 감기거나, 너무 멀어지거나, 시간이 다 되거나.
    /// 어느 길로 끊어졌든 무기는 다음 발이 차오르기를 기다릴 뿐이다.
    /// </para>
    /// <para>
    /// Fired를 울리지 않는다. 그 신호는 사격음이 듣는데, 갈고리의 소리는 이미 줄의
    /// 단계에 붙어 있다 — 여기서도 울리면 던질 때마다 기총 소리가 함께 난다.
    /// </para>
    /// </summary>
    public sealed class GrappleWeapon : AircraftWeapon
    {
        [Header("갈고리")]
        [Tooltip("몰고 갈 갈고리. 비워두면 이 기체에서 찾는다.")]
        [SerializeField] private GrapplingHook _hook;

        // 이번 누름의 첫 프레임이 지났는지. 두 번째 누름을 가려내는 데 쓴다.
        private bool _wasHeld;

        // 이번 누름이 이미 끊는 일을 했는지. 끊은 그 누름으로 곧장 다시 던지면
        // 토글이 아니라 갈아타기가 된다.
        private bool _spent;

        /// <summary>꽂힌 에셋을 갈고리의 말로 읽을 일은 아직 없다. 종류만 지킨다.</summary>
        protected override bool Accepts(WeaponDefinition definition) => definition is GrappleDefinition;

        /// <summary>탄이 있어도 줄이 나가 있으면 못 던진다. 줄은 하나뿐이다.</summary>
        public override bool CanFire => base.CanFire && !_spent && _hook != null && _hook.CanThrow;

        protected override void Awake()
        {
            base.Awake();

            if (_hook == null && _root != null)
            {
                _hook = _root.Find<GrapplingHook>();
            }

            if (_hook == null)
            {
                Debug.LogError($"{nameof(GrappleWeapon)}: 몰고 갈 {nameof(GrapplingHook)}을 찾지 못했습니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 누름의 첫 프레임에 끊을지 본다. 줄이 나가 있으면 이 누름은 끊는 누름이고,
        /// 같은 누름으로 다시 던지지는 않는다.
        /// </summary>
        protected override void OnTriggerHeld(float deltaTime)
        {
            if (_wasHeld)
            {
                return;
            }

            _wasHeld = true;

            if (_hook != null && _hook.Phase != GrapplePhase.Idle)
            {
                _hook.Release();
                _spent = true;
            }
        }

        protected override void OnTriggerReleased()
        {
            _wasHeld = false;
            _spent = false;
        }

        protected override void FireOnce() => _hook.Fire();
    }
}
