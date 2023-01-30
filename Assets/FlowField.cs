using System.Collections;
using System.Collections.Generic;
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
    private ComputeBuffer flowVectorBuffer;
    private ComputeBuffer positionBuffer;
    private ComputeBuffer argsBuffer;
    // Start is called before the first frame update
    void Start()
    {
        bounds = GetComponent<Collider>().bounds;
        dimensions = Vector3Int.CeilToInt(bounds.size);
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));

        // create buffers for vectors 
        flowBufferSize = dimensions.x * dimensions.y * dimensions.z;

        positionBuffer = new ComputeBuffer(flowBufferSize, stride);

        stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Matrix4x4));
        flowVectorBuffer = new ComputeBuffer(flowBufferSize, stride);
        Vector3[] positions = new Vector3[flowBufferSize];

        // Compute transformation matrices
        // https://forum.unity.com/threads/rotate-mesh-inside-shader.1109660/
        Matrix4x4[] vectors = new Matrix4x4[flowBufferSize];
        int index = 0;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    Vector3 pos = bounds.min + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f);
                    positions[index] = pos;
                    Vector3 flow = pos;
                    float scaling = flow.magnitude / bounds.extents.magnitude;
                    if (z > bounds.extents.z)
                    {
                        flow = bounds.center - pos;
                        scaling = 1 - scaling;
                    }
                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, flow.normalized);
                    vectors[index] = Matrix4x4.TRS(pos, rot, Vector3.one * Mathf.Pow(scaling, 2));
                    index++;
                }
            }
        }
        positionBuffer.SetData(positions);
        flowVectorBuffer.SetData(vectors);


        // Create args buffer
		uint[] args = new uint[5];
		args[0] = (uint)mesh.GetIndexCount(0);
		args[1] = (uint)flowBufferSize;
		args[2] = (uint)mesh.GetIndexStart(0);
		args[3] = (uint)mesh.GetBaseVertex(0);
		args[4] = 0; // offset

		argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
		argsBuffer.SetData(args);

        instancedMaterial.SetBuffer("positionBuffer", positionBuffer);
        instancedMaterial.SetBuffer("flowVectorBuffer", flowVectorBuffer);
    }

    private void Update()
    {
        // Dispatch update flowfield
        Graphics.DrawMeshInstancedIndirect(mesh, 0, instancedMaterial, GetComponent<Collider>().bounds, argsBuffer); 
    }

    private void OnDisable()
    {
        positionBuffer.Release();
        positionBuffer = null;
        flowVectorBuffer.Release();
        flowVectorBuffer = null;
        argsBuffer.Release();
        argsBuffer = null;
    }
}
