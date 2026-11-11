#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// Where on the board a play is aimed, said the way a player would ("the free slot by the second bend",
    /// "the bunch of enemies on my lane", "their towers") and resolved to a world point when the drag starts.
    /// Read off live state on the host, so the same script survives a different wave or a different bot build.
    /// </summary>
    [Serializable]
    public class BoardTarget
    {
        public BoardTargetKind Kind = BoardTargetKind.FreeSlotNearPath;
        public BoardSide Side = BoardSide.Local;

        [Tooltip("FreeSlotNearPath / PathPoint: 0 = where the lane enters the field, 1 = the base.")]
        [Range(0f, 1f)] public float PathProgress = 0.5f;

        [Tooltip("TowerOfType: which tower. LeadEnemy/DensestEnemies ignore it.")]
        public CardType Tower = CardType.None;

        [Tooltip("DensestEnemies: radius in world units that counts as one bunch.")]
        public float Radius = 1.2f;

        [Tooltip("Added to the resolved point, in world units.")]
        public Vector2 Offset;

        public BoardTarget() { }

        public static BoardTarget Slot(float pathProgress, BoardSide side = BoardSide.Local) =>
            new() { Kind = BoardTargetKind.FreeSlotNearPath, PathProgress = pathProgress, Side = side };

        public static BoardTarget Path(float progress, BoardSide side) =>
            new() { Kind = BoardTargetKind.PathPoint, PathProgress = progress, Side = side };

        public static BoardTarget TowerOf(CardType tower, BoardSide side = BoardSide.Local) =>
            new() { Kind = BoardTargetKind.TowerOfType, Tower = tower, Side = side };

        public static BoardTarget Towers(BoardSide side) => new() { Kind = BoardTargetKind.TowersCentre, Side = side };

        public static BoardTarget Lead(BoardSide side) => new() { Kind = BoardTargetKind.LeadEnemy, Side = side };

        public static BoardTarget Bunch(BoardSide side, float radius = 1.2f) =>
            new() { Kind = BoardTargetKind.DensestEnemies, Side = side, Radius = radius };

        public static BoardTarget Field(BoardSide side) => new() { Kind = BoardTargetKind.FieldCentre, Side = side };

        public override string ToString() => Kind switch
        {
            BoardTargetKind.FreeSlotNearPath => $"free slot near {Side} path {PathProgress:0.00}",
            BoardTargetKind.PathPoint => $"{Side} path {PathProgress:0.00}",
            BoardTargetKind.TowerOfType => $"{Side} {Tower}",
            _ => $"{Kind} ({Side})",
        };

        /// <summary>World point, or null when nothing on the board matches (no free slot, no enemy...).</summary>
        public Vector2? Resolve(CaptureContext ctx)
        {
            TeamType team = ctx.Side(Side);
            Vector2? point = Kind switch
            {
                BoardTargetKind.FreeSlotNearPath => FreeSlotNear(ctx, team, PathPoint(ctx, team, PathProgress)),
                BoardTargetKind.PathPoint => PathPoint(ctx, team, PathProgress),
                BoardTargetKind.TowerOfType => TowerPosition(ctx, team, Tower),
                BoardTargetKind.TowersCentre => TowersCentre(team),
                BoardTargetKind.LeadEnemy => LeadEnemy(team),
                BoardTargetKind.DensestEnemies => DensestEnemies(team, Radius),
                BoardTargetKind.FieldCentre => FieldCentre(ctx, team),
                _ => null,
            };

            return point.HasValue ? point.Value + Offset : null;
        }

        // ------------------------------------------------------------------ resolvers

        private static Vector2? PathPoint(CaptureContext ctx, TeamType team, float progress)
        {
            WaypointPath path = ctx.Waves != null ? ctx.Waves.GetPath(team) : null;
            return path != null ? (Vector2)path.SamplePosition(Mathf.Clamp01(progress)) : null;
        }

        /// <summary>The empty build slot of <paramref name="team"/> closest to <paramref name="near"/>.</summary>
        public static Vector2? FreeSlotNear(CaptureContext ctx, TeamType team, Vector2? near)
        {
            if (!near.HasValue) return null;

            Vector2? best = null;
            float bestDistance = float.MaxValue;

            foreach (AbstractPlaceable placeable in UnityEngine.Object.FindObjectsByType<AbstractPlaceable>(FindObjectsSortMode.None))
            {
                if (placeable == null || placeable.IsOccupied() || placeable.PlaceablePoint == null) continue;

                TeamIdentifier owner = placeable.GetComponentInParent<TeamIdentifier>();
                if (owner == null || owner.TeamType != team) continue;

                Vector2 p = placeable.PlaceablePoint.position;
                float d = Vector2.Distance(p, near.Value);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        private static Vector2? TowerPosition(CaptureContext ctx, TeamType team, CardType tower)
        {
            foreach (TowerManager t in TowerRegistry.ActiveTowers)
            {
                if (t == null || TowerTeam(t) != team) continue;
                if (tower != CardType.None && ctx.Settings != null && ctx.Settings.CardOf(t) != tower) continue;
                return t.transform.position;
            }

            return null;
        }

        private static Vector2? TowersCentre(TeamType team)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;

            foreach (TowerManager t in TowerRegistry.ActiveTowers)
            {
                if (t == null || TowerTeam(t) != team) continue;
                sum += (Vector2)t.transform.position;
                count++;
            }

            return count > 0 ? sum / count : null;
        }

        /// <summary>The enemy furthest down <paramref name="team"/>'s lane (the one about to cost health).</summary>
        private static Vector2? LeadEnemy(TeamType team)
        {
            EnemyManager lead = null;
            float best = -1f;

            foreach (EnemyManager e in EnemiesOn(team))
            {
                float progress = e.ServerMovement.Progress;
                if (progress > best)
                {
                    best = progress;
                    lead = e;
                }
            }

            return lead != null ? lead.transform.position : null;
        }

        /// <summary>The point with the most enemies within <paramref name="radius"/>, centred on that bunch.</summary>
        private static Vector2? DensestEnemies(TeamType team, float radius)
        {
            List<Vector2> points = new();
            foreach (EnemyManager e in EnemiesOn(team)) points.Add(e.transform.position);
            if (points.Count == 0) return null;

            int bestCount = 0;
            Vector2 bestCentre = points[0];

            foreach (Vector2 candidate in points)
            {
                Vector2 sum = Vector2.zero;
                int n = 0;
                foreach (Vector2 p in points)
                {
                    if (Vector2.Distance(candidate, p) > radius) continue;
                    sum += p;
                    n++;
                }

                if (n > bestCount)
                {
                    bestCount = n;
                    bestCentre = sum / n;
                }
            }

            return bestCentre;
        }

        private static Vector2? FieldCentre(CaptureContext ctx, TeamType team)
        {
            WaypointPath path = ctx.Waves != null ? ctx.Waves.GetPath(team) : null;
            if (path == null) return null;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < path.WaypointCount; i++) sum += (Vector2)path.GetWaypointPosition(i);
            return path.WaypointCount > 0 ? sum / path.WaypointCount : null;
        }

        // ------------------------------------------------------------------ live-state helpers (host)

        /// <summary>Enemies walking the lane that attacks <paramref name="team"/>'s base (wave and sent alike).</summary>
        public static IEnumerable<EnemyManager> EnemiesOn(TeamType team)
        {
            foreach (EnemyManager e in EnemyRegistry.ActiveEnemies)
            {
                if (e == null || e.ServerHealth == null || e.ServerMovement == null) continue;
                if (e.Team == null || e.Team.GetTeamType() != team) continue;
                yield return e;
            }
        }

        public static int CountEnemiesOn(TeamType team, float minProgress = 0f)
        {
            int n = 0;
            foreach (EnemyManager e in EnemiesOn(team))
                if (e.ServerMovement.Progress >= minProgress) n++;
            return n;
        }

        public static TeamType TowerTeam(TowerManager tower) =>
            tower.Team != null ? tower.Team.GetTeamType() : TeamType.None;
    }

    public enum BoardTargetKind
    {
        FreeSlotNearPath,
        PathPoint,
        TowerOfType,
        TowersCentre,
        LeadEnemy,
        DensestEnemies,
        FieldCentre,
    }
}
#endif
