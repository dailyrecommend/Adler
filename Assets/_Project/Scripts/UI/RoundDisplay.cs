using System.Collections.Generic;
using Adler.Weapons;
using UnityEngine;

namespace Adler.UI
{
    /// <summary>
    /// 남은 탄을 발 하나하나 그림으로 늘어놓는다.
    /// <para>
    /// 눈금 하나로 뭉뚱그리지 않는다. 보조무기는 서너 발뿐이라 한 발이 곧 한 판단인데,
    /// 게이지로 두면 "지금 두 발"이 "가운데쯤"으로 보여서 셀 수가 없다. 발수가 적을수록
    /// 세는 편이 낫고, 많아지면 반대다.
    /// </para>
    /// <para>
    /// 차오르는 중인 한 발만 반쯤 칠해진다. 쏠 수 있는 발은 가득, 아직 안 온 발은 비어
    /// 있으니, 줄 전체가 곧 "지금 몇 발이고 다음 발이 얼마나 왔나"다.
    /// </para>
    /// <para>
    /// 어느 무기의 탄인지는 묻지 않는다. 꽂아주는 쪽이 정한다 — 스스로 "고른 무기"를
    /// 찾아가게 두면 칸마다 하나씩 두었을 때 세 줄이 전부 같은 무기를 그린다.
    /// </para>
    /// <para>
    /// 발 그림은 찍어낸다. 발수가 무기마다 다르므로 손으로 놓아두면 무기를 바꿀 때마다
    /// 배치를 다시 해야 한다 — 이 줄은 가로로 늘어놓기만 하면 되는 자리라 찍어내도
    /// 배치가 코드에 매이지 않는다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundDisplay : MonoBehaviour
    {
        /// <summary>
        /// 이만큼 넘게 실은 무기는 발을 세지 않는다.
        /// <para>
        /// 기총처럼 천 발짜리를 이 방식으로 그리면 천 개를 찍어내게 된다. 그런 무기는
        /// 애초에 세는 물건이 아니라 게이지로 볼 물건이다.
        /// </para>
        /// </summary>
        private const int MaxRounds = 16;

        [Tooltip("발 하나를 그릴 프리팹. 이 오브젝트 아래에 발수만큼 찍어낸다.\n\n" +
                 "늘어놓는 방식은 이 오브젝트에 Horizontal Layout Group 같은 것을\n" +
                 "붙여서 정한다 — 어떻게 놓을지는 여기서 정하지 않는다.")]
        [SerializeField] private RoundSlot _roundPrefab;

        private AmmoStock _stock;
        private readonly List<RoundSlot> _rounds = new();

        private void Awake()
        {
            if (_roundPrefab == null)
            {
                Debug.LogError($"{nameof(RoundDisplay)}: 발 프리팹이 비어 있습니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 어느 무기의 탄을 그릴지 정한다. 무기가 바뀔 때만 부른다.
        /// <para>
        /// 찍어낸 것은 지우지 않고 재워둔다. 무기를 바꿀 때마다 만들고 부수면 바꾸는
        /// 내내 쓰레기가 쌓이는데, 재워두면 가장 많이 실은 무기만큼만 만들고 끝난다.
        /// </para>
        /// </summary>
        public void Bind(AircraftWeapon weapon)
        {
            _stock = weapon != null ? weapon.Ammo : null;

            if (_stock == null || weapon.Definition == null)
            {
                Sleep(0);
                return;
            }

            int capacity = _stock.Capacity;

            if (capacity > MaxRounds)
            {
                Debug.LogWarning(
                    $"{nameof(RoundDisplay)}: {weapon.Definition.DisplayName}는 {capacity}발이라 " +
                    $"발을 세기에 너무 많습니다. {MaxRounds}발까지만 그립니다.", this);

                capacity = MaxRounds;
            }

            while (_rounds.Count < capacity)
            {
                _rounds.Add(Instantiate(_roundPrefab, transform));
            }

            for (int i = 0; i < capacity; i++)
            {
                _rounds[i].gameObject.SetActive(true);
                _rounds[i].SetPicture(weapon.Definition.Picture);
            }

            Sleep(capacity);
        }

        /// <summary>
        /// 발이 찬 정도를 넣어준다. 꽂아준 쪽이 매 프레임 부른다.
        /// <para>
        /// 차오르는 것은 알림으로 오지 않으므로 직접 읽는 수밖에 없고, 어차피 그 값이
        /// 매끄럽게 흐르는 것이 이 줄이 하는 일이다.
        /// </para>
        /// </summary>
        public void Refresh()
        {
            if (_stock == null)
            {
                return;
            }

            int shown = Mathf.Min(_stock.Capacity, _rounds.Count);

            for (int i = 0; i < shown; i++)
            {
                _rounds[i].SetFill(FillAt(i));
            }
        }

        /// <summary>
        /// 쏠 수 있는 발은 가득, 그다음 한 발은 차오르는 만큼, 나머지는 비어 있다.
        /// </summary>
        private float FillAt(int index)
        {
            if (index < _stock.Remaining)
            {
                return 1f;
            }

            return index == _stock.Remaining ? _stock.Progress : 0f;
        }

        /// <summary>이 번호부터 뒤는 재운다.</summary>
        private void Sleep(int from)
        {
            for (int i = from; i < _rounds.Count; i++)
            {
                if (_rounds[i].gameObject.activeSelf)
                {
                    _rounds[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
