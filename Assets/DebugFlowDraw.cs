using UnityEngine;

public class DebugFlowDraw : MonoBehaviour
{
    private Material instancedMaterial;
    private FlowField flowField;
    private BoxCollider box;
    // Start is called before the first frame update
    void Start()
    {
        flowField = transform.parent.GetComponent<FlowField>();
        instancedMaterial = flowField.instancedMaterial;
        box = GetComponent<BoxCollider>();
    }
    // Update is called once per frame
    void LateUpdate()
    {
        instancedMaterial.SetVector("DRAWBOX_MIN", box.bounds.min);
        instancedMaterial.SetVector("DRAWBOX_EXTENTS", box.bounds.extents);
        instancedMaterial.SetVector("DRAWBOX_SIZE", box.bounds.size);
    }
    private void OnDisable()
    {
        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;
        instancedMaterial.SetVector("DRAWBOX_MIN", box.bounds.min);
        instancedMaterial.SetVector("DRAWBOX_EXTENTS", box.bounds.extents);
        instancedMaterial.SetVector("DRAWBOX_SIZE", box.bounds.size);
    }
}
