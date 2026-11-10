using System;
using System.Collections.Generic;

namespace MoaKnights.Core.Sim
{
    /// <summary>SP가 차는 방식 (기획 4-8). enum은 끝에만 추가한다.</summary>
    public enum SpType
    {
        /// <summary>시간이 지나면 찬다.</summary>
        Natural = 0,

        /// <summary>공격할 때마다 찬다.</summary>
        OnAttack = 1,

        /// <summary>맞을 때마다 찬다.</summary>
        OnHit = 2,
    }

    /// <summary>스킬 한 단계의 효과. 회복은 즉시, 공격 · 저지 보너스는 지속 시간 동안.</summary>
    public sealed class SkillDefinition
    {
        /// <summary>최대 HP 대비 즉시 회복 비율(퍼센트, 내림).</summary>
        public int HealPercent { get; }

        /// <summary>지속 중 공격력 추가 비율(퍼센트). 50이면 1.5배.</summary>
        public int AttackBonusPercent { get; }

        /// <summary>지속 중 저지 수 추가.</summary>
        public int BlockBonus { get; }

        /// <summary>지속 틱. 0이면 즉시 효과만 있다.</summary>
        public int DurationTicks { get; }

        public SkillDefinition(int healPercent, int attackBonusPercent, int blockBonus, int durationTicks)
        {
            if (healPercent < 0) throw new ArgumentOutOfRangeException(nameof(healPercent));
            if (attackBonusPercent < 0) throw new ArgumentOutOfRangeException(nameof(attackBonusPercent));
            if (blockBonus < 0) throw new ArgumentOutOfRangeException(nameof(blockBonus));
            if (durationTicks < 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            HealPercent = healPercent;
            AttackBonusPercent = attackBonusPercent;
            BlockBonus = blockBonus;
            DurationTicks = durationTicks;
        }
    }

    /// <summary>능력자 한 종류의 SP와 단계별 스킬. 스킬 칸 0이 1단계다 (중첩마다 쓰는 스킬이 바뀐다, 기획 4-6).</summary>
    public sealed class SkillSet
    {
        private readonly SkillDefinition[] _skills;

        public SpType SpType { get; }
        public int MaxSp { get; }

        /// <summary>자연 회복일 때 SP 1이 차는 틱. 다른 타입이면 쓰지 않는다.</summary>
        public int NaturalRegenTicks { get; }

        public IReadOnlyList<SkillDefinition> Skills => _skills;

        public SkillSet(SpType spType, int maxSp, int naturalRegenTicks, IReadOnlyList<SkillDefinition> skills)
        {
            if (maxSp <= 0) throw new ArgumentOutOfRangeException(nameof(maxSp));
            if (spType == SpType.Natural && naturalRegenTicks <= 0) throw new ArgumentOutOfRangeException(nameof(naturalRegenTicks));
            if (skills == null || skills.Count == 0) throw new ArgumentException("Skill list is empty.");

            SpType = spType;
            MaxSp = maxSp;
            NaturalRegenTicks = naturalRegenTicks;
            _skills = new SkillDefinition[skills.Count];
            for (var i = 0; i < _skills.Length; i++) _skills[i] = skills[i] ?? throw new ArgumentNullException(nameof(skills));
        }

        public SkillDefinition ForStage(int stage)
        {
            if (stage < 1 || stage > _skills.Length) throw new ArgumentOutOfRangeException(nameof(stage));
            return _skills[stage - 1];
        }
    }
}
