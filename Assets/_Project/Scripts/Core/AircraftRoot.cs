using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adler.Core
{
    /// <summary>
    /// 기체 한 대의 뿌리를 표시한다. 부품이 자기 기체와 형제 부품을 찾는 기준점이다.
    /// <para>
    /// <c>GetComponentInParent</c>만으로는 모자라서 있다. 부품을 자식 오브젝트로 무리
    /// 지어 두면 서로가 형제라 부모 방향 탐색이 놓치는데, 그 증상이 "어떤 스트라타젬만
    /// 안 써진다"처럼 엉뚱한 곳에서 나타났다. 뿌리에서 아래로 훑으면 어디에 있든 찾는다.
    /// </para>
    /// <para>
    /// 아무 부품의 형(型)도 모른다. 무기가 무엇이고 조종이 무엇인지 알면 이 클래스가
    /// 그 계층들을 올려다보게 되고, 그러면 아래층이 위층을 아는 순환이 된다 — 이름 있는
    /// 창구가 필요한 쪽(화면 표시 같은)은 위층의 <c>AircraftRig</c>를 쓰면 된다.
    /// </para>
    /// <para>
    /// 찾은 것은 한 번만 찾는다. 부품은 대개 부팅 때 서로를 찾고 끝이므로, 그 뒤에
    /// 생기거나 사라지는 것까지 좇는 값을 치를 이유가 없다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftRoot : MonoBehaviour
    {
        private readonly Dictionary<Type, object> _found = new();

        private Rigidbody _body;

        /// <summary>이 기체의 몸체. 뿌리에 붙은 Rigidbody다.</summary>
        public Rigidbody Body => _body != null ? _body : _body = GetComponent<Rigidbody>();

        /// <summary>
        /// 기체 위에 얹힌 부품이 자기 뿌리를 찾을 때 쓴다.
        /// 인스펙터에 넣어두면 그것을 쓰고, 비어 있으면 위로 거슬러 올라가 찾는다.
        /// </summary>
        public static AircraftRoot Resolve(Component component, AircraftRoot assigned)
            => assigned != null ? assigned : component.GetComponentInParent<AircraftRoot>();

        /// <summary>
        /// 이 기체 어딘가에 있는 부품 하나. 없으면 null. 인터페이스로도 찾을 수 있다.
        /// </summary>
        public T Find<T>() where T : class
        {
            if (_found.TryGetValue(typeof(T), out object cached))
            {
                return (T)cached;
            }

            T found = GetComponentInChildren<T>(includeInactive: true);

            // 못 찾은 것은 기억하지 않는다. 부팅 순서에 따라 아직 안 만들어졌을 수
            // 있는데, 그 한 번의 헛걸음을 새겨두면 영영 없는 것이 된다.
            if (found != null)
            {
                _found[typeof(T)] = found;
            }

            return found;
        }
    }
}
