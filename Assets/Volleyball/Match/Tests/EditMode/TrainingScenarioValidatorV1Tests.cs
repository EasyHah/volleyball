using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingScenarioValidatorV1Tests
    {
        [Test]
        public void Validate_ReportsMissingDuplicateAndUnexpectedPlayersWithStablePaths()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.Players.RemoveAt(0);
            draft.Players.Add(draft.Players[0].DeepCopy());

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == TrainingScenarioIssueCodesV1.DuplicatePlayer &&
                issue.PropertyPath.StartsWith("players[", System.StringComparison.Ordinal)), Is.True);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == TrainingScenarioIssueCodesV1.MissingPlayer &&
                !string.IsNullOrWhiteSpace(issue.ObjectId)), Is.True);
        }

        [Test]
        public void Validate_RejectsNonFiniteAndOutOfBoundsSpatialInputs()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.Players[0].Position = new SimVector3(float.NaN, 0f, 0f);
            draft.Players[1].Position = new SimVector3(9f, 0f, 0f);
            draft.BallVelocity = new SimVector3(0f, float.PositiveInfinity, 1f);
            draft.BallPosition = new SimVector3(0f, 20f, 0f);

            var result = TrainingScenarioValidatorV1.Validate(draft);
            var codes = result.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain(TrainingScenarioIssueCodesV1.NonFiniteValue));
            Assert.That(codes, Does.Contain(TrainingScenarioIssueCodesV1.PlayerOutOfBounds));
            Assert.That(codes, Does.Contain(TrainingScenarioIssueCodesV1.BallOutOfBounds));
        }

        [Test]
        public void Validate_RejectsPlayerAndBallInitialIntersections()
        {
            var playerOverlap = TrainingScenarioV1Tests.CreateValidDraft();
            playerOverlap.Players[1].Position = playerOverlap.Players[0].Position;
            var ballOverlap = TrainingScenarioV1Tests.CreateValidDraft();
            ballOverlap.BallPosition =
                ballOverlap.Players[0].Position + new SimVector3(0f, .12f, 0f);

            Assert.That(
                TrainingScenarioValidatorV1.Validate(playerOverlap).Issues
                    .Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.PlayerOverlap));
            Assert.That(
                TrainingScenarioValidatorV1.Validate(ballOverlap).Issues
                    .Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.BallPlayerOverlap));
        }

        [Test]
        public void Validate_RejectsPlayerSnapshotOnOpponentCourt()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.Players[0].Position =
                new SimVector3(0f, 0f, 2f);

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Issues.Any(issue =>
                    issue.Code ==
                    TrainingScenarioIssueCodesV1.PlayerOutOfBounds &&
                    issue.ObjectId ==
                    draft.Players[0].PlayerId.Value),
                Is.True);
        }

        [Test]
        public void Validate_RejectsActorSideMismatchAndIneligiblePostBlockActor()
        {
            var sideMismatch = TrainingScenarioV1Tests.CreateValidDraft();
            sideMismatch.LastLegalActor =
                sideMismatch.Context.Away.Players[1].PlayerId;
            var invalidBlocker = TrainingScenarioV1Tests.CreateValidDraft();
            invalidBlocker.StartRecipe = RallyStartRecipeV3.AfterAcceptedBlock;
            invalidBlocker.LastLegalActor =
                invalidBlocker.Context.Home.Players[4].PlayerId;

            Assert.That(
                TrainingScenarioValidatorV1.Validate(sideMismatch).Issues
                    .Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidRallyStart));
            Assert.That(
                TrainingScenarioValidatorV1.Validate(invalidBlocker).Issues
                    .Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidRallyStart));
        }

        [Test]
        public void Validate_RejectsServeFlightFromNonServingTeam()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.StartRecipe = RallyStartRecipeV3.ServeFlight;
            draft.SourceTeam = TeamSide.Home;
            draft.LastLegalActor = null;
            draft.BallPosition = new SimVector3(0f, 2.1f, -1.2f);
            draft.BallVelocity = new SimVector3(0f, .5f, 6.5f);

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidRallyStart));
        }

        [Test]
        public void Validate_RejectsBallTravellingBackTowardSourceTeam()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.BallVelocity = new SimVector3(.1f, -.3f, -6.5f);

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidRallyStart));
        }

        [Test]
        public void Validate_RejectsBallStartingOnOpponentCourt()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.BallPosition = new SimVector3(0f, 2.1f, 1.2f);

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidRallyStart));
        }

        [Test]
        public void Build_DerivesThirdTouchAndPostBlockExclusivelyWithoutRawDraftFields()
        {
            var attackDraft = TrainingScenarioV1Tests.CreateValidDraft();
            attackDraft.StartRecipe = RallyStartRecipeV3.AfterAttack;
            var attack = TrainingScenarioValidatorV1.Build(attackDraft);
            var blockDraft = TrainingScenarioV1Tests.CreateValidDraft();
            blockDraft.StartRecipe = RallyStartRecipeV3.AfterAcceptedBlock;
            blockDraft.LastLegalActor = blockDraft.Context.Home.Players[1].PlayerId;
            var block = TrainingScenarioValidatorV1.Build(blockDraft);

            Assert.That(attack.StartState.TouchSequence.CountedHits, Is.EqualTo(3));
            Assert.That(attack.StartState.IsPostBlock, Is.False);
            Assert.That(block.StartState.TouchSequence.CountedHits, Is.Zero);
            Assert.That(block.StartState.IsPostBlock, Is.True);
            Assert.That(block.StartState.TouchSequence.CurrentCountedSequenceTeam, Is.Null);
        }

        [Test]
        public void Validate_ReservesPlayerAccessButDoesNotEnableItInFirstMilestone()
        {
            var draft = TrainingScenarioV1Tests.CreateValidDraft();
            draft.AccessLevel = TrainingScenarioAccessLevelV1.Player;

            var result = TrainingScenarioValidatorV1.Validate(draft);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.InvalidAccess));
        }
    }
}
