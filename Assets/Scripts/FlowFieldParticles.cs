using System.Runtime.InteropServices;
using UnityEngine;

public class FlowFieldParticles : MonoBehaviour
{
    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    [SerializeField] private ComputeShader flowParticlesCS;
    [SerializeField] private FlowField flowField;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Mesh particleMesh;
    [SerializeField] public bool _drawDebug;
    private RenderParams renderParams;
    private ComputeBuffer particleBuffer;
    public int numParticles = 1000;

    // debug
    private Particle[] debugParticleArr;

    // Start is called before the first frame update
    void Start()
    {
        var particles = new Particle[numParticles];
        for (int i = 0; i < numParticles; i++)
        {
            particles[i] = new Particle();
            var position = Random.insideUnitSphere * 10f;
            var velocity = Vector3.zero;
            particles[i].position = position;
            particles[i].velocity = velocity;
        }
        int stride = Marshal.SizeOf(typeof(Particle));
        particleBuffer = new ComputeBuffer(numParticles, stride);
        particleBuffer.SetData(particles);

        renderParams = new RenderParams(particleMaterial);
        renderParams.worldBounds = flowField.bounds;

        Shader.SetGlobalBuffer("_Particles", particleBuffer);

        // debug
        debugParticleArr = particles;
    }

    // Update is called once per frame
    void Update()
    {
        flowParticlesCS.Dispatch(0, numParticles / 64, 1, 1); // update
        Graphics.RenderMeshPrimitives(renderParams, particleMesh, 0, numParticles);
        flowParticlesCS.Dispatch(1, numParticles / 64, 1, 1); // recycle
    }

    // Debug
    private void OnDrawGizmos()
    {
        if (_drawDebug)
        {
            particleBuffer.GetData(debugParticleArr);
            for (int i = 0; i < numParticles; i++)
            {
                var from = debugParticleArr[i].position;
                Vector3 to = debugParticleArr[i].velocity + from;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(from, to);
            }
        }
    }


    private void OnDestroy()
    {
        particleBuffer.Dispose();
    }
}
