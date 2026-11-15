using Raylib_cs;
using static Raylib_cs.Raylib;

static class SceneRenderTarget
{
    // A texture instead of Raylib's default depth renderbuffer lets local heat and autofocus sample depth.
    public static RenderTexture2D Create(int width, int height)
    {
        var target = new RenderTexture2D { Id = Rlgl.LoadFramebuffer() };
        try
        {
            if (target.Id == 0) throw new InvalidDataException("Cannot create scene framebuffer.");
            var image = GenImageColor(width, height, Color.Black);
            try { target.Texture = LoadTextureFromImage(image); }
            finally { UnloadImage(image); }
            if (!IsTextureValid(target.Texture)) throw new InvalidDataException("Cannot create scene color texture.");
            Rlgl.FramebufferAttach(target.Id, target.Texture.Id, FramebufferAttachType.ColorChannel0, FramebufferAttachTextureType.Texture2D, 0);
            target.Depth = new Texture2D { Id = Rlgl.LoadTextureDepth(width, height, false), Width = width, Height = height, Mipmaps = 1 };
            Rlgl.FramebufferAttach(target.Id, target.Depth.Id, FramebufferAttachType.Depth, FramebufferAttachTextureType.Texture2D, 0);
            if (target.Depth.Id == 0 || !Rlgl.FramebufferComplete(target.Id))
                throw new InvalidDataException("Cannot create sampleable scene depth texture.");
            SetTextureFilter(target.Texture, TextureFilter.Bilinear); SetTextureWrap(target.Texture, TextureWrap.Clamp);
            SetTextureFilter(target.Depth, TextureFilter.Point); SetTextureWrap(target.Depth, TextureWrap.Clamp);
            return target;
        }
        catch { if (target.Id != 0) UnloadRenderTexture(target); throw; }
    }
}
