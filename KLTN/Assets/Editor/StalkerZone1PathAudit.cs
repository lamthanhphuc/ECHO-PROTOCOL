using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class StalkerZone1PathAudit
{
    private static readonly string[] RoomNames =
    {
        "06_Archive_C4_EMPTY",
        "01_Start_Area_EMPTY",
        "02_Initial_Storage_C1_EMPTY",
        "04_Server_Room_C2_EMPTY",
        "05_Research_Lab_C3_EMPTY"
    };

    [MenuItem("Tools/Stalker/Audit Zone 1 NavMesh Paths")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "SciFi")
        {
            Debug.LogError("Hãy mở scene SciFi trước khi chạy audit.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/StalkerNetwork.prefab");

        var agent = prefab != null
            ? prefab.GetComponentInChildren<NavMeshAgent>(true)
            : null;

        if (agent == null)
        {
            Debug.LogError(
                "Không tìm thấy NavMeshAgent trong StalkerNetwork.prefab.");
            return;
        }

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        var report = new StringBuilder();
        report.AppendLine("STALKER ZONE 1 - UNITY NAVMESH PATH AUDIT");
        report.AppendLine("Scene: " + scene.name);
        report.AppendLine("Agent type: " + agent.agentTypeID);
        report.AppendLine("Area mask: " + agent.areaMask);
        report.AppendLine();

        var samples = new Dictionary<string, List<Vector3>>();

        foreach (string roomName in RoomNames)
        {
            var room = FindZone1Room(scene, roomName);
            var points = room != null
                ? GetNavMeshPoints(room, filter)
                : new List<Vector3>();

            samples[roomName] = points;

            report.AppendLine(roomName);
            report.AppendLine("Object found: " + (room != null));
            report.AppendLine("Valid NavMesh samples: " + points.Count);

            foreach (var point in points)
                report.AppendLine("  " + Format(point));

            report.AppendLine();
        }

        var starts = samples[RoomNames[0]];

        if (starts.Count == 0)
        {
            report.AppendLine(
                "NO VALID ARCHIVE C4 SAMPLES. Cannot test routes.");
        }
        else
        {
            foreach (string targetName in RoomNames.Skip(1))
            {
                var targets = samples[targetName];
                int complete = 0;
                int partial = 0;
                int invalid = 0;

                report.AppendLine("ROUTE: Archive C4 -> " + targetName);

                if (targets.Count == 0)
                {
                    report.AppendLine(
                        "NO VALID TARGET SAMPLES. Result inconclusive.");
                    report.AppendLine();
                    continue;
                }

                foreach (var start in starts.Take(3))
                {
                    foreach (var target in targets.Take(10))
                    {
                        var path = new NavMeshPath();

                        bool calculated = NavMesh.CalculatePath(
                            start, target, filter, path);

                        if (path.status == NavMeshPathStatus.PathComplete)
                            complete++;
                        else if (path.status == NavMeshPathStatus.PathPartial)
                            partial++;
                        else
                            invalid++;

                        report.AppendLine(
                            "  start=" + Format(start) +
                            " target=" + Format(target) +
                            " calculated=" + calculated +
                            " status=" + path.status +
                            " corners=" + path.corners.Length +
                            " lastCorner=" +
                            (path.corners.Length > 0
                                ? Format(path.corners[path.corners.Length - 1])
                                : "NONE"));
                    }
                }

                report.AppendLine(
                    "SUMMARY: Complete=" + complete +
                    " Partial=" + partial +
                    " Invalid=" + invalid);
                report.AppendLine();
            }
        }

        string output = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "../../STALKER-ZONE1-NAVMESH-DIAG.txt"));

        File.WriteAllText(output, report.ToString());

        Debug.Log("[Stalker Path Audit] Đã xuất báo cáo: " + output);
        EditorUtility.RevealInFinder(output);
    }

    
    private static Transform FindZone1Room(
        Scene scene, string roomName)
    {
        // Tìm trong toàn bộ hierarchy, kể cả khi Zone 1
        // không phải object gốc của scene.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in
                    root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != roomName)
                    continue;

                // Xác nhận phòng thuộc Zone 1.
                for (Transform parent = candidate.parent;
                    parent != null;
                    parent = parent.parent)
                {
                    if (parent.name == "Zone01_ResearchStorage")
                        return candidate;
                }
            }
        }

        Debug.LogWarning(
            "[Stalker Path Audit] Không tìm thấy phòng Zone 1: "
            + roomName);

        return null;
    }

    private static List<Vector3> GetNavMeshPoints(
        Transform room, NavMeshQueryFilter filter)
    {
        var candidates = new List<Vector3>();

        foreach (var renderer in
                 room.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.gameObject.activeInHierarchy)
                continue;

            if (renderer.name.IndexOf(
                    "floor", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var b = renderer.bounds;

            candidates.Add(new Vector3(
                b.center.x, b.max.y + 0.1f, b.center.z));
        }

        foreach (var collider in
                 room.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.gameObject.activeInHierarchy ||
                collider.isTrigger)
                continue;

            if (collider.name.IndexOf(
                    "floor", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var b = collider.bounds;

            candidates.Add(new Vector3(
                b.center.x, b.max.y + 0.1f, b.center.z));
        }

        // Điểm dự phòng để chẩn đoán nếu tên mesh không chứa "floor".
        candidates.Add(room.position);

        var results = new List<Vector3>();
        int stride = Math.Max(1, candidates.Count / 20);

        for (int i = 0; i < candidates.Count; i += stride)
        {
            var candidate = candidates[i];

            if (!NavMesh.SamplePosition(
                    candidate, out var hit, 2f, filter))
                continue;

            // Tránh lấy nhầm NavMesh ở tầng khác hoặc phòng bên cạnh.
            var horizontal = new Vector2(
                candidate.x - hit.position.x,
                candidate.z - hit.position.z).magnitude;

            if (horizontal > 1.25f ||
                Mathf.Abs(candidate.y - hit.position.y) > 1.5f)
                continue;

            bool duplicate = results.Any(
                p => Vector3.Distance(p, hit.position) < 0.5f);

            if (!duplicate)
                results.Add(hit.position);

            if (results.Count >= 12)
                break;
        }

        return results;
    }

    private static string Format(Vector3 value)
    {
        return string.Format(
            "({0:F2}, {1:F2}, {2:F2})",
            value.x, value.y, value.z);
    }
}
