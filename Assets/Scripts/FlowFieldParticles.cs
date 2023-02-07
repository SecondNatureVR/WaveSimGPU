using UnityEngine;

public class FlowFieldParticles : MonoBehaviour
{
    [SerializeField] private ComputeShader flowParticlesCS;
    private ParticleSystem particleSystem;
    private ParticleSystem.Particle[] particles;
    private ParticleSystemRenderer particleRenderer;
    private ComputeBuffer particleBuffer;
    private int numParticles;

    // Start is called before the first frame update
    void Start()
    {
        particleSystem = GetComponent<ParticleSystem>();
        particleSystem.GetComponent<ParticleSystemRenderer>();
        particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
        numParticles = particles.Length;
        particleSystem.GetParticles(particles, numParticles);

        int particleStride = System.Runtime.InteropServices.Marshal.SizeOf(new ParticleSystem.Particle());
        particleBuffer = new ComputeBuffer(numParticles, particleStride);
        particleBuffer.SetData(particles);
        flowParticlesCS.SetBuffer(0, "Particles", particleBuffer);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        flowParticlesCS.Dispatch(0, numParticles / 64, 1, 1);
        particleBuffer.GetData(particles);
        Debug.Log(particles[0].velocity);
        particleSystem.SetParticles(particles);
    }

    private void OnDestroy()
    {
        particleBuffer.Dispose();
    }
}
