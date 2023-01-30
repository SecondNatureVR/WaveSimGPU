using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowField : MonoBehaviour
{
    [SerializeField] private ComputeShader flowFieldCS;
    [SerializeField] public Material instancedMaterial;
    [SerializeField] private Mesh mesh;
    private Bounds bounds;
    private Vector3Int dimensions;
    private int flowBufferSize;
    private ComputeBuffer flowVectorBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer positionBuffer;
    // Start is called before the first frame update
    void Start()
    {
        bounds = GetComponent<Collider>().bounds;
        dimensions = Vector3Int.CeilToInt(bounds.size);
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        // create buffer for vectors 
        flowBufferSize = dimensions.x * dimensions.y * dimensions.z;
        flowVectorBuffer = new ComputeBuffer(flowBufferSize, stride);

        Vector3[] vectors = new Vector3[flowBufferSize];
        for (int i = 0; i < flowBufferSize; i++)
        {
            vectors[i] = new Vector3(0.01f, 0, 1f) * Random.Range(.5f, .8f);
        }
        flowVectorBuffer.SetData(vectors);

        positionBuffer = new ComputeBuffer(flowBufferSize, stride);

        Vector3[] positions = new Vector3[flowBufferSize];
        int index = 0;
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    positions[index] = bounds.min + new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f);
                    index++;
                }
            }
        }
        positionBuffer.SetData(positions);


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
