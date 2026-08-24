using System;

namespace Adler.Combat
{
    /// <summary>
    /// 명중 한 번의 무게.
    /// <para>
    /// 맞혔다는 사실만으로는 어떤 반응이 어울리는지 정할 수 없다. 기총 한 발과
    /// 미사일 한 발은 같은 "명중"이지만, 둘에 같은 연출을 주면 한쪽은 과하고
    /// 다른 쪽은 밋밋해진다.
    /// </para>
    /// </summary>
    public enum ImpactWeight
    {
        /// <summary>기총 한 발이 스친 정도. 자주 일어난다.</summary>
        Light,

        /// <summary>폭발이 표적을 덮었다. 드물고, 한 발이 곧 한 사건이다.</summary>
        Blast,

        /// <summary>표적이 쓰러졌다. 이 게임에서 가장 값진 순간.</summary>
        Kill,

        /// <summary>
        /// 몸으로 들이받았다. 거리가 0이 되어야 일어나는 일이라, 무게가 아니라
        /// 거기까지 파고들었다는 사실이 값이다.
        /// </summary>
        Ram,
    }

    /// <summary>
    /// 손맛 신호가 지나가는 통로. 때린 쪽과 맞은 쪽 모두 여기로 알린다.
    /// <para>
    /// 정적인 이유는 무게를 재는 쪽이 화면·소리·카메라를 몰라야 하기 때문이다.
    /// 하나하나 이어주면 반응을 붙일 때마다 재는 쪽이 그것을 알아야 하고, 반응들은
    /// 재는 쪽이 어느 계층에 사는지까지 알아야 한다 — 그 앎이 카메라와 소리가
    /// 화면 계층을 올려다보는 역류를 만들었다.
    /// </para>
    /// <para>
    /// 구독한 쪽은 반드시 OnDisable에서 끊어야 한다. 이 통로는 씬이 바뀌어도
    /// 살아 있어서, 끊지 않으면 사라진 반응이 계속 불려 나온다.
    /// </para>
    /// </summary>
    public static class ImpactChannel
    {
        /// <summary>때렸다. 무게를 재는 쪽이 알린다.</summary>
        public static event Action<ImpactWeight> Landed;

        /// <summary>
        /// 때리며 멈춰뒀던 시간이 다시 흐르기 시작했다.
        /// 시간이 멎지 않는 가벼운 명중에는 이 순간이 없다.
        /// </summary>
        public static event Action<ImpactWeight> Released;

        /// <summary>맞았다. 잃은 내구도의 비율(0~1)이 함께 온다.</summary>
        public static event Action<float> Suffered;

        public static void ReportLanded(ImpactWeight weight) => Landed?.Invoke(weight);

        public static void ReportReleased(ImpactWeight weight) => Released?.Invoke(weight);

        public static void ReportSuffered(float fraction) => Suffered?.Invoke(fraction);
    }
}
