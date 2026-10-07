using GCNRenderLibrary.Rendering;
using OpenTK.Graphics.OpenGL;
using System;
using Toolbox.Core;

namespace MPLibrary.GCN
{
    /// <summary>
    /// Uploads HSF textures to the GPU.
    ///
    /// GCNRenderLibrary decodes GameCube textures through the native gctex_v13
    /// library. That library is not shipped on all platforms (notably Linux),
    /// and GLGXTexture.Load swallows the resulting DllNotFoundException, which
    /// leaves behind an empty (0x0) texture. Models using such textures then
    /// render flat grey with no colors.
    ///
    /// This helper detects the empty texture and re-uploads it using the fully
    /// managed <see cref="Decode_Gamecube"/> decoder instead.
    /// </summary>
    public static class HsfTextureHelper
    {
        public static GLGXTexture CreateTexture(HSFTexture tex)
        {
            var info = tex.TextureInfo;

            var renderTex = new GLGXTexture(
                tex.Name, info.Width, info.Height,
                (uint)tex.GcnFormat, (uint)tex.GcnPaletteFormat, 1,
                tex.ImageData, tex.PaletteData);

            EnsureUploaded(renderTex, tex);
            return renderTex;
        }

        public static void EnsureUploaded(GLGXTexture renderTex, HSFTexture tex)
        {
            if (renderTex == null || tex?.ImageData == null || tex.ImageData.Length == 0)
                return;

            //Drain any stale GL error so the check below is meaningful.
            GL.GetError();

            renderTex.Bind();
            GL.GetTexLevelParameter(renderTex.TextureTarget, 0,
                GetTextureParameter.TextureWidth, out int width);
            GL.GetTexLevelParameter(renderTex.TextureTarget, 0,
                GetTextureParameter.TextureHeight, out int height);
            var err = GL.GetError();
            renderTex.Unbind();

            //The native decoder worked, nothing to do.
            if (err == ErrorCode.NoError && width > 0 && height > 0)
                return;

            UploadManaged(renderTex, tex);
        }

        private static void UploadManaged(GLGXTexture renderTex, HSFTexture tex)
        {
            //Prefer the dimensions of the GL texture itself as those match the
            //encoded image data (the HSF header may be stale after a replace).
            int width = renderTex.Width > 0 ? renderTex.Width : tex.TextureInfo.Width;
            int height = renderTex.Height > 0 ? renderTex.Height : tex.TextureInfo.Height;

            //The managed decoder outputs BGRA byte order, so upload as Bgra
            //into an Rgba texture to get correct colors.
            byte[] rgba = Decode_Gamecube.DecodeData(
                tex.ImageData, tex.PaletteData,
                (uint)width, (uint)height,
                tex.GcnFormat, tex.GcnPaletteFormat);

            if (rgba == null || rgba.Length != width * height * 4)
                return;

            renderTex.Width = width;
            renderTex.Height = height;

            renderTex.Bind();
            GL.TexImage2D(renderTex.TextureTarget, 0,
                PixelInternalFormat.Rgba,
                width, height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, rgba);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            renderTex.Unbind();
        }
    }
}
