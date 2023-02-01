using System.Linq;
using TMPro.EditorUtilities;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
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
    private RenderParams renderParams;

    private Vector3 TEXEL_SIZE;
    private Vector3 TEX_DIMENSIONS;
    [SerializeField] public Vector3 _UV_Offset = Vector3.zero;
    [SerializeField] public Vector3 _UV_Scale = Vector3.one;


    // normal map for velocity
    private Texture3D directionTex;
    // height map for magnitude
    private Texture3D magnitudeTex;

    struct FlowVector
    {
        // position is implied by indexing into buffer
        public Matrix4x4 transform;
        public Vector3 direction;
        public float magnitude;
    }
    Vector3 GetPosition(int index) {
        int z = index % dimensions.z;
        int y = (index / dimensions.z) % dimensions.y;
        int x = index / (dimensions.y * dimensions.z);
        return bounds.min + new Vector3(x, y, z);
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(
            $"SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.Linear)=" +
            $"{SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.Linear)}"
        );
        Debug.Log(
            $"SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.Sample)=" +
            $"{SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.Sample)}"
        );
        Debug.Log(
            $"SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.SetPixels)=" +
            $"{SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_SNorm, FormatUsage.SetPixels)}"
        );
        bounds = GetComponent<Collider>().bounds;
        dimensions = Vector3Int.RoundToInt(bounds.size);

        // create buffers for vectors 
        flowBufferSize = dimensions.x * dimensions.y * dimensions.z;

        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FlowVector));

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
            v.direction = flow.normalized;
            v.magnitude = (flow.magnitude / bounds.extents.magnitude);
            vectors[index] = v;
            index++;
        }

        Init3DTextures(vectors);

        instancedMaterial.SetTexture("_NormalDirections", directionTex);
        instancedMaterial.SetTexture("_HeightMagnitudes", magnitudeTex);
        instancedMaterial.SetVector("BOUNDS_MIN", bounds.min);
        instancedMaterial.SetVector("BOUNDS_EXTENTS", bounds.extents);
        instancedMaterial.SetVector("BOUNDS_SIZE", bounds.size);
        instancedMaterial.SetInt("WIDTH", dimensions.x);
        instancedMaterial.SetInt("HEIGHT", dimensions.y);
        instancedMaterial.SetInt("DEPTH", dimensions.z);
        instancedMaterial.SetVector("TEXEL_SIZE", TEXEL_SIZE);
        instancedMaterial.SetVector("TEX_DIMENSIONS", TEX_DIMENSIONS);
        instancedMaterial.SetVector("_UV_Offset", _UV_Offset);
        instancedMaterial.SetVector("_UV_Scale", _UV_Scale);
        renderParams = new RenderParams(instancedMaterial);
        renderParams.worldBounds = bounds;

        flowFieldCS.SetTexture(0, "_NormalDirections", directionTex);
        flowFieldCS.SetTexture(0, "_HeightMagnitudes", magnitudeTex);
        flowFieldCS.SetVector("BOUNDS_MIN", bounds.min);
        flowFieldCS.SetVector("BOUNDS_EXTENTS", bounds.extents);
        flowFieldCS.SetVector("BOUNDS_SIZE", bounds.size);
        flowFieldCS.SetInt("WIDTH", dimensions.x);
        flowFieldCS.SetInt("HEIGHT", dimensions.y);
        flowFieldCS.SetInt("DEPTH", dimensions.z);
        flowFieldCS.SetVector("TEXEL_SIZE", TEXEL_SIZE);
        flowFieldCS.SetVector("TEX_DIMENSIONS", TEX_DIMENSIONS);
        flowFieldCS.SetVector("_UV_Offset", _UV_Offset);
        flowFieldCS.SetVector("_UV_Scale", _UV_Scale);
    }

    private void Init3DTextures(FlowVector[] vectors)
    {
        directionTex = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RGB24, false, true);
        directionTex.filterMode = FilterMode.Bilinear;
        directionTex.wrapMode = TextureWrapMode.Repeat;
        directionTex.SetPixels(vectors.Select(v => (v.direction + Vector3.one) * 0.5f).Select(p => new Color(p.x, p.y, p.z)).ToArray());
        directionTex.Apply();

        TEXEL_SIZE = new Vector3(1.0f / directionTex.width, 1.0f / directionTex.height, 1.0f / directionTex.depth);
        TEX_DIMENSIONS = new Vector3(directionTex.width, directionTex.height, directionTex.depth);

        var dirs = vectors.Select(v => v.direction).ToArray();
        var packed = dirs.Select(d => (d + Vector3.one) * 0.5f).ToArray();
        var allPacked = packed.SelectMany(p => new float[] { p.x, p.y, p.z }).Any(f => f > 1 || f < 0);
        var cols = packed.Select(p => new Color(p.x, p.y, p.z)).ToArray();
        var unpack = cols.Select(c => new Vector3(c.r, c.g, c.b)).Select(v => v * 2 - Vector3.one).ToArray();
        var allNorm = unpack.Any(v => v.magnitude > 1);
        var mmag = unpack.Select(v => v.magnitude).Max();
        Debug.Log($"directionTex.graphicsFormat={directionTex.graphicsFormat}");


        magnitudeTex = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RFloat, false, true);
        directionTex.filterMode = FilterMode.Bilinear;
        magnitudeTex.wrapMode = TextureWrapMode.Repeat;
        magnitudeTex.SetPixelData(vectors.Select(v => v.magnitude).ToArray(), 0);
        magnitudeTex.Apply();

        Debug.Log($"magnitudeTex.graphicsFormat={magnitudeTex.graphicsFormat}");
    }

    private void Update()
    {
        instancedMaterial.SetVector("_UV_Offset", _UV_Offset);
        instancedMaterial.SetVector("_UV_Scale", _UV_Scale);
        flowFieldCS.SetFloat("_deltaTime", Time.deltaTime);
        flowFieldCS.SetFloat("_decay", decay);
        flowFieldCS.SetVector("_SpherePos", Sphere.position);
        flowFieldCS.SetVector("_SphereVelocity", Sphere.velocity);
        flowFieldCS.SetFloat("_SphereRadius", Sphere.GetComponent<SphereCollider>().radius);
        //flowFieldCS.Dispatch(0, flowBufferSize/64, 1, 1);

        // Dispatch update flowfield
        Graphics.RenderMeshPrimitives(renderParams, mesh, 0, flowBufferSize); 
    }

    private void OnDisable()
    {
        DestroyImmediate(directionTex);
        DestroyImmediate(magnitudeTex);
    }
}
