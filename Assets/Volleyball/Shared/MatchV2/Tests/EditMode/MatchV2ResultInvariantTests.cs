using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    public sealed class MatchV2ResultInvariantTests
    {
        [Test]
        public void Result_CreatesValidCompletedAndAbandonedObservations()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            var facts = MatchV2TestFactory.ZeroFacts(context);
            var completed = MatchResultV2.CreateCompleted(context, context.Teams[0].TeamId,
                new[] { new SetScoreV2(1, 25, 21, true) }, 46, facts);
            var abandoned = MatchResultV2.CreateAbandoned(context,
                new[] { new SetScoreV2(1, 4, 3, false) }, 7, ZeroFactsWithRallies(context, 0));

            Assert.DoesNotThrow(() => completed.ValidateAgainst(context));
            Assert.DoesNotThrow(() => abandoned.ValidateAgainst(context));
            Assert.That(completed.ResultHash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(abandoned.WinnerTeamId, Is.Null);
        }

        [Test]
        public void Result_RejectsWrongPlayerOrderForeignOrDuplicateIds()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            var facts = MatchV2TestFactory.ZeroFacts(context);
            (facts[0], facts[1]) = (facts[1], facts[0]);
            Assert.That(() => MatchResultV2.CreateCompleted(context, context.Teams[0].TeamId,
                new[] { new SetScoreV2(1, 25, 21, true) }, 46, facts),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("order"));
        }

        [TestCase("spike")]
        [TestCase("serve")]
        [TestCase("reception")]
        [TestCase("defense")]
        [TestCase("block")]
        [TestCase("jump")]
        [TestCase("stability_sum")]
        [TestCase("stability_zero")]
        [TestCase("stability_episode")]
        public void FactFamilies_RejectSubsetAndPartitionViolations(string family)
        {
            TestDelegate action = family switch
            {
                "spike" => () => new SpikeFactsV2(1, 1, 1),
                "serve" => () => new ServeFactsV2(1, 1, 1),
                "reception" => () => new ReceptionFactsV2(2, 1, 1, 1, 0, 0),
                "defense" => () => new DefenseFactsV2(1, 2),
                "block" => () => new BlockFactsV2(1, 2, 1),
                "jump" => () => new MatchLoadFactsV2(1, 1, 1, 1, 2, 0, 0),
                "stability_sum" => () => new StabilityFactsV2(1, 1, 1, 0, 0),
                "stability_zero" => () => new StabilityFactsV2(1, 0, 0, 0, 2),
                _ => () => new StabilityFactsV2(1, 0, 0, 1, 1)
            };
            Assert.That(action, Throws.TypeOf<MatchV2ContractException>());
        }

        [TestCase(24, 23, 47)]
        [TestCase(25, 24, 49)]
        [TestCase(25, 21, 45)]
        public void CompletedResult_RejectsIllegalScoreOrRallyCount(int home, int away, int rallies)
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            Assert.That(() => MatchResultV2.CreateCompleted(context, context.Teams[0].TeamId,
                new[] { new SetScoreV2(1, home, away, true) }, rallies,
                MatchV2TestFactory.ZeroFacts(context)), Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void CompletedResult_RejectsTechnicalPointsAboveTeamScore()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            var facts = MatchV2TestFactory.ZeroFacts(context);
            facts[0] = new MatchPlayerFactsV2(facts[0].PlayerId,
                new SpikeFactsV2(26, 26, 0), facts[0].Serve, facts[0].Reception,
                facts[0].Defense, facts[0].Block, facts[0].Load, facts[0].Stability);
            Assert.That(() => MatchResultV2.CreateCompleted(context, context.Teams[0].TeamId,
                new[] { new SetScoreV2(1, 25, 21, true) }, 46, facts),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("technical"));
        }

        [Test]
        public void CompletedResult_RejectsAwayTechnicalPointsAboveAwayScore()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            var facts = MatchV2TestFactory.ZeroFacts(context);
            facts[6] = new MatchPlayerFactsV2(facts[6].PlayerId,
                new SpikeFactsV2(22, 22, 0), facts[6].Serve, facts[6].Reception,
                facts[6].Defense, facts[6].Block, facts[6].Load, facts[6].Stability);

            Assert.That(() => MatchResultV2.CreateCompleted(context, context.Teams[0].TeamId,
                    new[] { new SetScoreV2(1, 25, 21, true) }, 46, facts),
                Throws.TypeOf<MatchV2ContractException>().With.Message.Contains("technical"));
        }

        [Test]
        public void AbandonedResult_AcceptsOnlyZeroSetsOrOneGenuinelyIncompleteSet()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());

            Assert.DoesNotThrow(() => MatchResultV2.CreateAbandoned(
                context, Array.Empty<SetScoreV2>(), 0, MatchV2TestFactory.ZeroFacts(context)));
            Assert.DoesNotThrow(() => MatchResultV2.CreateAbandoned(
                context, new[] { new SetScoreV2(1, 4, 3, false) }, 7,
                MatchV2TestFactory.ZeroFacts(context)));

            TestDelegate[] invalidResults =
            {
                () => MatchResultV2.CreateAbandoned(
                    context, Array.Empty<SetScoreV2>(), 1, MatchV2TestFactory.ZeroFacts(context)),
                () => MatchResultV2.CreateAbandoned(
                    context, new[] { new SetScoreV2(1, 4, 3, false) }, 6,
                    MatchV2TestFactory.ZeroFacts(context)),
                () => MatchResultV2.CreateAbandoned(
                    context, new[] { new SetScoreV2(1, 25, 21, true) }, 46,
                    MatchV2TestFactory.ZeroFacts(context)),
                () => MatchResultV2.CreateAbandoned(
                    context, new[]
                    {
                        new SetScoreV2(1, 4, 3, false),
                        new SetScoreV2(2, 1, 0, false)
                    }, 8, MatchV2TestFactory.ZeroFacts(context)),
                () => MatchResultV2.CreateAbandoned(
                    context, new[] { new SetScoreV2(1, 25, 21, false) }, 46,
                    MatchV2TestFactory.ZeroFacts(context))
            };

            foreach (var invalid in invalidResults)
                Assert.That(invalid, Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void Result_DefensivelyCopiesSetAndFactCollections()
        {
            var context = MatchV2TestFactory.CreateContext(MatchV2TestFactory.CreateTeams());
            var originalSet = new SetScoreV2(1, 25, 21, true);
            var sets = new[] { originalSet };
            var facts = MatchV2TestFactory.ZeroFacts(context);
            var originalFact = facts[0];
            var result = MatchResultV2.CreateCompleted(
                context, context.Teams[0].TeamId, sets, 46, facts);

            sets[0] = new SetScoreV2(1, 1, 0, false);
            facts[0] = facts[1];

            Assert.That(result.Sets[0], Is.SameAs(originalSet));
            Assert.That(result.PlayerFacts[0], Is.SameAs(originalFact));
        }

        [Test]
        public void Load_RejectsUnsafeIntegerAndNormalizedRangeOverflow()
        {
            Assert.That(() => new MatchLoadFactsV2(0, 9007199254740992L, 0, 0, 0, 0, 0),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => new MatchLoadFactsV2(0, 0, 0, 0, 0, 10001, 0),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void FactSubsetValidation_CannotBeBypassedByIntegerOverflow()
        {
            Assert.That(() => new SpikeFactsV2(int.MaxValue, int.MaxValue, 1),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => new ServeFactsV2(int.MaxValue, int.MaxValue, 1),
                Throws.TypeOf<MatchV2ContractException>());
            Assert.That(() => new StabilityFactsV2(int.MaxValue, int.MaxValue, 1, 0, 0),
                Throws.TypeOf<MatchV2ContractException>());
        }

        private static MatchPlayerFactsV2[] ZeroFactsWithRallies(MatchContextV2 context, int rallies)
        {
            return MatchV2TestFactory.ZeroFacts(context).Select(fact => new MatchPlayerFactsV2(
                fact.PlayerId, fact.Spike, fact.Serve, fact.Reception, fact.Defense, fact.Block,
                new MatchLoadFactsV2(rallies, 0, 0, 0, 0, 0, 0), fact.Stability)).ToArray();
        }
    }
}
