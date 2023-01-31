using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class FlowField : MonoBehaviour
{
    [SerializeField] private ComputeShader flowFieldCS;
    [SerializeField] public Material instancedMaterial;
    [SerializeField] private Mesh mesh;
    private Bounds bounds;
    private Vector3Int dimensions;
    private int flowBufferSize;
    private ComputeBuffer flowVectors;
    private RenderParams renderParams;

    struct FlowVector
    {
        // position is implied by indexing into buffer
        public Matrix4x4 rotation;
        public Matrix4x4 transform;
        public float magnitude;
    }
    Vector3 GetPosition(int index) {
        Vector3Int dims = Vector3Int.RoundToInt(bounds.size);
        int z = index % dims.z;
        int y = (index / dims.z) % dims.y;
        int x = index / (dims.y * dims.z);
        return bounds.min + new Vector3(x,y,z) + new Vector3(.5f, .5f, .5f);
    }

    // Start is called before the first frame update
    void Start()
    {
        bounds = GetComponent<Collider>().bounds;
        dimensions = Vector3Int.CeilToInt(bounds.size);

        // create buffers for vectors 
        flowBufferSize = dimensions.x * dimensions.y * dimensions.z;

        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FlowVector));
        flowVectors = new ComputeBuffer(flowBufferSize, stride);

        // Compute transformation matrices
        // https://forum.unity.com/threads/rotate-mesh-inside-shader.1109660/
        FlowVector[] vectors = new FlowVector[flowBufferSize];
        int index = 0;
        for (int i = 0; i < flowBufferSize; i++)
        {
            Vector3 pos = GetPosition(i);
            Vector3 flow = bounds.center - pos;
            if (pos.z < bounds.center.z)
            {
                flow = pos - bounds.center;
            }
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, flow.normalized);
            FlowVector v = new FlowVector();
            v.rotation = Matrix4x4.Rotate(rot);
            v.transform = Matrix4x4.TRS(pos, rot, Vector3.one);
            v.magnitude = Mathf.InverseLerp(1, bounds.extents.magnitude, flow.magnitude);
            vectors[index] = v;
            index++;
        }
        flowVectors.SetData(vectors);

        instancedMaterial.SetBuffer("flowVectors", flowVectors);
        instancedMaterial.SetVector("BOUNDS_MIN", bounds.min);
        instancedMaterial.SetInt("WIDTH", dimensions.x);
        instancedMaterial.SetInt("HEIGHT", dimensions.y);
        instancedMaterial.SetInt("DEPTH", dimensions.z);

        renderParams = new RenderParams(instancedMaterial);
        renderParams.worldBounds = bounds;
    }

    private void Update()
    {
        // Dispatch update flowfield
        Graphics.RenderMeshPrimitives(renderParams, mesh, 0, flowBufferSize); 
    }

    private void OnDisable()
    {
        flowVectors.Release();
        flowVectors = null;
    }

    private void OnDestroy()
    {
        flowVectors.Release();
    }
}
