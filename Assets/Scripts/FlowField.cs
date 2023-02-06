using System;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

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
    [SerializeField] public Texture3D initDirectionTex;

    private Bounds bounds;
    private int flowBufferSize;
    private RenderParams renderParams;

    private Vector3Int TEX_DIMENSIONS;
    private Vector3 TEXEL_SIZE;
    [SerializeField] public Vector3 _UV_Offset = Vector3.zero;
    [SerializeField] public Vector3 _UV_Scale = Vector3.one;
    [SerializeField] public float _TimeScale = 3f;
    [SerializeField] public float _DiffusionScale = 3f;
    [SerializeField] public float _AdvectScale = 25f;
    [SerializeField] public float _AddForceScale = 1f;
    [SerializeField, Range(5,7)] public int texResolution;
   
    private DoubleBufferedTexture3D directionDBT;
    private DoubleBufferedTexture3D magnitudeDBT;

    // Compute Params
    private int threadGroupX;

    // Debugging
    private ComputeBuffer backStepPosCB;
    private Vector4[] backStepPosArr;
    private float SphereRadius;
    [SerializeField] public bool _enableDebugForce = false;
    [SerializeField] public bool _drawDebugAdvect = false;
    [SerializeField] public bool _drawFlowVectors = true;


    private ParticleSystemForceField forceField;

    struct FlowVector
    {
        // position is implied by indexing into buffer
        public Vector3 direction;
        public float magnitude;
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
        bounds = GetComponent<Collider>().bounds;
        TEX_DIMENSIONS = Vector3Int.one * (int) Mathf.Pow(2, texResolution);
        TEXEL_SIZE = new Vector3(1.0f / TEX_DIMENSIONS.x, 1.0f / TEX_DIMENSIONS.y, 1.0f / TEX_DIMENSIONS.z);

        Init3DTextures();
        InitDoubleBuffers();

        instancedMaterial.SetTexture("_NormalDirections", directionDBT.readTexture);
        instancedMaterial.SetTexture("_HeightMagnitudes", magnitudeDBT.readTexture);
        Shader.SetGlobalTexture("_NormalDirections", directionDBT.readTexture);
        Shader.SetGlobalTexture("_HeightMagnitudes", magnitudeDBT.readTexture);
        Shader.SetGlobalTexture("_NormalDirectionsRW", directionDBT.writeTexture);
        Shader.SetGlobalTexture("_HeightMagnitudesRW", magnitudeDBT.writeTexture);
        Shader.SetGlobalVector("BOUNDS_MIN", bounds.min);
        Shader.SetGlobalVector("BOUNDS_EXTENTS", bounds.extents);
        Shader.SetGlobalVector("BOUNDS_SIZE", bounds.size);
        Shader.SetGlobalVector("TEXEL_SIZE", TEXEL_SIZE);
        Shader.SetGlobalVector("TEX_DIMENSIONS", new Vector3(
            TEX_DIMENSIONS.x,
            TEX_DIMENSIONS.y,
            TEX_DIMENSIONS.z
        ));
        Shader.SetGlobalVector("_UV_Offset", _UV_Offset);
        Shader.SetGlobalVector("_UV_Scale", _UV_Scale);
        Shader.SetGlobalFloat("_DiffusionScale", _DiffusionScale);
        Shader.SetGlobalFloat("_AdvectScale", _AdvectScale);

        Shader.SetGlobalBuffer("_backStepPos", backStepPosCB);

        renderParams = new RenderParams(instancedMaterial);
        renderParams.worldBounds = bounds;

        // Compute Threads
        uint numthreadsX;
        flowFieldCS.GetKernelThreadGroupSizes(0, out numthreadsX, out _, out _);
        threadGroupX = Mathf.CeilToInt(flowBufferSize / (int)numthreadsX);

        // DEBUG
        backStepPosCB = new ComputeBuffer(flowBufferSize, sizeof(float) * 4);
        backStepPosArr = new Vector4[flowBufferSize];
        flowFieldCS.SetBuffer(0, "backStepPos", backStepPosCB);

        forceField = GetComponent<ParticleSystemForceField>();
        forceField.vectorField = new Texture3D(TEX_DIMENSIONS.x, TEX_DIMENSIONS.y, TEX_DIMENSIONS.z, TextureFormat.ARGB32, false, true);
        forceField.vectorField.Apply(false);
    }
    private void Init3DTextures()
    {
        // create buffers for vectors 
        flowBufferSize = TEX_DIMENSIONS.x * TEX_DIMENSIONS.y * TEX_DIMENSIONS.z;

        // Init values
        FlowVector[] vectors = new FlowVector[flowBufferSize];
        int index = 0;
        for (int i = 0; i < flowBufferSize; i++)
        {
            Vector3 pos = GetWorldPosition(i);
            FlowVector v = new FlowVector();
            Vector3 rotDir = new Vector3(Mathf.Cos(pos.x), Mathf.Sin(pos.y), Mathf.Sin(pos.z * 0.25f));
            Vector3 centerDir = pos;
            //float blendT = Mathf.Pow(pos.magnitude / bounds.extents.magnitude, 2);
            v.direction = Vector3.up;
            // float centerMag = pos.magnitude;
            // float rotMag = (pos.x - bounds.min.x) / bounds.size.x;
            // v.magnitude = pos.x < 0 && pos.y < 0 && pos.z < 0 ? centerMag : 0;
            v.magnitude = Vector3.Distance(pos, Vector3.zero) < 3 ? 0.3f : 0.001f;
            vectors[index++] = v;
        }

        if (initDirectionTex == null)
        {
            // FlowVector.direction = unit Vector3 w/ component values -1...1
            var directionTex = new Texture3D(TEX_DIMENSIONS.x, TEX_DIMENSIONS.y, TEX_DIMENSIONS.z, TextureFormat.ARGB32, false, true);
            directionTex.SetPixels(vectors
                .Select(v => (v.direction + Vector3.one) * 0.5f)
                .Select(d => new Color(d.z, d.y, d.x))
                .ToArray()
            );
            directionTex.Apply();

            AssetDatabase.CreateAsset(directionTex, $"Assets/Resources/directionInitTex.asset");
            initDirectionTex = directionTex;
        }

        if (initMagnitudeTex == null)
        {
            var magnitudeTex = new Texture3D(TEX_DIMENSIONS.x, TEX_DIMENSIONS.y, TEX_DIMENSIONS.z, TextureFormat.RFloat, false, true);
            magnitudeTex.SetPixelData(vectors.Select(v => v.magnitude).ToArray(), 0);
            magnitudeTex.Apply();

            AssetDatabase.CreateAsset(magnitudeTex, $"Assets/Resources/magnitudeInitTex.asset");
            initMagnitudeTex = magnitudeTex;
        }
    }
    private void InitDoubleBuffers()
    {
        directionDBT = DoubleBufferedTexture3D.CreateDirection(TEX_DIMENSIONS.x, TEX_DIMENSIONS.y, TEX_DIMENSIONS.z);
        directionDBT.Init(initDirectionTex);

        magnitudeDBT = DoubleBufferedTexture3D.CreateMagnitude(TEX_DIMENSIONS.x, TEX_DIMENSIONS.y, TEX_DIMENSIONS.z);
        magnitudeDBT.Init(initMagnitudeTex);
    }

    private void Update()
    {
        SphereRadius = Sphere.GetComponent<SphereCollider>().radius;
        Shader.SetGlobalVector("_UV_Offset", _UV_Offset);
        Shader.SetGlobalVector("_UV_Scale", _UV_Scale);
        Shader.SetGlobalFloat("_Time", Time.time);
        Shader.SetGlobalFloat("_TimeScale", _TimeScale);
        Shader.SetGlobalFloat("_deltaTime", Time.deltaTime);
        flowFieldCS.SetFloat("_decay", decay);
        flowFieldCS.SetBool("_enableDebugForce", _enableDebugForce);
        flowFieldCS.SetFloat("_AddForceScale", _AddForceScale);
        flowFieldCS.SetVector("_SpherePos", Sphere.transform.position);
        flowFieldCS.SetVector("_SphereVelocity", Sphere.velocity);
        flowFieldCS.SetFloat("_SphereRadius", SphereRadius);
        flowFieldCS.Dispatch(0, threadGroupX, 1, 1); // ADVECT
        flowFieldCS.Dispatch(1, threadGroupX, 1, 1); // DIFFUSE
        flowFieldCS.Dispatch(2, threadGroupX, 1, 1); // ADDFORCE

        Graphics.CopyTexture(directionDBT.readTexture, forceField.vectorField);
        magnitudeDBT.Swap();
        directionDBT.Swap();

        if (_drawFlowVectors) 
            Graphics.RenderMeshPrimitives(renderParams, mesh, 0, flowBufferSize);
    }

    // Debug
    private void OnDrawGizmos()
    {
        if (_drawDebugAdvect)
        {
            backStepPosCB.GetData(backStepPosArr);
            for (int i = 0; i < backStepPosArr.Length; i++)
            {
                var from = GetWorldPosition(i);
                Vector3 to = backStepPosArr[i];
                //if (Vector3.Distance(to, Vector3.zero) > 3)
                if (Vector3.Distance(from, Sphere.transform.position) < SphereRadius) {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(from, to);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(to, 0.05f);
                }
            }
        }
    }

    private void OnDisable()
    {
        directionDBT.Destroy();
        magnitudeDBT.Destroy();

        backStepPosCB.Release();
    }

    private void OnDestroy()
    {
       directionDBT.Destroy();
       magnitudeDBT.Destroy();

       backStepPosCB.Dispose();
    }
}
