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

        /// <summary>A standing tower of this type below its top tier (a level-up drop target).</summary>
        public static BoardTarget UpgradableTowerOf(CardType tower, BoardSide side = BoardSide.Local) =>
            new() { Kind = BoardTargetKind.UpgradableTower, Tower = tower, Side = side };

        public static BoardTarget Towers(BoardSide side) => new() { Kind = BoardTargetKind.TowersCentre, Side = side };

        /// <summary>The centre of the towers standing within <paramref name="radius"/> of a point on the lane.</summary>
        public static BoardTarget TowersNear(float pathProgress, BoardSide side, float radius = 1.3f) =>
            new() { Kind = BoardTargetKind.TowersNearPath, PathProgress = pathProgress, Side = side, Radius = radius };

        /// <summary>The centre of the tightest group of towers (what one area spell can cover).</summary>
        public static BoardTarget TowerBunch(BoardSide side, float radius = 1.2f) =>
            new() { Kind = BoardTargetKind.DensestTowers, Side = side, Radius = radius };

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
                BoardTargetKind.UpgradableTower => TowerPosition(ctx, team, Tower, belowTopTier: true),
                BoardTargetKind.TowersCentre => TowersCentre(team),
                BoardTargetKind.LeadEnemy => LeadEnemy(team),
                BoardTargetKind.DensestEnemies => DensestEnemies(team, Radius),
                BoardTargetKind.DensestTowers => Densest(TowerPoints(team), Radius),
                BoardTargetKind.TowersNearPath => CentreNear(TowerPoints(team), PathPoint(ctx, team, PathProgress), Radius),
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

        private static Vector2? TowerPosition(CaptureContext ctx, TeamType team, CardType tower, bool belowTopTier = false)
        {
            foreach (TowerManager t in TowerRegistry.ActiveTowers)
            {
                if (t == null || TowerTeam(t) != team) continue;
                if (tower != CardType.None && ctx.Settings != null && ctx.Settings.CardOf(t) != tower) continue;
                if (belowTopTier && t.ServerTowerCombat != null && TowerTier(t) >= 3) continue;
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
            return Densest(points, radius);
        }

        private static List<Vector2> TowerPoints(TeamType team)
        {
            List<Vector2> points = new();
            foreach (TowerManager t in TowerRegistry.ActiveTowers)
                if (t != null && TowerTeam(t) == team) points.Add(t.transform.position);
            return points;
        }

        /// <summary>Of the points within <paramref name="radius"/> of <paramref name="near"/>, the centre of the
        /// tightest group one area spell (radius 1) can cover.</summary>
        private static Vector2? CentreNear(List<Vector2> points, Vector2? near, float radius)
        {
            if (!near.HasValue) return null;
            List<Vector2> close = new();
            foreach (Vector2 p in points)
                if (Vector2.Distance(p, near.Value) <= radius) close.Add(p);

            // Grouped at 1.5 and centred on the group: three towers in adjacent slots (~1.1 apart) all fall
            // inside a radius-1 spell dropped on their centre, where centring on any one of them leaves the others out.
            return Densest(close, 1.5f);
        }

        private static Vector2? Densest(List<Vector2> points, float radius)
        {
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

        /// <summary>The in-match placement tier (1-3), read off the server combat's network variable.</summary>
        public static int TowerTier(TowerManager tower) =>
            tower.ServerTowerCombat != null ? tower.ServerTowerCombat.TowerLevel.Value : 1;

        public static TeamType TowerTeam(TowerManager tower) =>
            tower.Team != null ? tower.Team.GetTeamType() : TeamType.None;
    }

    public enum BoardTargetKind
    {
        FreeSlotNearPath,
        PathPoint,
        TowerOfType,
        TowersCentre,
        UpgradableTower,
        DensestTowers,
        TowersNearPath,
        LeadEnemy,
        DensestEnemies,
        FieldCentre,
    }
}
#endif
