using UnityEngine;

public class DoubleBufferedTexture3D : Object
{
    public RenderTexture readTexture;
    public RenderTexture writeTexture;
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

        readTexture = AllocateBuffer();
        writeTexture = AllocateBuffer();
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
        Graphics.CopyTexture(initTexture, readTexture);
        Graphics.CopyTexture(initTexture, writeTexture);
    }

    public void Swap()
    {
        Graphics.CopyTexture(writeTexture, readTexture);
        var tmp = readTexture;
        readTexture = writeTexture;
        writeTexture = tmp;
    }

    public void Destroy()
    {
        Destroy(writeTexture);
        Destroy(readTexture);
    }
}
