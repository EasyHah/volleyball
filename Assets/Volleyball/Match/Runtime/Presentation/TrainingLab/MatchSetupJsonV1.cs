using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public static class MatchSetupJsonV1
    {
        [Serializable]
        private sealed class FileDto
        {
            public string contextJson;
            public int firstServingSide;
            public string[] homeRotation;
            public string[] awayRotation;
            public PlayerDto[] players;
            public VectorDto ballPosition;
            public VectorDto ballVelocity;
            public OverrideDto[] overrides;
            public bool rotationLocked;
        }

        [Serializable]
        private sealed class PlayerDto
        {
            public string playerId;
            public VectorDto position;
        }

        [Serializable]
        private sealed class VectorDto
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class OverrideDto
        {
            public string playerId;
            public bool hasStrength; public int strength;
            public bool hasHeight; public int height;
            public bool hasJump; public int jump;
            public bool hasMovement; public int movement;
            public bool hasReaction; public int reaction;
            public bool hasCoordination; public int coordination;
            public bool hasAttack; public int attack;
            public bool hasDefense; public int defense;
            public bool hasCourtIq; public int courtIq;
            public bool hasBlock; public int block;
            public bool hasServe; public int serve;
            public bool hasSet; public int setting;
            public bool hasDominantHand; public int dominantHand;
        }

        public static string Serialize(MatchSetupDraftV1 draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            new MatchSetupEditorV1(draft).Validate();
            return JsonUtility.ToJson(new FileDto
            {
                contextJson = ContractJson.SerializeV5(draft.BaseContext),
                firstServingSide = (int)draft.FirstServingSide,
                homeRotation = draft.HomeRotation.Select(value => value.Value).ToArray(),
                awayRotation = draft.AwayRotation.Select(value => value.Value).ToArray(),
                players = draft.Players.OrderBy(value => value.PlayerId.Value,
                        StringComparer.Ordinal)
                    .Select(value => new PlayerDto
                    {
                        playerId = value.PlayerId.Value,
                        position = Vector(value.Position)
                    }).ToArray(),
                ballPosition = Vector(draft.BallPosition),
                ballVelocity = Vector(draft.BallVelocity),
                overrides = draft.AttributeOverrides.OrderBy(pair => pair.Key.Value,
                        StringComparer.Ordinal)
                    .Select(pair => Override(pair.Key, pair.Value)).ToArray(),
                rotationLocked = draft.RotationLocked
            });
        }

        public static MatchSetupDraftV1 Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Match setup JSON is required.", nameof(json));
            FileDto file;
            try
            {
                file = JsonUtility.FromJson<FileDto>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Match setup JSON is malformed.", exception);
            }
            if (file == null || file.homeRotation == null ||
                file.awayRotation == null || file.players == null ||
                file.ballPosition == null || file.ballVelocity == null)
                throw new InvalidOperationException("Match setup JSON is incomplete.");

            var overrides = new Dictionary<PlayerId,
                TrainingPlayerAttributeOverrideV2>();
            foreach (var item in file.overrides ?? Array.Empty<OverrideDto>())
            {
                var value = new TrainingPlayerAttributeOverrideV2();
                Set(value, TrainingPlayerAttributeFieldV2.Strength,
                    item.hasStrength, item.strength);
                Set(value, TrainingPlayerAttributeFieldV2.Height,
                    item.hasHeight, item.height);
                Set(value, TrainingPlayerAttributeFieldV2.Jump,
                    item.hasJump, item.jump);
                Set(value, TrainingPlayerAttributeFieldV2.Movement,
                    item.hasMovement, item.movement);
                Set(value, TrainingPlayerAttributeFieldV2.Reaction,
                    item.hasReaction, item.reaction);
                Set(value, TrainingPlayerAttributeFieldV2.Coordination,
                    item.hasCoordination, item.coordination);
                Set(value, TrainingPlayerAttributeFieldV2.Attack,
                    item.hasAttack, item.attack);
                Set(value, TrainingPlayerAttributeFieldV2.Defense,
                    item.hasDefense, item.defense);
                Set(value, TrainingPlayerAttributeFieldV2.CourtIq,
                    item.hasCourtIq, item.courtIq);
                Set(value, TrainingPlayerAttributeFieldV2.Block,
                    item.hasBlock, item.block);
                Set(value, TrainingPlayerAttributeFieldV2.Serve,
                    item.hasServe, item.serve);
                Set(value, TrainingPlayerAttributeFieldV2.Set,
                    item.hasSet, item.setting);
                if (item.hasDominantHand)
                    value.SetDominantHand((DominantHandV5)item.dominantHand);
                overrides.Add(new PlayerId(item.playerId), value);
            }

            return MatchSetupDraftV1.Restore(
                ContractJson.DeserializeMatchContextV5(file.contextJson),
                (TeamSide)file.firstServingSide,
                file.homeRotation.Select(value => new PlayerId(value)),
                file.awayRotation.Select(value => new PlayerId(value)),
                file.players.Select(value => new MatchPlayerPoseDraftV1(
                    new PlayerId(value.playerId), Vector(value.position))),
                Vector(file.ballPosition),
                Vector(file.ballVelocity),
                overrides,
                file.rotationLocked);
        }

        private static OverrideDto Override(PlayerId id,
            TrainingPlayerAttributeOverrideV2 value)
        {
            if (value == null) throw new InvalidOperationException(
                "Match setup override cannot be null.");
            return new OverrideDto
            {
                playerId = id.Value,
                hasStrength = value.Strength.HasValue, strength = value.Strength ?? 0,
                hasHeight = value.HeightMillimeters.HasValue, height = value.HeightMillimeters ?? 0,
                hasJump = value.Jump.HasValue, jump = value.Jump ?? 0,
                hasMovement = value.Movement.HasValue, movement = value.Movement ?? 0,
                hasReaction = value.Reaction.HasValue, reaction = value.Reaction ?? 0,
                hasCoordination = value.Coordination.HasValue, coordination = value.Coordination ?? 0,
                hasAttack = value.Attack.HasValue, attack = value.Attack ?? 0,
                hasDefense = value.Defense.HasValue, defense = value.Defense ?? 0,
                hasCourtIq = value.CourtIq.HasValue, courtIq = value.CourtIq ?? 0,
                hasBlock = value.Block.HasValue, block = value.Block ?? 0,
                hasServe = value.Serve.HasValue, serve = value.Serve ?? 0,
                hasSet = value.Setting.HasValue, setting = value.Setting ?? 0,
                hasDominantHand = value.DominantHand.HasValue,
                dominantHand = value.DominantHand.HasValue
                    ? (int)value.DominantHand.Value : 0
            };
        }

        private static void Set(TrainingPlayerAttributeOverrideV2 value,
            TrainingPlayerAttributeFieldV2 field, bool present, int fieldValue)
        {
            if (present) value.Set(field, fieldValue);
        }

        private static VectorDto Vector(SimVector3 value) => new VectorDto
        {
            x = value.X, y = value.Y, z = value.Z
        };

        private static SimVector3 Vector(VectorDto value)
        {
            if (value == null) throw new InvalidOperationException(
                "Match setup vector is missing.");
            return new SimVector3(value.x, value.y, value.z);
        }
    }
}
