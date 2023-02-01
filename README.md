# WaveSimGPU

Implementation of a 3D vector field for fluid simulations using compute shaders for GPU calculations.

The idea is to have visual effect particles flow through this vector field.

![1-31-2023 (13-58-15)](https://user-images.githubusercontent.com/122818242/215856949-bcf986c1-8f0c-401f-af46-1f6b67463f32.gif)

TODO:
- Generic support for particles/rigidbodies inside vector field
- Interface with 3D texture to encode direction (normal map) and magnitude (height map) of flow vectors
  - I think we can also leveraging the GPU interpolator to get flow vector in continuous space
- Interface with some particle system
- Expose to VR interface
  - User can wave hand around and affect the flow field
  - User can perform attraction/repulsion with button presses
     - Support via brush-like drawing of the 3D textures directly.
