using System.Collections.Generic;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingPlayerPoseDraftV1
    {
        public StablePlayerId PlayerId { get; set; }

        public SimVector3 Position { get; set; }

        public SimVector3 Forward { get; set; }

        public StickFigurePose Pose { get; set; }

        public TrainingPlayerPoseDraftV1 DeepCopy()
        {
            return new TrainingPlayerPoseDraftV1
            {
                PlayerId = PlayerId,
                Position = Position,
                Forward = Forward,
                Pose = Pose
            };
        }
    }

    public sealed class TrainingScenarioDraftV1
    {
        public TrainingScenarioDraftV1()
        {
            Players = new List<TrainingPlayerPoseDraftV1>();
        }

        public string ScenarioId { get; set; }

        public string DisplayName { get; set; }

        public string Source { get; set; }

        public int FormatVersion { get; set; } = TrainingScenarioV1.FormatVersion;

        public MatchContextV4 Context { get; set; }

        public TeamSide FirstServingSide { get; set; }

        public int HomeInitialRotationOffset { get; set; }

        public int AwayInitialRotationOffset { get; set; }

        public FormalMatchTacticInputV4 HomeTactics { get; set; }

        public FormalMatchTacticInputV4 AwayTactics { get; set; }

        public FormalMatchAiInputV4 Ai { get; set; }

        public List<TrainingPlayerPoseDraftV1> Players { get; }

        public SimVector3 BallPosition { get; set; }

        public SimVector3 BallVelocity { get; set; }

        public RallyStartRecipeV3 StartRecipe { get; set; }

        public TeamSide SourceTeam { get; set; }

        public StablePlayerId? LastLegalActor { get; set; }

        public TrainingScenarioAccessLevelV1 AccessLevel { get; set; } =
            TrainingScenarioAccessLevelV1.Developer;

        public TrainingScenarioDraftV1 DeepCopy()
        {
            var copy = new TrainingScenarioDraftV1
            {
                ScenarioId = ScenarioId,
                DisplayName = DisplayName,
                Source = Source,
                FormatVersion = FormatVersion,
                Context = Context,
                FirstServingSide = FirstServingSide,
                HomeInitialRotationOffset = HomeInitialRotationOffset,
                AwayInitialRotationOffset = AwayInitialRotationOffset,
                HomeTactics = Clone(HomeTactics),
                AwayTactics = Clone(AwayTactics),
                Ai = Clone(Ai),
                BallPosition = BallPosition,
                BallVelocity = BallVelocity,
                StartRecipe = StartRecipe,
                SourceTeam = SourceTeam,
                LastLegalActor = LastLegalActor,
                AccessLevel = AccessLevel
            };
            for (var index = 0; index < Players.Count; index++)
            {
                copy.Players.Add(Players[index]?.DeepCopy());
            }

            return copy;
        }

        internal static FormalMatchTacticInputV4 Clone(
            FormalMatchTacticInputV4 source)
        {
            if (source == null)
            {
                return null;
            }

            return new TrainingTeamTacticV1(source).ToInput();
        }

        internal static FormalMatchAiInputV4 Clone(FormalMatchAiInputV4 source)
        {
            return source == null
                ? null
                : new TrainingAiConfigurationV1(source).ToInput();
        }
    }
}
