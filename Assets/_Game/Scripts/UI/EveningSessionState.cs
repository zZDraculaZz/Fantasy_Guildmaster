using System;
using System.Collections.Generic;

namespace FantasyGuildmaster.UI
{
    [Serializable]
    public sealed class EveningSessionState
    {
        public int dayIndex = -1;
        public int maxAP = 2;
        public int apLeft = 2;
        public List<string> performedActionIds = new();
        public bool forcedIntroFinished;
    }
}
