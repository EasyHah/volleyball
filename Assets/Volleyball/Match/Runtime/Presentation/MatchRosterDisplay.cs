using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Volleyball.Domain.Prototype;

namespace Volleyball.Presentation
{
    public sealed class MatchRosterDisplay : MonoBehaviour
    {
        private PhysicalMatchRallyDirector _director;
        private IReadOnlyList<PrototypePlayerAgent> _players;

        public void Initialize(
            PhysicalMatchRallyDirector director,
            IEnumerable<PrototypePlayerAgent> players)
        {
            _director = director != null ? director : throw new ArgumentNullException(nameof(director));
            _players = players?.OrderBy(player => player.Id.Team)
                .ThenBy(player => player.Id.RosterSlot)
                .ToArray() ?? throw new ArgumentNullException(nameof(players));
        }

        private void OnGUI()
        {
            if (_director == null || _players == null || _players.Count != 12)
            {
                return;
            }

            const float panelWidth = 330f;
            const float panelHeight = 178f;
            var top = Screen.height - panelHeight - 18f;
            RenderTeam(
                TeamId.Blue,
                new Rect(18f, top, panelWidth, panelHeight),
                new Color(0.65f, 0.82f, 1f));
            RenderTeam(
                TeamId.Orange,
                new Rect(Screen.width - panelWidth - 18f, top, panelWidth, panelHeight),
                new Color(1f, 0.75f, 0.58f));
        }

        private void RenderTeam(TeamId team, Rect panel, Color color)
        {
            GUI.Box(panel, string.Empty);
            var heading = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 22f),
                $"{team.ToString().ToUpperInvariant()} ROTATION",
                heading);
            var row = 0;
            foreach (var player in _players)
            {
                if (player.Id.Team != team)
                {
                    continue;
                }

                var position = _director.RotationPositionFor(player.Id);
                var zone = _director.IsFrontRow(player.Id) ? "F" : "B";
                var serve = player.StableId.Equals(_director.CurrentServer) ? "  SERVE" : string.Empty;
                GUI.Label(
                    new Rect(panel.x + 12f, panel.y + 31f + (row * 23f), panel.width - 24f, 22f),
                    $"P{position} {zone}  #{player.Id.RosterSlot + 1} {player.Id.Role}{serve}");
                row++;
            }
        }
    }
}
