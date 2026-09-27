using UnityEngine;
using UnityEngine.AI;

public sealed class EcoEnemyPath : MonoBehaviour
{
    private NavMeshPath path;
    private Vector3 lastGoal;
    private float nextPath;
    private int corner;
    public bool HasRoute => path != null && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1;
    public Vector3 Direction(Vector3 goal, Vector3 fallback)
    {
        if (EcoNavigation.Current == null || !EcoNavigation.Current.Ready) return fallback;
        path ??= new NavMeshPath();
        if (Time.time >= nextPath || (goal - lastGoal).sqrMagnitude > 2.25f)
        {
            nextPath = Time.time + 0.7f; lastGoal = goal; corner = 1; path.ClearCorners();
            if (NavMesh.SamplePosition(transform.position, out var start, 1.5f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(goal, out var end, 2.5f, NavMesh.AllAreas))
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
        }
        if (!HasRoute) return fallback;
        var points = path.corners;
        while (corner < points.Length - 1 && Flat(points[corner] - transform.position).magnitude < 0.45f) corner++;
        return Flat(points[Mathf.Min(corner, points.Length - 1)] - transform.position).normalized;
    }
    private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
