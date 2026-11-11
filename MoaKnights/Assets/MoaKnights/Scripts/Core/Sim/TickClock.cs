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

        /// <summary>느린 시간 비율(%). 100이면 보통. 카드를 맵 위로 끄는 동안 낮춘다. 배속과 곱해진다.</summary>
        public int SlowPercent { get; private set; } = 100;

        /// <summary>다음 틱까지 지난 비율(0~1). 화면 보간 전용이고 규칙에 쓰지 않는다.</summary>
        public float TickFraction => (float)_pendingTicks;

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

        /// <summary>느린 시간을 건다 (1~100%). 100이면 끈 것과 같다.</summary>
        public void SetSlow(int percent)
        {
            if (percent <= 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            SlowPercent = percent;
        }

        public void ClearSlow() => SlowPercent = 100;

        /// <summary>이번 프레임에 돌릴 틱 수를 돌려준다.</summary>
        public int Advance(double deltaSeconds)
        {
            if (Paused) return 0;

            _pendingTicks += deltaSeconds * _ticksPerSecond * Speed * SlowPercent / 100d;
            var ticks = (int)Math.Floor(_pendingTicks);
            _pendingTicks -= ticks;

            if (ticks <= MaxTicksPerAdvance) return ticks;
            _pendingTicks = 0d;
            return MaxTicksPerAdvance;
        }
    }
}
