using Adler.Core;
using Adler.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 보조무기 한 칸. 그 칸에 실린 무기와, 지금 고른 칸인지를 보여준다.
    /// <para>
    /// 판 하나가 곧 무기 하나다. 세 판이 나란히 서서 각자 자기 무기를 보여주고,
    /// 휠을 돌리면 그중 하나가 커지며 앞으로 나온다.
    /// </para>
    /// <para>
    /// 고른 칸은 커지고 옮겨간다. 표식을 켜는 대신 칸 자체가 움직이면, 무엇이 골라졌는지가
    /// 아니라 <b>골라지는 일이 일어났다</b>는 것까지 눈에 들어온다 — 휠을 돌린 손이
    /// 화면을 보지 않고도 반응을 느낀다.
    /// </para>
    /// <para>
    /// 제자리는 인스펙터에 잡아둔 그 자리다. 세 칸을 원하는 대로 놓으신 뒤, 고른 칸이
    /// 거기서 얼마나 달라질지만 여기 적으면 된다.
    /// </para>
    /// <para>
    /// 스스로 돌지 않는다. 무엇을 꽂을지도, 매 프레임 무엇을 할지도 줄을 쥔 쪽이 정한다 —
    /// 칸마다 기체를 뒤지게 두면 세 칸이 저마다 "고른 무기"를 찾아가서, 결국 셋이
    /// 같은 무기를 그리게 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SecondarySlot : MonoBehaviour
    {
        [Header("무기")]
        [Tooltip("무기 아이콘이 들어갈 곳.")]
        [SerializeField] private Image _icon;

        [Tooltip("무기 이름.")]
        [SerializeField] private TMP_Text _nameLabel;

        [Tooltip("무기 설명. 비워둬도 된다.")]
        [SerializeField] private TMP_Text _descriptionLabel;

        [Tooltip("이 칸의 남은 탄을 발 하나하나 그릴 줄. 비워둬도 된다.")]
        [SerializeField] private RoundDisplay _rounds;

        [Header("고른 칸 알리기")]
        [Tooltip("고른 칸일 때의 크기 배율. 1이면 크기로는 알리지 않는다.\n" +
                 "인스펙터에 잡아둔 크기에 곱해지므로, 칸마다 크기가 달라도 그대로 먹는다.")]
        [Min(0.01f)]
        [SerializeField] private float _selectedScale = 1.25f;

        [Tooltip("고른 칸일 때 제자리에서 얼마나 옮길지(px).\n" +
                 "제자리는 인스펙터에 잡아둔 그 자리다.")]
        [SerializeField] private Vector2 _selectedOffset = new(0f, 12f);

        [Tooltip("옮겨가고 돌아오는 속도. 클수록 즉각적이다.\n\n" +
                 "0으로 두면 툭 바뀐다. 작게 두면 흐물거리고, 아주 크게 두면 움직였다는\n" +
                 "사실 자체가 안 보여서 크기와 자리로 알리는 뜻이 없어진다.")]
        [Min(0f)]
        [SerializeField] private float _response = 14f;

        private RectTransform _rect;
        private Clock _clock;

        // 인스펙터에서 잡아둔 제자리. 여기에 고른 몫을 얹는다. 지금 자리를 기준으로
        // 삼으면 옮겨간 자리에 또 얹혀서 한 방향으로 계속 흘러간다.
        private Vector2 _home;
        private Vector3 _homeScale;

        private bool _selected;
        private bool _placed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _clock = TimeScale.For(this);
            _home = _rect.anchoredPosition;
            _homeScale = _rect.localScale;
        }

        /// <summary>이 칸에 무기를 꽂는다. 없으면 칸째로 물러난다.</summary>
        public void Bind(AircraftWeapon weapon)
        {
            bool filled = weapon != null && weapon.Definition != null;

            if (gameObject.activeSelf != filled)
            {
                gameObject.SetActive(filled);
            }

            if (!filled)
            {
                _rounds?.Bind(null);
                return;
            }

            WeaponDefinition definition = weapon.Definition;

            if (_icon != null)
            {
                _icon.sprite = definition.Icon;
                _icon.enabled = _icon.sprite != null;
            }

            _nameLabel?.SetText(definition.DisplayName);
            _descriptionLabel?.SetText(definition.Description);

            _rounds?.Bind(weapon);
        }

        /// <summary>
        /// 고른 칸인지 알려준다.
        /// <para>
        /// 처음 한 번은 곧장 자리를 잡는다. 첫 프레임부터 기어가면 화면이 뜨는 동안
        /// 칸들이 제자리를 찾는 것이 보이는데, 그건 아무 일도 일어나지 않은 순간이다.
        /// </para>
        /// </summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;

            if (_placed)
            {
                return;
            }

            _placed = true;
            Settle(1f);
        }

        /// <summary>매 프레임 할 일. 줄을 쥔 쪽이 부른다.</summary>
        public void Refresh()
        {
            _rounds?.Refresh();
            Settle(_response <= 0f ? 1f : 1f - Mathf.Exp(-_response * _clock.Delta));
        }

        /// <summary>
        /// 제자리와 고른 자리 사이를 <paramref name="t"/>만큼 옮긴다.
        /// <para>
        /// 아직 깨어나지 않았으면 아무것도 하지 않는다. 씬에서 미리 꺼둔 칸은 Awake가
        /// 돌지 않아 제자리를 모르는데, 그런 칸은 애초에 보이지 않으므로 옮길 이유도 없다.
        /// 무기가 실려서 켜지는 순간 Awake가 돌고, 그때부터 제자리를 안다.
        /// </para>
        /// </summary>
        private void Settle(float t)
        {
            if (_rect == null)
            {
                return;
            }

            Vector2 position = _home + (_selected ? _selectedOffset : Vector2.zero);
            Vector3 scale = _homeScale * (_selected ? _selectedScale : 1f);

            _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, position, t);
            _rect.localScale = Vector3.Lerp(_rect.localScale, scale, t);
        }
    }
}
