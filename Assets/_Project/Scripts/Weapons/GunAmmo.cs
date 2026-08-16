using System;
using UnityEngine;

namespace Adler.Weapons
{
    /// <summary>
    /// 기총의 탄약. 재보급 스트라타젬이 승인되면 채워진다.
    /// <para>
    /// 재장전을 그냥 기다리는 시간으로 두지 않고 커맨드 입력으로 만든 이유가 있다.
    /// 탄이 떨어졌을 때 <em>지금 손을 뗄 수 있는가</em>를 판단해야 하므로, 적진 한가운데서
    /// 탄이 바닥나는 것이 실제 위험이 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunAmmo : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("장탄수를 읽어올 총. 비워두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private AircraftGun _gun;

        [Tooltip("재보급 승인을 받아올 곳. 비워두면 부모에서 찾는다.")]
        [SerializeField] private StratagemBay _stratagemBay;

        private int _capacity;
        private int _remaining;
        private float _resupplyReadyAt;
        private int _resuppliesUsed;

        /// <summary>남은 탄이 바뀔 때마다. 화면 표시가 구독한다.</summary>
        public event Action<GunAmmo> Changed;

        /// <summary>탄이 바닥났을 때 한 번.</summary>
        public event Action<GunAmmo> Emptied;

        /// <summary>재보급이 이루어졌을 때.</summary>
        public event Action<GunAmmo> Resupplied;

        public int Remaining => _remaining;

        public int Capacity => _capacity;

        public float Normalized => _capacity > 0 ? (float)_remaining / _capacity : 0f;

        public bool IsEmpty => _remaining <= 0;

        private void Awake()
        {
            if (_gun == null)
            {
                _gun = GetComponent<AircraftGun>();
            }

            if (_stratagemBay == null)
            {
                _stratagemBay = GetComponentInParent<StratagemBay>();
            }

            // 장탄수는 총의 성능이지 이 컴포넌트의 설정이 아니다. 에셋에서 읽어야
            // 정비로 총을 바꾸거나 탄량을 올렸을 때 그대로 따라온다.
            if (_gun == null || _gun.Definition == null)
            {
                Debug.LogError($"{nameof(GunAmmo)}: 장탄수를 읽을 총을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            _capacity = _gun.Definition.AmmoCapacity;
            _remaining = _capacity;
        }

        private void OnEnable()
        {
            if (_stratagemBay != null)
            {
                _stratagemBay.Authorized += OnAuthorized;
            }
        }

        private void OnDisable()
        {
            if (_stratagemBay != null)
            {
                _stratagemBay.Authorized -= OnAuthorized;
            }
        }

        /// <summary>한 발을 쓴다. 남은 탄이 없으면 false.</summary>
        public bool TryConsume()
        {
            if (_remaining <= 0)
            {
                return false;
            }

            _remaining--;
            Changed?.Invoke(this);

            if (_remaining == 0)
            {
                Emptied?.Invoke(this);
            }

            return true;
        }

        private void OnAuthorized(StratagemDefinition stratagem)
        {
            if (stratagem is not ResupplyDefinition resupply)
            {
                return;
            }

            if (!CanResupply(resupply))
            {
                return;
            }

            int amount = resupply.Rounds > 0 ? resupply.Rounds : _capacity;
            _remaining = Mathf.Min(_capacity, _remaining + amount);

            _resuppliesUsed++;
            _resupplyReadyAt = Time.time + resupply.Cooldown;

            Changed?.Invoke(this);
            Resupplied?.Invoke(this);
        }

        /// <summary>
        /// 재보급을 부를 수 있는지. 막혀 있어도 커맨드 자체는 통과시킨다 —
        /// 입력이 맞았는데 아무 반응이 없는 것과, 승인은 됐지만 보급이 안 되는 것은
        /// 플레이어에게 다른 정보다.
        /// </summary>
        public bool CanResupply(ResupplyDefinition resupply)
        {
            if (resupply.Cooldown > 0f && Time.time < _resupplyReadyAt)
            {
                return false;
            }

            return resupply.UsesPerSortie <= 0 || _resuppliesUsed < resupply.UsesPerSortie;
        }

        /// <summary>출격을 다시 시작할 때 되돌린다.</summary>
        public void Restock()
        {
            _remaining = _capacity;
            _resuppliesUsed = 0;
            _resupplyReadyAt = 0f;
            Changed?.Invoke(this);
        }
    }
}
