using System;

namespace MoaKnights.Core.Sim
{
    /// <summary>실제 시간을 틱 수로 바꾼다. 배속과 일시정지는 여기서만 처리하고 규칙은 모른다.</summary>
    public sealed class TickClock
    {
        // 프레임이 크게 밀려도 시뮬레이션이 따라잡느라 멈추지 않게 하는 한도.
        private const int MaxTicksPerAdvance = 16;

        private readonly int _ticksPerSecond;
        private double _pendingTicks;

        public int Speed { get; private set; } = 1;
        public bool Paused { get; set; }

        public TickClock(int ticksPerSecond)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            _ticksPerSecond = ticksPerSecond;
        }

        public void SetSpeed(int speed)
        {
            if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        /// <summary>이번 프레임에 돌릴 틱 수를 돌려준다.</summary>
        public int Advance(double deltaSeconds)
        {
            if (Paused) return 0;

            _pendingTicks += deltaSeconds * _ticksPerSecond * Speed;
            var ticks = (int)Math.Floor(_pendingTicks);
            _pendingTicks -= ticks;

            if (ticks <= MaxTicksPerAdvance) return ticks;
            _pendingTicks = 0d;
            return MaxTicksPerAdvance;
        }
    }
}
