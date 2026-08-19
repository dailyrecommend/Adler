using Adler.Flight;
using Adler.Combat;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 명중했을 때 만들어 둔 표식을 잠깐 띄운다.
    /// <para>
    /// 히트스캔은 탄이 날아가는 과정이 없어서, 표식이 없으면 맞았는지 빗나갔는지
    /// 표적이 사라질 때까지 알 수 없다. 사격을 다듬는 동안에는 이 즉각적인 확인이
    /// 조준 관용도나 탄 굵기를 판단하는 유일한 근거다.
    /// </para>
    /// <para>
    /// 표식의 모양과 위치는 건드리지 않는다. 껐다 켜고 서서히 사라지게만 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitMarker : MonoBehaviour
    {
        [Header("읽어올 대상")]
        [SerializeField] private AircraftRig _aircraft;

        private WeaponBay _bay;
        private StratagemBay _stratagemBay;


        [Header("표식")]
        [Tooltip("피해가 들어갔을 때 띄울 요소. 보통 조준점 위에 겹쳐 둔다.")]
        [SerializeField] private CanvasGroup _hitMarker;

        [Tooltip("맞혔지만 통하지 않았을 때 띄울 요소.\n" +
                 "장갑을 못 뚫었거나 철거력이 모자란 경우이며, 둘 다 무기를 바꾸라는 뜻이다.\n" +
                 "비워두면 아무것도 띄우지 않는다.")]
        [SerializeField] private CanvasGroup _blockedMarker;

        [Header("연출")]
        [Tooltip("표식이 완전히 사라지기까지의 시간(초).")]
        [Min(0.01f)]
        [SerializeField] private float _duration = 0.25f;

        private CanvasGroup _active;
        private float _remaining;

        private void Awake()
        {
            _aircraft = AircraftRig.Resolve(this, _aircraft);
            _bay = _aircraft != null ? _aircraft.Weapons : null;
            _stratagemBay = _aircraft != null ? _aircraft.Stratagems : null;

            if (_bay == null)
            {
                Debug.LogError($"{nameof(HitMarker)}: 기체의 무기를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            Hide(_hitMarker);
            Hide(_blockedMarker);
        }

        /// <summary>
        /// 손에 들지 않은 무기의 명중도 받는다. 미사일은 쏘고 나서 무기를 바꿔도
        /// 계속 날아가고, 맞았을 때 표식이 안 뜨면 어디로 갔는지 알 수 없다.
        /// </summary>
        private void OnEnable()
        {
            foreach (AircraftWeapon weapon in _bay.Weapons)
            {
                weapon.Hit += OnHit;

                if (weapon is MissileLauncher launcher)
                {
                    launcher.Detonated += OnMissileDetonated;
                }
            }

            if (_stratagemBay != null)
            {
                _stratagemBay.Detonated += OnDetonated;
            }
        }

        private void OnDisable()
        {
            foreach (AircraftWeapon weapon in _bay.Weapons)
            {
                weapon.Hit -= OnHit;

                if (weapon is MissileLauncher launcher)
                {
                    launcher.Detonated -= OnMissileDetonated;
                }
            }

            if (_stratagemBay != null)
            {
                _stratagemBay.Detonated -= OnDetonated;
            }
        }

        /// <summary>
        /// 폭발은 한 번에 여러 표적을 때린다. 하나라도 피해가 들어갔으면 명중으로 본다.
        /// 전부 막혔을 때만 막힘 표시를 띄워야, 장갑 차량 옆의 보병을 잡은 것을
        /// 실패로 알리는 일이 없다.
        /// </summary>
        private void OnDetonated(BombDefinition bomb, BlastReport report) => ShowBlast(report);

        private void OnMissileDetonated(MissileDefinition missile, BlastReport report) => ShowBlast(report);

        private void ShowBlast(BlastReport report)
        {
            if (report.MissedEverything)
            {
                return;
            }

            Show(report.Damaged > 0 ? _hitMarker : _blockedMarker);
        }

        private void OnHit(RaycastHit hit, IDamageable damaged, DamageResult result)
        {
            // 지형을 스친 것은 명중이 아니다. 표식이 아무 데서나 뜨면
            // 조준이 맞았다는 신호로서의 값어치가 사라진다.
            if (damaged == null)
            {
                return;
            }

            Show(result.Blocked ? _blockedMarker : _hitMarker);
        }

        private void Show(CanvasGroup marker)
        {
            if (marker == null)
            {
                return;
            }

            if (_active != null && _active != marker)
            {
                Hide(_active);
            }

            _active = marker;
            _active.alpha = 1f;
            _active.gameObject.SetActive(true);
            _remaining = _duration;
        }

        private void Update()
        {
            if (_active == null)
            {
                return;
            }

            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                Hide(_active);
                _active = null;
                return;
            }

            _active.alpha = _remaining / _duration;
        }

        private static void Hide(CanvasGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }
    }
}
