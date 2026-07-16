namespace TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

// ── DOM-003: agent pathing over the CON-003 traversal graph ─────────────────
// Breadth-first search: unit-cost edges via TraversalGraph.Neighbors (horizontal-both-walkable,
// vertical-both-stairs). The guest sim caches the returned path and walks it a cell at a time;
// GuestTicksPerCell governs the per-cell duration (movement is game state, interpolation is view state).

public static class GuestPathfinding
{
    /// <summary>
    /// Shortest cell path from <paramref name="start"/> to the nearest cell of one of
    /// <paramref name="targetRooms"/>. The target ROOM is chosen by smallest path length, ties broken
    /// by lowest <see cref="RoomId"/> (CON-005 agenda rule). The returned path <b>excludes</b>
    /// <paramref name="start"/> and ends on the chosen room cell; it is empty when the guest already
    /// stands on a target room cell, and <c>null</c> when no target room is reachable.
    /// </summary>
    public static IReadOnlyList<CellCoord>? PathToNearestRoom(
        TraversalGraph graph, CellCoord start, IReadOnlySet<RoomId> targetRooms)
    {
        if (targetRooms.Count == 0) return null;

        var predecessor = new Dictionary<CellCoord, CellCoord>();
        var distance = new Dictionary<CellCoord, int> { [start] = 0 };
        var frontier = new Queue<CellCoord>();
        frontier.Enqueue(start);

        // Best target found so far, compared by (distance, roomId) — BFS visits in nondecreasing
        // distance, so once we have any target we only need to resolve ties at the same distance.
        CellCoord? bestCell = null;
        var bestDistance = int.MaxValue;
        var bestRoom = default(RoomId);

        void Consider(CellCoord cell, int dist)
        {
            if (!graph.RoomAtCell.TryGetValue(cell, out var room) || !targetRooms.Contains(room)) return;
            if (bestCell is null || dist < bestDistance || (dist == bestDistance && room.Value < bestRoom.Value))
            {
                bestCell = cell;
                bestDistance = dist;
                bestRoom = room;
            }
        }

        Consider(start, 0);

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();
            var dist = distance[cell];

            // Everything reachable beyond this point is at distance ≥ dist+1; a target already found at
            // ≤ dist can no longer be beaten, so we can stop expanding once we pass its distance.
            if (bestCell is not null && dist >= bestDistance) break;

            foreach (var next in graph.Neighbors(cell))
            {
                if (distance.ContainsKey(next)) continue;
                distance[next] = dist + 1;
                predecessor[next] = cell;
                Consider(next, dist + 1);
                frontier.Enqueue(next);
            }
        }

        if (bestCell is null) return null;

        var path = new List<CellCoord>();
        var step = bestCell.Value;
        while (!step.Equals(start))
        {
            path.Add(step);
            step = predecessor[step];
        }
        path.Reverse();
        return path;
    }
}
