using UnityEngine;

public class FlowField : MonoBehaviour
{
    [SerializeField] private ComputeShader flowFieldCS;
    [SerializeField] public Material instancedMaterial;
    [SerializeField] public float decay = -0.1f;
    [SerializeField] private Mesh mesh;
    [SerializeField] public Rigidbody Sphere;
    private Bounds bounds;
    private Vector3Int dimensions;
    private int flowBufferSize;
    private ComputeBuffer flowVectors;
    private RenderParams renderParams;

    struct FlowVector
    {
        // position is implied by indexing into buffer
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
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, flow.normalized);
            FlowVector v = new FlowVector();
            v.transform = Matrix4x4.TRS(pos, rot, Vector3.one);
            v.magnitude = flow.magnitude / bounds.extents.magnitude; 
            vectors[index] = v;
            index++;
        }
        flowVectors.SetData(vectors);
        flowFieldCS.SetBuffer(0, "flowVectors", flowVectors);

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
        flowFieldCS.SetFloat("_deltaTime", Time.deltaTime);
        flowFieldCS.SetFloat("_decay", decay);
        flowFieldCS.SetVector("_SpherePos", Sphere.position);
        flowFieldCS.SetVector("_SphereVelocity", Sphere.velocity);
        flowFieldCS.SetFloat("_SphereRadius", Sphere.GetComponent<SphereCollider>().radius);
        flowFieldCS.Dispatch(0, flowBufferSize/64, 1, 1);

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
        flowVectors.Dispose();
    }
}
