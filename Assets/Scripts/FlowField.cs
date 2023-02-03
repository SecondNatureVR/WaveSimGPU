using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// Draws inspiration from https://github.com/keijiro/StableFluids/blob/master/Assets/Fluid.cs
public class FlowField : MonoBehaviour
{
    [SerializeField] private ComputeShader flowFieldCS;
    [SerializeField] public Material instancedMaterial;
    [SerializeField] public float decay = -0.1f;
    [SerializeField] private Mesh mesh;
    [SerializeField] public Rigidbody Sphere;
    [SerializeField] public GameObject DEBUG_ARROW;
    [SerializeField] public Texture3D initMagnitudeTex;
    private Bounds bounds;
    private Vector3Int dimensions;
    private int flowBufferSize;
    private RenderParams renderParams;

    private Vector3 TEXEL_SIZE;
    private Vector3Int TEX_DIMENSIONS;
    [SerializeField] public Vector3 _UV_Offset = Vector3.zero;
    [SerializeField] public Vector3 _UV_Scale = Vector3.one;

    // normal map for velocity
    private Texture3D velocityTex;
    // normal map for velocity
    private Texture3D directionTex;

    // RW Versions
    // normal map for velocity
    private Texture3D velocityTexRW;
    // normal map for velocity
    private Texture3D directionTexRW;

    private DoubleBufferedTexture3D magnitudeDBT;

    struct FlowVector
    {
        // position is implied by indexing into buffer
        public Vector3 direction;
        public Vector3 position;
        public float magnitude;
        public Vector3 velocity;
        public Quaternion rotation;
    }
    Vector3Int GetCoord(int index) {
        int z = index % TEX_DIMENSIONS.z;
        int y = (index / TEX_DIMENSIONS.z) % TEX_DIMENSIONS.y;
        int x = index / (TEX_DIMENSIONS.y * TEX_DIMENSIONS.z);
        return new Vector3Int(x, y, z);
    }
    Vector3 GetIndexUV(int index) {
        Vector3Int coord = GetCoord(index);
        return new Vector3(
          coord.x * TEXEL_SIZE.x,
          coord.y * TEXEL_SIZE.y,
          coord.z * TEXEL_SIZE.z
        );
    }

    Vector3 GetWorldUV(int index) {
        Vector3 uv = GetIndexUV(index);
        // Moves UV into center of texel in world space
        return uv + TEXEL_SIZE * 0.5f;
    }

    Vector3 GetWorldPosition(int index) {
        Vector3 uv = GetWorldUV(index);
        return bounds.min + new Vector3(
             uv.x * bounds.size.x,
             uv.y * bounds.size.y,
             uv.z * bounds.size.z
        );
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
        TEX_DIMENSIONS = dimensions;
        TEXEL_SIZE = new Vector3(1.0f / TEX_DIMENSIONS.x, 1.0f / TEX_DIMENSIONS.y, 1.0f / TEX_DIMENSIONS.z);

        // create buffers for vectors 
        flowBufferSize = dimensions.x * dimensions.y * dimensions.z;

        // Init values
        FlowVector[] vectors = new FlowVector[flowBufferSize];
        int index = 0;
        for (int i = 0; i < flowBufferSize; i++)
        {
            Vector3 pos = GetWorldPosition(i);
            Vector3 flow = Vector3.zero - pos;
            FlowVector v = new FlowVector();
            v.direction = flow.normalized;
            v.magnitude = (flow.magnitude / bounds.extents.magnitude);
            vectors[index] = v;
            index++;
        }

        Init3DTextures(vectors);

        instancedMaterial.SetTexture("_NormalDirections", directionTex);
        instancedMaterial.SetTexture("_HeightMagnitudes", magnitudeDBT.readTexture);
        Shader.SetGlobalTexture("_NormalDirections", directionTex);
        Shader.SetGlobalTexture("_HeightMagnitudes", magnitudeDBT.readTexture);
        Shader.SetGlobalTexture("_Velocity", velocityTex);
        Shader.SetGlobalTexture("_NormalDirectionsRW", directionTexRW);
        Shader.SetGlobalTexture("_HeightMagnitudesRW", magnitudeDBT.writeTexture);
        Shader.SetGlobalTexture("_VelocityRW", velocityTexRW);
        Shader.SetGlobalVector("BOUNDS_MIN", bounds.min);
        Shader.SetGlobalVector("BOUNDS_EXTENTS", bounds.extents);
        Shader.SetGlobalVector("BOUNDS_SIZE", bounds.size);
        Shader.SetGlobalInt("WIDTH", dimensions.x);
        Shader.SetGlobalInt("HEIGHT", dimensions.y);
        Shader.SetGlobalInt("DEPTH", dimensions.z);
        Shader.SetGlobalVector("TEXEL_SIZE", TEXEL_SIZE);
        Shader.SetGlobalVector("TEX_DIMENSIONS", new Vector3(
            TEX_DIMENSIONS.x,
            TEX_DIMENSIONS.y,
            TEX_DIMENSIONS.z
        ));
        Shader.SetGlobalVector("_UV_Offset", _UV_Offset);
        Shader.SetGlobalVector("_UV_Scale", _UV_Scale);
        renderParams = new RenderParams(instancedMaterial);
        renderParams.worldBounds = bounds;
    }

    private void Init3DTextures(FlowVector[] vectors)
    {
        // FlowVector.direction = unit Vector3 w/ component values -1...1
        directionTex = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.ARGB32, false, true);
        directionTex.filterMode = FilterMode.Point;
        directionTex.wrapMode = TextureWrapMode.Repeat;
        directionTex.SetPixelData(vectors
            .Select(v => (v.direction + Vector3.one) * 0.5f)
            .SelectMany(d => new byte[] {
                // d.z.... then d.x....
                (byte) 255,
                (byte) (d.z * 255),
                (byte) (d.y * 255),
                (byte) (d.x * 255)
            })
            .ToArray(),
            0
        );
        directionTexRW = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.ARGB32, false, true);
        Debug.Log(SystemInfo.maxTexture3DSize);
        directionTexRW.filterMode = FilterMode.Point;
        directionTexRW.wrapMode = TextureWrapMode.Repeat;
        Graphics.CopyTexture(directionTex, directionTexRW);

        directionTex.Apply();
        directionTexRW.Apply();


        magnitudeDBT = DoubleBufferedTexture3D.CreateMagnitude(dimensions.x, dimensions.y, dimensions.z);
        magnitudeDBT.Init(initMagnitudeTex);

        velocityTex = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RGBAHalf, false, true);
        velocityTex.filterMode = FilterMode.Trilinear;
        velocityTex.wrapMode = TextureWrapMode.Repeat;
        velocityTex.SetPixelData(vectors.Select(v => v.velocity).ToArray(), 0);

        velocityTexRW = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RGBAHalf, false, true);
        velocityTexRW.filterMode = FilterMode.Trilinear;
        velocityTexRW.wrapMode = TextureWrapMode.Repeat;
        Graphics.CopyTexture(velocityTex, velocityTexRW);

        velocityTex.Apply();
        velocityTexRW.Apply();

        Debug.Log($"directionTex.graphicsFormat={directionTex.graphicsFormat}");
        Debug.Log($"velocityTex.graphicsFormat={velocityTex.graphicsFormat}");

        AssetDatabase.CreateAsset(directionTex, $"Assets/Resources/directionInitTex.asset");
        AssetDatabase.CreateAsset(velocityTex, $"Assets/Resources/velocityInitTex.asset");
    }

    private void Update()
    {
        Shader.SetGlobalVector("_UV_Offset", _UV_Offset);
        Shader.SetGlobalVector("_UV_Scale", _UV_Scale);
        Shader.SetGlobalFloat("_Time", Time.time);
        Shader.SetGlobalFloat("_deltaTime", Time.deltaTime);
        flowFieldCS.SetFloat("_decay", decay);
        flowFieldCS.SetVector("_SpherePos", Sphere.position);
        flowFieldCS.SetVector("_SphereVelocity", Sphere.velocity);
        flowFieldCS.SetFloat("_SphereRadius", Sphere.GetComponent<SphereCollider>().radius);
        flowFieldCS.Dispatch(0, flowBufferSize / 64, 1, 1);

        magnitudeDBT.Swap();

        //SwapTextureBuffers();
        Graphics.RenderMeshPrimitives(renderParams, mesh, 0, flowBufferSize);
    }

    private void OnDisable()
    {
        Destroy(directionTex);
        Destroy(velocityTex);
        Destroy(directionTexRW);
        Destroy(velocityTexRW);
        Destroy(magnitudeDBT.readTexture);
        Destroy(magnitudeDBT.writeTexture);
    }

    private void SwapTextureBuffers()
    {
        Texture3D tmp;
        tmp = directionTex;
        directionTex = directionTexRW;
        directionTexRW = tmp;

        tmp = velocityTex;
        velocityTex = velocityTexRW;
        velocityTexRW = tmp;
    }
}
