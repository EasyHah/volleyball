using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>
    /// Career-owned V5 launch data. It is intentionally separate from the
    /// eight-axis V4 launch type so no missing V5 bases can be invented.
    /// </summary>
    public sealed class CareerMatchPlayerLaunchV5
    {
        public CareerMatchPlayerLaunchV5(PlayerId playerId, string displayName,
            int jerseyNumber, CareerMatchPlayerPosition position, int rotationSlot,
            int fatigue, DominantHandV5 dominantHand, CareerBaseAttributesV5 bases)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value)) throw new ArgumentException("A player ID is required.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (jerseyNumber < 1 || jerseyNumber > 99) throw new ArgumentOutOfRangeException(nameof(jerseyNumber));
            if (!Enum.IsDefined(typeof(CareerMatchPlayerPosition), position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (rotationSlot < 1 || rotationSlot > 6) throw new ArgumentOutOfRangeException(nameof(rotationSlot));
            if (fatigue < 0 || fatigue > 100) throw new ArgumentOutOfRangeException(nameof(fatigue));
            if (!Enum.IsDefined(typeof(DominantHandV5), dominantHand)) throw new ArgumentOutOfRangeException(nameof(dominantHand));
            PlayerId = playerId;
            DisplayName = displayName;
            JerseyNumber = jerseyNumber;
            Position = position;
            RotationSlot = rotationSlot;
            Fatigue = fatigue;
            DominantHand = dominantHand;
            Bases = bases ?? throw new ArgumentNullException(nameof(bases));
        }

        public PlayerId PlayerId { get; }
        public string DisplayName { get; }
        public int JerseyNumber { get; }
        public CareerMatchPlayerPosition Position { get; }
        public int RotationSlot { get; }
        public int Fatigue { get; }
        public DominantHandV5 DominantHand { get; }
        public CareerBaseAttributesV5 Bases { get; }
    }

    public sealed class CareerMatchTeamLaunchV5
    {
        private readonly CareerMatchPlayerLaunchV5[] _players;

        public CareerMatchTeamLaunchV5(TeamId teamId, string displayName,
            CareerMatchTeamSide side, IReadOnlyList<CareerMatchPlayerLaunchV5> players)
        {
            if (string.IsNullOrWhiteSpace(teamId.Value)) throw new ArgumentException("A team ID is required.", nameof(teamId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(CareerMatchTeamSide), side)) throw new ArgumentOutOfRangeException(nameof(side));
            if (players == null || players.Count != 6) throw new ArgumentException("A V5 team needs six players.", nameof(players));
            TeamId = teamId;
            DisplayName = displayName;
            Side = side;
            _players = new CareerMatchPlayerLaunchV5[6];
            var ids = new HashSet<PlayerId>();
            for (var index = 0; index < _players.Length; index++)
            {
                var player = players[index] ?? throw new ArgumentException("V5 players cannot be null.", nameof(players));
                if (player.RotationSlot != index + 1 || !ids.Add(player.PlayerId))
                    throw new ArgumentException("V5 players must have unique IDs in ordered rotation slots.", nameof(players));
                _players[index] = player;
            }
        }

        public TeamId TeamId { get; }
        public string DisplayName { get; }
        public CareerMatchTeamSide Side { get; }
        public IReadOnlyList<CareerMatchPlayerLaunchV5> Players => Array.AsReadOnly(_players);
    }

    public sealed class CareerMatchLaunchV5
    {
        private readonly CareerMatchTeamLaunchV5[] _teams;

        public CareerMatchLaunchV5(Guid sessionId, uint matchSeed,
            IReadOnlyList<CareerMatchTeamLaunchV5> teams)
        {
            if (sessionId == Guid.Empty) throw new ArgumentException("A session ID is required.", nameof(sessionId));
            if (teams == null || teams.Count != 2) throw new ArgumentException("V5 requires home and away teams.", nameof(teams));
            if (teams[0] == null || teams[1] == null || teams[0].Side != CareerMatchTeamSide.Home ||
                teams[1].Side != CareerMatchTeamSide.Away || teams[0].TeamId.Equals(teams[1].TeamId))
                throw new ArgumentException("V5 teams must be distinct ordered home and away teams.", nameof(teams));
            SessionId = sessionId;
            MatchSeed = matchSeed;
            _teams = new[] { teams[0], teams[1] };
        }

        public Guid SessionId { get; }
        public uint MatchSeed { get; }
        public IReadOnlyList<CareerMatchTeamLaunchV5> Teams => Array.AsReadOnly(_teams);
    }

    /// <summary>Applies Career fatigue once, immediately before V5 context freezing.</summary>
    public static class CareerMatchFatigueV5
    {
        // Fatigue 100 leaves 75% of every trainable base; height and handedness are identity.
        public static CareerBaseAttributesV5 ApplyOnce(CareerBaseAttributesV5 bases, int fatigue)
        {
            if (bases == null) throw new ArgumentNullException(nameof(bases));
            if (fatigue < 0 || fatigue > 100) throw new ArgumentOutOfRangeException(nameof(fatigue));
            return new CareerBaseAttributesV5(Effective(bases.Strength, fatigue), bases.HeightMillimeters,
                Effective(bases.Jump, fatigue), Effective(bases.Movement, fatigue), Effective(bases.Reaction, fatigue),
                Effective(bases.Coordination, fatigue), Effective(bases.Attack, fatigue), Effective(bases.Defense, fatigue),
                Effective(bases.CourtIq, fatigue), Effective(bases.Block, fatigue), Effective(bases.Serve, fatigue),
                Effective(bases.Set, fatigue));
        }

        private static int Effective(int value, int fatigue) => (value * (400 - fatigue) + 200) / 400;
    }

    public sealed class CareerMatchV5Mapper
    {
        private readonly string _physicsConfigurationHash;
        private readonly TrajectoryPredictionProviderConfigurationV5 _trajectoryConfiguration;

        public CareerMatchV5Mapper(string physicsConfigurationHash,
            TrajectoryPredictionProviderConfigurationV5 trajectoryConfiguration)
        {
            if (string.IsNullOrWhiteSpace(physicsConfigurationHash)) throw new ArgumentException("A physics configuration hash is required.", nameof(physicsConfigurationHash));
            _physicsConfigurationHash = physicsConfigurationHash;
            _trajectoryConfiguration = trajectoryConfiguration ?? throw new ArgumentNullException(nameof(trajectoryConfiguration));
        }

        public MatchContextV5 ToContext(CareerMatchLaunchV5 launch)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return MatchContextV5.Create(launch.SessionId, unchecked((int)launch.MatchSeed),
                ToTeam(launch.Teams[0]), ToTeam(launch.Teams[1]), _physicsConfigurationHash,
                _trajectoryConfiguration, RulesVersions.FullRallyV3);
        }

        private static TeamSnapshotV5 ToTeam(CareerMatchTeamLaunchV5 team)
        {
            var players = new PlayerSnapshotV5[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                var player = team.Players[index];
                players[index] = new PlayerSnapshotV5(player.PlayerId, player.DisplayName,
                    player.JerseyNumber, ToPosition(player.Position), player.DominantHand,
                    CareerMatchFatigueV5.ApplyOnce(player.Bases, player.Fatigue));
            }
            return new TeamSnapshotV5(team.TeamId, team.DisplayName,
                team.Side == CareerMatchTeamSide.Home ? TeamSide.Home : TeamSide.Away, players);
        }

        private static PlayerPosition ToPosition(CareerMatchPlayerPosition position) => position switch
        {
            CareerMatchPlayerPosition.Setter => PlayerPosition.Setter,
            CareerMatchPlayerPosition.OutsideHitter => PlayerPosition.OutsideHitter,
            CareerMatchPlayerPosition.MiddleBlocker => PlayerPosition.MiddleBlocker,
            CareerMatchPlayerPosition.Opposite => PlayerPosition.Opposite,
            CareerMatchPlayerPosition.Libero => PlayerPosition.Libero,
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
    }
}
