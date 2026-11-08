using System.Collections.Generic;

namespace Vodovorot.RoundExperience
{
    /// <summary>Session-only objective state. Repeated notifications cannot add progress.</summary>
    public sealed class RoundGoalProgress
    {
        private readonly HashSet<int> targets;
        private readonly HashSet<int> gathered = new HashSet<int>();
        private readonly HashSet<int> lost = new HashSet<int>();

        public RoundGoalProgress(IEnumerable<int> targetIds) => targets = new HashSet<int>(targetIds);
        public int Total => targets.Count;
        public int Gathered => gathered.Count;
        public int Lost => lost.Count;
        public bool Complete => Total > 0 && Gathered == Total;
        public bool Resolved => Gathered + Lost >= Total;
        public bool IsPending(int id) => targets.Contains(id) && !gathered.Contains(id) && !lost.Contains(id);

        public bool Collect(int id)
        {
            if (!IsPending(id))
                return false;
            return gathered.Add(id);
        }

        public void MarkUnavailable(int id)
        {
            if (IsPending(id))
                lost.Add(id);
        }
    }
}
