using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherResponseV1
    {
        internal SetterTeacherResponseV1(
            IReadOnlyList<PlayerId> ranking,
            string reason)
        {
            Ranking = new ReadOnlyCollection<PlayerId>((ranking ??
                throw new ArgumentNullException(nameof(ranking))).ToArray());
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public IReadOnlyList<PlayerId> Ranking { get; }
        public PlayerId TopChoice => Ranking[0];
        public string Reason { get; }
    }
}
