using System;
using System.Collections.Generic;
using Volleyball.Domain.Prototype;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum RallyStartRecipeV3
    {
        ServeFlight,
        AfterReceive,
        AfterSet,
        AfterAttack,
        AfterAcceptedBlock
    }

    public sealed class RallyStartRequestV3
    {
        public RallyStartRequestV3(
            RallyStartRecipeV3 recipe,
            TeamSide sourceTeam,
            StablePlayerId? lastLegalActor)
        {
            if (!Enum.IsDefined(typeof(RallyStartRecipeV3), recipe))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recipe),
                    recipe,
                    "Rally start recipe is not supported.");
            }

            Recipe = recipe;
            SourceTeam = PlayerWorldSnapshotV3.RequireDefinedEnum(
                sourceTeam,
                nameof(sourceTeam));
            LastLegalActor = lastLegalActor;
        }

        public RallyStartRecipeV3 Recipe { get; }

        public TeamSide SourceTeam { get; }

        public StablePlayerId? LastLegalActor { get; }
    }

    public sealed class RallyStartStateV3
    {
        internal RallyStartStateV3(
            RallyStartRecipeV3 recipe,
            TeamSide sourceTeam,
            StablePlayerId? lastLegalActor,
            TouchSequenceStateV3 touchSequence,
            bool isPostBlock)
        {
            Recipe = recipe;
            SourceTeam = sourceTeam;
            LastLegalActor = lastLegalActor;
            TouchSequence = touchSequence ?? throw new ArgumentNullException(nameof(touchSequence));
            IsPostBlock = isPostBlock;
        }

        public RallyStartRecipeV3 Recipe { get; }

        public TeamSide SourceTeam { get; }

        public StablePlayerId? LastLegalActor { get; }

        public TouchSequenceStateV3 TouchSequence { get; }

        public bool IsPostBlock { get; }

        public RallyTouchState CreateCompatibilityState(RuntimePlayerId? mappedLastLegalActor)
        {
            if (Recipe == RallyStartRecipeV3.ServeFlight)
            {
                if (mappedLastLegalActor.HasValue)
                {
                    throw new ArgumentException(
                        "Serve-flight starts cannot carry a historical actor.",
                        nameof(mappedLastLegalActor));
                }
            }
            else
            {
                if (!mappedLastLegalActor.HasValue)
                {
                    throw new ArgumentException(
                        "This rally start requires the mapped last legal actor.",
                        nameof(mappedLastLegalActor));
                }

                var expectedTeam = SourceTeam == TeamSide.Home
                    ? RuntimeTeamId.Blue
                    : RuntimeTeamId.Orange;
                if (mappedLastLegalActor.Value.Team != expectedTeam)
                {
                    throw new ArgumentException(
                        "The mapped actor must belong to the semantic source team.",
                        nameof(mappedLastLegalActor));
                }
            }

            return RallyTouchState.CreateSemanticStart(this, mappedLastLegalActor);
        }
    }

    public static class RallyStartStateV3Factory
    {
        public static RallyStartStateV3 Create(
            RallyStartRequestV3 request,
            OnCourtEligibilitySnapshot eligibility)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (eligibility == null)
            {
                throw new ArgumentNullException(nameof(eligibility));
            }

            if (!Enum.IsDefined(typeof(RallyStartRecipeV3), request.Recipe))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.Recipe,
                    "Rally start recipe is not supported.");
            }

            if (request.Recipe == RallyStartRecipeV3.ServeFlight)
            {
                if (request.LastLegalActor.HasValue)
                {
                    throw new ArgumentException(
                        "Serve-flight starts cannot carry a historical actor.",
                        nameof(request));
                }

                return new RallyStartStateV3(
                    request.Recipe,
                    request.SourceTeam,
                    null,
                    TouchSequenceStateV3.Initial,
                    false);
            }

            if (!request.LastLegalActor.HasValue)
            {
                throw new ArgumentException(
                    "The selected rally start requires a last legal actor.",
                    nameof(request));
            }

            OnCourtPlayerEligibilityV3 actor;
            try
            {
                actor = eligibility.For(request.LastLegalActor.Value);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException(
                    "The last legal actor must be one of the twelve on-court players.",
                    nameof(request),
                    exception);
            }

            if (actor.Side != request.SourceTeam)
            {
                throw new ArgumentException(
                    "The last legal actor must belong to the semantic source team.",
                    nameof(request));
            }

            if (request.Recipe == RallyStartRecipeV3.AfterAcceptedBlock &&
                !BlockEligibilityRulesV3.CanAttempt(actor).IsEligible)
            {
                throw new ArgumentException(
                    "An accepted-block start requires a block-eligible actor.",
                    nameof(request));
            }

            var countedHits = CountedHitsFor(request.Recipe);
            var isPostBlock = request.Recipe == RallyStartRecipeV3.AfterAcceptedBlock;
            var sequence = new TouchSequenceStateV3(
                request.SourceTeam,
                isPostBlock ? null : request.SourceTeam,
                countedHits,
                isPostBlock ? null : request.LastLegalActor,
                isPostBlock
                    ? RallyContactClassificationV3.BlockContact
                    : RallyContactClassificationV3.TeamContact,
                null,
                false);

            return new RallyStartStateV3(
                request.Recipe,
                request.SourceTeam,
                request.LastLegalActor,
                sequence,
                isPostBlock);
        }

        private static int CountedHitsFor(RallyStartRecipeV3 recipe)
        {
            switch (recipe)
            {
                case RallyStartRecipeV3.AfterReceive:
                    return 1;
                case RallyStartRecipeV3.AfterSet:
                    return 2;
                case RallyStartRecipeV3.AfterAttack:
                    return 3;
                case RallyStartRecipeV3.AfterAcceptedBlock:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(recipe),
                        recipe,
                        "Rally start recipe does not carry a historical contact.");
            }
        }
    }
}
