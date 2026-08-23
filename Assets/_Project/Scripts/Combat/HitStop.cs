using System.Collections;
using UnityEngine;

namespace Adler.Combat
{
    /// <summary>
    /// 한순간 시간을 늦춘다. 결정적인 한 방이 지나가는 것을 눈에 붙잡아 둔다.
    /// <para>
    /// 완전히 멈추지 않고 늦추기만 하는 이유는, 화면이 뚝 멈추면 정지 화면처럼
    /// 보이고 소리도 끊긴다. 느려진 채로 짧게 흐르면 타격감으로 읽힌다.
    /// </para>
    /// <para>
    /// 물리 스텝도 같은 비율로 늦춘다. 그러지 않으면 늦춰진 시간 동안 리지드바디는
    /// 평소 속도로 계속 계산되어, 화면은 느린데 움직임은 그대로인 채로 어긋난다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitStop : MonoBehaviour
    {
        private float _baseFixedDeltaTime;
        private float _remaining;
        private Coroutine _routine;

        private void Awake() => _baseFixedDeltaTime = Time.fixedDeltaTime;

        /// <summary>
        /// 시간을 늦춘다. 이미 늦춰진 채라면 남은 시간이 더 긴 쪽을 따른다 — 격추처럼
        /// 강한 반응이 스쳐 가는 명중 위에 덮여 짧게 끊기면 안 되기 때문이다.
        /// </summary>
        public void Trigger(float duration, float scale)
        {
            if (duration <= 0f || duration < _remaining)
            {
                return;
            }

            _remaining = duration;

            Time.timeScale = Mathf.Clamp01(scale);
            Time.fixedDeltaTime = _baseFixedDeltaTime * Time.timeScale;

            if (_routine == null)
            {
                _routine = StartCoroutine(CountDown());
            }
        }

        private IEnumerator CountDown()
        {
            while (_remaining > 0f)
            {
                _remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            Restore();
            _routine = null;
        }

        private void Restore()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDeltaTime;
            _remaining = 0f;
        }

        private void OnDisable()
        {
            // 꺼질 때 시간이 늦춰진 채로 남으면 게임 전체가 멈춘 것처럼 보인다.
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            Restore();
        }
    }
}
