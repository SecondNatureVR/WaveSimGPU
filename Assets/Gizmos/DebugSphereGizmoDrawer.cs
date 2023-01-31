using UnityEditor;
using UnityEngine;

public class DebugSphereGizmoDrawer
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawGizmoForDebugSphere(DebugSphere sphere, GizmoType gizmoType)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphere.transform.position, sphere.radius);
    }
}