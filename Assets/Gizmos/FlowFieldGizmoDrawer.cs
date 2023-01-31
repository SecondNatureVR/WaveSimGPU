using UnityEditor;
using UnityEngine;

public class FlowFieldGizmoDrawer 
{
  [DrawGizmo (GizmoType.Selected | GizmoType.NonSelected)]
  static void DrawGizmoForFlowField(FlowField flowField, GizmoType gizmoType) {
    Gizmos.color = Color.green;
    Bounds b = flowField.GetComponent<Collider>().bounds;
    Gizmos.DrawWireCube(b.center, b.size);
  }
}
