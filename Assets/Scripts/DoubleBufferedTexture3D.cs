using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoubleBufferedTexture3D
{
    private RenderTexture _RT1;
    private RenderTexture _RT2;
    public RenderTexture readTexture { get => _RT1; }
    public RenderTexture writeTexture { get => _RT2; }

    private Vector3Int dimensions;

    public Format format;

    public int width { get => dimensions.x; }
    public int height { get => dimensions.y; }
    public int depth { get => dimensions.z; }

    // defaults
    private RenderTextureDescriptor descriptor;

    // TODO: Resolution

    public enum Format
    {
        Direction = RenderTextureFormat.ARGB32,
        Magnitude = RenderTextureFormat.RFloat,
    };

    private RenderTextureFormat RTFormat { get => (RenderTextureFormat) format; }

    public DoubleBufferedTexture3D(Format format, int width, int height, int depth)
    {
        dimensions = new Vector3Int(width, height, depth);
        this.format = format;

        descriptor = new RenderTextureDescriptor(width, height, RTFormat);
        descriptor.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        descriptor.volumeDepth = depth;
        descriptor.enableRandomWrite = true;
        descriptor.autoGenerateMips = false;
        descriptor.sRGB = false;

        _RT1 = AllocateBuffer();
        _RT2 = AllocateBuffer();
    }

    static public DoubleBufferedTexture3D CreateMagnitude(int width, int height, int depth)
    {
        return new DoubleBufferedTexture3D(Format.Magnitude, width, height, depth);
    }

    static public DoubleBufferedTexture3D CreateDirection(int width, int height, int depth)
    {
        return new DoubleBufferedTexture3D(Format.Direction, width, height, depth);
    }

    RenderTexture AllocateBuffer()
    {
        var rt = new RenderTexture(descriptor);
        rt.Create();
        return rt;
    }

    public void Init(Texture3D initTexture)
    {
        Graphics.CopyTexture(initTexture, _RT1);
        Graphics.CopyTexture(initTexture, _RT2);
    }

    public void Swap()
    {
        var tmp = _RT1;
        _RT1 = _RT2;
        _RT2 = tmp;
    }
}
