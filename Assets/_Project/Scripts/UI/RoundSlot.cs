using UnityEngine;
using UnityEngine.UI;

namespace Adler.UI
{
    /// <summary>
    /// 탄 한 발. 그 한 발이 지금 얼마나 차 있는지를 그림으로 보여준다.
    /// <para>
    /// 채워진 그림 하나와 그 뒤에 깔리는 흐린 그림 하나로 이뤄진다. 뒤가 없으면
    /// 다 쓴 발이 아예 사라져서, 남은 발수는 알겠지만 <b>몇 발짜리 무기인지</b>를
    /// 알 수 없다 — 두 발 중 한 발과 네 발 중 한 발은 다른 상황이다.
    /// </para>
    /// <para>
    /// 스스로 돌지 않는다. 채움량은 줄을 쥔 쪽이 넣어준다 — 발마다 탄을 뒤지게 두면
    /// 같은 값을 발수만큼 되풀이해 읽게 된다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundSlot : MonoBehaviour
    {
        [Tooltip("차오르는 쪽. Image Type을 Filled로 둘 것.")]
        [SerializeField] private Image _fill;

        [Tooltip("뒤에 깔리는 흐린 쪽. 다 쓴 발이 있던 자리를 남긴다. 비워둬도 된다.")]
        [SerializeField] private Image _backdrop;

        /// <summary>이 발에 쓸 그림을 꽂는다. 무기가 바뀔 때만 부른다.</summary>
        public void SetPicture(Sprite picture)
        {
            if (_fill != null)
            {
                _fill.sprite = picture;
            }

            if (_backdrop != null)
            {
                _backdrop.sprite = picture;
            }
        }

        /// <summary>이 발이 찬 정도(0~1). 1이면 쏠 수 있는 한 발이다.</summary>
        public void SetFill(float amount)
        {
            if (_fill != null)
            {
                _fill.fillAmount = amount;
            }
        }
    }
}
