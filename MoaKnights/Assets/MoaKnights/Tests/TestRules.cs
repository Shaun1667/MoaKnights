using MoaKnights.Core.Sim;

namespace MoaKnights.Tests
{
    /// <summary>
    /// 테스트에서 함께 쓰는 규칙 값. 실제 수치는 BALANCE.md · 데이터 에셋이 기준이고, 여기 값은 계산하기 쉬운 고정값이다.
    /// 중첩: HP · 공격 100/130/160%, 3단계 저지 +1, 겹칠 때 기본 코스트만큼. 재배치 300틱, 다시 놓을 때마다 +5, 퇴각 환급 50%.
    /// </summary>
    internal static class TestRules
    {
        public static readonly DeployRules Deploy = new DeployRules(
            new[] { 100, 130, 160 },
            new[] { 100, 130, 160 },
            new[] { 0, 0, 1 },
            100,
            300,
            5,
            50);

        /// <summary>별 2개 기준 (디펜스 HP 50% 이상).</summary>
        public const int TwoStarHpPercent = 50;

        /// <summary>효과 없는 스킬 단계 n개. SP는 맞을 때만 차고 최대치가 커서 사실상 쓰지 않는다.</summary>
        public static SkillSet NoSkills(int stages = 3)
        {
            var skills = new SkillDefinition[stages];
            for (var i = 0; i < stages; i++) skills[i] = new SkillDefinition(0, 0, 0, 0);
            return new SkillSet(SpType.OnHit, 9999, 0, skills);
        }
    }
}
