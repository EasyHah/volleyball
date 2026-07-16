using System.Collections;
using UnityEngine;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.Presentation
{
    public sealed class PrototypePlayerAgent : MonoBehaviour
    {
        [SerializeField]
        private float _moveSpeed = 5f;

        public PlayerId Id { get; private set; }

        public StickFigureRig Rig { get; private set; }

        public void Initialize(PlayerId id, Color color, string jerseyNumber)
        {
            Id = id;
            Rig = StickFigureRig.Create(transform, color, jerseyNumber);
        }

        public IEnumerator MoveTo(Vector3 destination)
        {
            while ((transform.position - destination).sqrMagnitude > 0.01f)
            {
                Rig.SetPose(StickFigurePose.Run, Time.deltaTime * 8f);
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destination,
                    _moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = destination;
            Rig.SetPose(StickFigurePose.Ready, 0.25f);
        }
    }
}
