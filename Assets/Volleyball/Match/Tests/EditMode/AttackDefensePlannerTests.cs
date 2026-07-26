using System.Linq;
using NUnit.Framework;
using Volleyball.AI;

namespace Volleyball.EditModeTests
{
    public sealed class AttackDefensePlannerTests
    {
        [Test]
        public void Result_ExposesPostSetCandidateAndThreatFactsWithoutIncompletePlan()
        {
            var names = typeof(AttackPlanningResultV3).GetProperties().Select(value => value.Name).ToArray();

            Assert.That(names, Does.Contain("Candidates"));
            Assert.That(names, Does.Contain("QualifiedPowerRoutes"));
            Assert.That(names, Does.Contain("FallbackCandidates"));
            Assert.That(names, Does.Contain("PublicThreat"));
            Assert.That(names, Does.Not.Contain("Plan"));
        }

        [Test]
        public void Planner_HasNoSetContactCommandSurface()
        {
            var names = typeof(AttackDefensePlanner).GetMethods().Select(value => value.Name);
            Assert.That(names, Has.None.Matches<string>(value =>
                value.Contains("ScheduleSet") || value.Contains("CommandSet") || value.Contains("ExecuteSet")));
        }
    }
}
