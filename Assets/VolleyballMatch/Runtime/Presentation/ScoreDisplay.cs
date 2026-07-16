using System;
using UnityEngine;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.Presentation
{
    public sealed class ScoreDisplay : MonoBehaviour
    {
        private TextMesh _label;

        public static ScoreDisplay Create(Transform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var scoreObject = new GameObject("ScoreDisplay");
            scoreObject.transform.SetParent(parent, false);
            scoreObject.transform.SetLocalPositionAndRotation(
                new Vector3(0f, 8f, 2f),
                Quaternion.Euler(60f, 0f, 0f));
            var display = scoreObject.AddComponent<ScoreDisplay>();
            display._label = scoreObject.AddComponent<TextMesh>();
            display._label.anchor = TextAnchor.MiddleCenter;
            display._label.alignment = TextAlignment.Center;
            display._label.characterSize = 0.25f;
            display._label.fontSize = 64;
            display._label.color = Color.white;
            return display;
        }

        public void Render(PrototypeMatch match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            _label.text = $"BLUE {match.BlueScore}  :  {match.OrangeScore} ORANGE";
        }
    }
}
