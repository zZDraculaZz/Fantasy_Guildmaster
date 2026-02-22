namespace FantasyGuildmaster.Core
{
    public static class SimulationTime
    {
        public static long NowSeconds { get; private set; }

        public static void Reset(long start = 0)
        {
            NowSeconds = start;
        }

        public static void AdvanceSeconds(long delta)
        {
            if (delta <= 0)
            {
                return;
            }

            NowSeconds += delta;
        }
    }
}
