namespace VolleyballMatch.Domain.Prototype
{
    public sealed class PrototypeMatch
    {
        private bool _isRallyActive = true;

        public PrototypeMatch(TeamId firstServer)
        {
            ServingTeam = firstServer;
        }

        public int BlueScore { get; private set; }

        public int OrangeScore { get; private set; }

        public int RallyNumber { get; private set; }

        public TeamId ServingTeam { get; private set; }

        public bool TryAwardPoint(TeamId winner)
        {
            if (!_isRallyActive)
            {
                return false;
            }

            if (winner == TeamId.Blue)
            {
                BlueScore++;
            }
            else
            {
                OrangeScore++;
            }

            ServingTeam = winner;
            RallyNumber++;
            _isRallyActive = false;
            return true;
        }

        public void BeginNextRally()
        {
            _isRallyActive = true;
        }
    }
}
