using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class TrainingRallyStartV5
    {
        public TrainingRallyStartV5(MatchSetupSnapshotV1 setup)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            if (setup.BaseContext.ContractVersion != ContractVersions.MatchV5)
                throw new ArgumentException(
                    "Training rally startup requires a native V5 context.",
                    nameof(setup));
            ContextHash = setup.BaseContext.ContextHash;
            SetupHash = setup.SetupHash;
        }

        public MatchSetupSnapshotV1 Setup { get; }
        public string ContextHash { get; }
        public string SetupHash { get; }
        public int Seed => Setup.BaseContext.Seed;
    }
}
