using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Stateless helpers for finding pillows/targets — shared by every TeddyAI instance.</summary>
    public static class AITargeting
    {
        public static Pillow FindNearestAvailablePillow(Vector3 from)
        {
            if (MatchManager.Instance == null) return null;

            Pillow nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var pillow in MatchManager.Instance.ActivePillows)
            {
                if (pillow == null || !pillow.CanBePickedUp) continue;
                float d = (pillow.transform.position - from).sqrMagnitude;
                if (d < nearestDist) { nearestDist = d; nearest = pillow; }
            }
            return nearest;
        }

        public static PlayerHealth FindNearestAliveTarget(Vector3 from, PlayerHealth self)
        {
            if (MatchManager.Instance == null) return null;

            PlayerHealth nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var candidate in MatchManager.Instance.ActivePlayers)
            {
                if (candidate == null || candidate == self) continue;
                if (candidate.State == HealthState.Eliminated) continue;
                float d = (candidate.transform.position - from).sqrMagnitude;
                if (d < nearestDist) { nearestDist = d; nearest = candidate; }
            }
            return nearest;
        }
    }
}
