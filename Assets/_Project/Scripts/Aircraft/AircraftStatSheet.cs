using System;
using System.Collections.Generic;

namespace Adler.Aircraft
{
    /// <summary>
    /// 기체 한 대의 실효 성능. 기본 스탯에 장착 부품과 일시 효과를 얹어 계산한다.
    /// <para>
    /// 계산은 값이 실제로 바뀌었을 때만 일어난다. 비행 중 부품이 교체되거나 효과가
    /// 붙고 떨어져도 반영되면서, 아무 일도 없는 프레임에서는 배열을 읽기만 한다.
    /// </para>
    /// <para>
    /// 계산식은 <c>(기본값 + 고정 보정 합) × (1 + 비율 보정 합)</c> 이다.
    /// 같은 종류끼리 먼저 더하므로 부품을 끼운 순서가 결과를 바꾸지 않는다 —
    /// 순서에 따라 성능이 달라지면 플레이어가 이유를 알 수 없다.
    /// </para>
    /// </summary>
    public sealed class AircraftStatSheet
    {
        private static readonly int SlotCount = Enum.GetValues(typeof(PartSlot)).Length;

        private readonly AirframeDefinition _airframe;
        private readonly PartDefinition[] _equipped;
        private readonly List<TransientEntry> _transient = new();

        private readonly float[] _values = new float[AircraftStatInfo.Count];
        private readonly float[] _flat = new float[AircraftStatInfo.Count];
        private readonly float[] _percent = new float[AircraftStatInfo.Count];

        private bool _dirty = true;

        /// <summary>실효 수치가 바뀔 때 발생. HUD나 정비창 UI가 구독한다.</summary>
        public event Action Changed;

        public AircraftStatSheet(AirframeDefinition airframe)
        {
            _airframe = airframe ?? throw new ArgumentNullException(nameof(airframe));
            _equipped = new PartDefinition[SlotCount];
        }

        /// <summary>부품으로 바뀌지 않는 기체 고유 특성을 읽기 위한 통로.</summary>
        public AirframeDefinition Airframe => _airframe;

        public float this[AircraftStat stat]
        {
            get
            {
                EnsureUpToDate();
                return _values[(int)stat];
            }
        }

        // 비행 모델이 매 스텝 읽는 값들. 인덱서보다 읽기 쉬우라고 둔 것이다.
        public float MinSpeed => this[AircraftStat.MinSpeed];
        public float CruiseSpeed => this[AircraftStat.CruiseSpeed];
        public float MaxSpeed => this[AircraftStat.MaxSpeed];
        public float BoostSpeed => this[AircraftStat.BoostSpeed];
        public float Acceleration => this[AircraftStat.Acceleration];
        public float Deceleration => this[AircraftStat.Deceleration];
        public float ThrottleResponse => this[AircraftStat.ThrottleResponse];
        public float PitchRate => this[AircraftStat.PitchRate];
        public float RollRate => this[AircraftStat.RollRate];
        public float YawRate => this[AircraftStat.YawRate];
        public float ControlResponse => this[AircraftStat.ControlResponse];
        public float LowSpeedAgility => this[AircraftStat.LowSpeedAgility];
        public float BankTurnRate => this[AircraftStat.BankTurnRate];

        // ------------------------------------------------------------------
        // 부품
        // ------------------------------------------------------------------

        public PartDefinition GetEquipped(PartSlot slot) => _equipped[(int)slot];

        /// <summary>부품을 자기 슬롯에 장착한다. 이미 있던 부품은 반환된다.</summary>
        public PartDefinition Equip(PartDefinition part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            int index = (int)part.Slot;
            PartDefinition previous = _equipped[index];
            if (ReferenceEquals(previous, part))
            {
                return previous;
            }

            _equipped[index] = part;
            Invalidate();
            return previous;
        }

        public PartDefinition Unequip(PartSlot slot)
        {
            int index = (int)slot;
            PartDefinition previous = _equipped[index];
            if (previous == null)
            {
                return null;
            }

            _equipped[index] = null;
            Invalidate();
            return previous;
        }

        // ------------------------------------------------------------------
        // 일시 효과 (피격 페널티, 과열, 지속 시간이 있는 강화 등)
        // ------------------------------------------------------------------

        /// <summary>
        /// 부품과 무관하게 붙었다 떨어지는 보정. <paramref name="source"/>는 나중에
        /// 한꺼번에 걷어내기 위한 손잡이이므로, 효과를 건 쪽이 자기 자신을 넘기면 된다.
        /// </summary>
        public void AddTransient(object source, in StatModifier modifier)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _transient.Add(new TransientEntry(source, modifier));
            Invalidate();
        }

        /// <summary>해당 출처가 건 보정을 모두 제거한다.</summary>
        public bool RemoveTransient(object source)
        {
            int removed = _transient.RemoveAll(entry => ReferenceEquals(entry.Source, source));
            if (removed == 0)
            {
                return false;
            }

            Invalidate();
            return true;
        }

        public void ClearTransient()
        {
            if (_transient.Count == 0)
            {
                return;
            }

            _transient.Clear();
            Invalidate();
        }

        // ------------------------------------------------------------------
        // 계산
        // ------------------------------------------------------------------

        /// <summary>기본 스탯 에셋을 인스펙터에서 직접 고쳤을 때처럼, 밖에서 재계산을 강제한다.</summary>
        public void Invalidate()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        private void EnsureUpToDate()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            Recalculate();
        }

        private void Recalculate()
        {
            Array.Clear(_flat, 0, _flat.Length);
            Array.Clear(_percent, 0, _percent.Length);

            _airframe.WriteBaseValues(_values);

            foreach (PartDefinition part in _equipped)
            {
                if (part == null)
                {
                    continue;
                }

                foreach (StatModifier modifier in part.Modifiers)
                {
                    Accumulate(modifier.Stat, modifier.Mode, part.EffectiveValue(modifier));
                }
            }

            foreach (TransientEntry entry in _transient)
            {
                Accumulate(entry.Modifier.Stat, entry.Modifier.Mode, entry.Modifier.Value);
            }

            for (int i = 0; i < _values.Length; i++)
            {
                float combined = (_values[i] + _flat[i]) * (1f + _percent[i]);
                _values[i] = AircraftStatInfo.Clamp((AircraftStat)i, combined);
            }

            EnforceSpeedOrder();
        }

        private void Accumulate(AircraftStat stat, StatModifierMode mode, float value)
        {
            if (mode == StatModifierMode.Flat)
            {
                _flat[(int)stat] += value;
            }
            else
            {
                _percent[(int)stat] += value;
            }
        }

        /// <summary>
        /// 부품을 겹쳐 끼우다 보면 최저 속도가 최고 속도를 넘는 조합이 나온다.
        /// 그대로 두면 비행 모델이 목표 속도를 잡지 못하므로 순서를 강제한다.
        /// </summary>
        private void EnforceSpeedOrder()
        {
            int min = (int)AircraftStat.MinSpeed;
            int cruise = (int)AircraftStat.CruiseSpeed;
            int max = (int)AircraftStat.MaxSpeed;
            int boost = (int)AircraftStat.BoostSpeed;

            if (_values[cruise] < _values[min])
            {
                _values[cruise] = _values[min];
            }

            if (_values[max] < _values[cruise])
            {
                _values[max] = _values[cruise];
            }

            if (_values[boost] < _values[max])
            {
                _values[boost] = _values[max];
            }
        }

        private readonly struct TransientEntry
        {
            public readonly object Source;
            public readonly StatModifier Modifier;

            public TransientEntry(object source, in StatModifier modifier)
            {
                Source = source;
                Modifier = modifier;
            }
        }
    }
}
