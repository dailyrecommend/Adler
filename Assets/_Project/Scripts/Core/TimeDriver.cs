using UnityEngine;

namespace Adler.Core
{
    /// <summary>
    /// 세상의 시계를 매 프레임 밀어주고, 그 배율을 엔진에도 알린다.
    /// <para>
    /// 씬에 놓지 않고 스스로 생긴다. 시계는 어느 씬에서든 반드시 흐르고 있어야 하는데,
    /// 놓아야만 도는 물건으로 두면 씬을 하나 만들 때마다 잊을 수 있고 잊었을 때의
    /// 증상이 "모든 것이 멈춘다"라 원인을 짚기 어렵다.
    /// </para>
    /// <para>
    /// 엔진의 <see cref="Time.timeScale"/>도 함께 맞춘다. 리지드바디는 유니티가
    /// 굴리므로 우리 시계를 모르고, 물리만 제 속도로 흐르면 늦춘 화면 위로 기체가
    /// 평소처럼 날아간다. 그래서 <b>세상 시계</b>만은 엔진과 묶어 둔다 — 매달린
    /// 시계들은 물리가 아닌 것에만 쓸 수 있다는 뜻이기도 하다.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("")]
    public sealed class TimeDriver : MonoBehaviour
    {
        private float _baseFixedDelta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            GameObject carrier = new(nameof(TimeDriver)) { hideFlags = HideFlags.HideAndDontSave };

            carrier.AddComponent<TimeDriver>();
            DontDestroyOnLoad(carrier);
        }

        private void Awake() => _baseFixedDelta = Time.fixedDeltaTime;

        private void Update()
        {
            Clock.World.Advance(Time.unscaledDeltaTime);

            float scale = Clock.World.Scale;

            Time.timeScale = scale;

            // 물리 스텝도 같은 비율로 줄인다. 그러지 않으면 늦춘 동안 스텝 수만
            // 줄어들어 기체가 뚝뚝 끊기며 움직인다.
            Time.fixedDeltaTime = _baseFixedDelta * scale;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }
    }
}
