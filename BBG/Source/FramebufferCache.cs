using System;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace BBOpenGL;

public static partial class BBG
{
    private static class FramebufferCache
    {
        public const int MAX_COLOR_ATTACHMENTS = 8;
        public const int MAX_FRAMEBUFFER_ATTACHMENTS = MAX_COLOR_ATTACHMENTS + 1; // 8Color + 1DepthStencil

        public record struct Attachment
        {
            public Texture Texture;
            public int Level;
            public FramebufferAttachment AttachmentPoint;
        }

        public struct FramebufferDesc
        {
            public AttachmentArray Attachments;
            public int NumAttachments;

            public static bool operator ==(in FramebufferDesc lhs, in FramebufferDesc rhs)
            {
                if (lhs.NumAttachments != rhs.NumAttachments)
                {
                    return false;
                }

                for (int i = 0; i < lhs.NumAttachments; i++)
                {
                    if (lhs.Attachments[i] != rhs.Attachments[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public static bool operator !=(in FramebufferDesc lhs, in FramebufferDesc rhs)
            {
                return !(lhs == rhs);
            }
        }

        private record struct FramebufferRessorce
        {
            public FramebufferDesc FramebufferDesc;
            public int GLResource;
        }

        private static FramebufferRessorce[] framebuffers = [];

        public static int GetFramebuffer(in FramebufferDesc framebufferDesc)
        {
            for (int i = 0; i < framebuffers.Length; i++)
            {
                ref readonly FramebufferRessorce framebuffer = ref framebuffers[i];
                if (framebuffer.FramebufferDesc == framebufferDesc)
                {
                    return framebuffer.GLResource;
                }
            }

            FramebufferRessorce newFramebuffer = CreateFramebuffer(framebufferDesc);
            framebuffers = [.. framebuffers, newFramebuffer];

            return newFramebuffer.GLResource;
        }

        public static int DeleteFramebuffersWithTexture(Texture texture)
        {
            int aliveFbos = framebuffers.Length;
            for (int i = 0; i < aliveFbos;)
            {
                bool deletedFramebuffer = false;

                ref readonly FramebufferRessorce framebuffer = ref framebuffers[i];
                for (int j = 0; j < framebuffer.FramebufferDesc.NumAttachments; j++)
                {
                    ref readonly Attachment attachment = ref framebuffer.FramebufferDesc.Attachments[j];
                    if (attachment.Texture == texture)
                    {
                        GL.DeleteFramebuffers(1, in framebuffer.GLResource);

                        if (aliveFbos > 0)
                        {
                            // move deleted framebuffer to end of array
                            MathHelper.Swap(ref framebuffers[i], ref framebuffers[--aliveFbos]);
                        }

                        deletedFramebuffer = true;
                        break;
                    }
                }

                if (!deletedFramebuffer)
                {
                    i++;
                }
            }

            int deleted = framebuffers.Length - aliveFbos;
            Array.Resize(ref framebuffers, aliveFbos);

            return deleted;
        }

        private static unsafe FramebufferRessorce CreateFramebuffer(in FramebufferDesc framebufferDesc)
        {
            FramebufferRessorce newFramebuffer = new FramebufferRessorce();
            newFramebuffer.FramebufferDesc = framebufferDesc;
#if GL_HAS_DSA
            GL.CreateFramebuffers(1, ref newFramebuffer.GLResource);
#else
            GL.GenFramebuffers(1, ref newFramebuffer.GLResource);
            // a name from GenFramebuffers is not an object until it is bound once
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, newFramebuffer.GLResource);
#endif

            Span<ColorBuffer> drawBuffers = stackalloc ColorBuffer[MAX_COLOR_ATTACHMENTS];
            int numColorAttachments = 0;
            for (int i = 0; i < framebufferDesc.NumAttachments; i++)
            {
                ref readonly Attachment attachment = ref framebufferDesc.Attachments[i];

#if GL_HAS_DSA
                GL.NamedFramebufferTexture(newFramebuffer.GLResource, attachment.AttachmentPoint, attachment.Texture.ID, attachment.Level);
#else
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, newFramebuffer.GLResource);
                GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, attachment.AttachmentPoint, attachment.Texture.ID, attachment.Level);
#endif

                if (IsColorAttachment(attachment.AttachmentPoint))
                {
                    drawBuffers[numColorAttachments] = ColorBuffer.ColorAttachment0 + (uint)numColorAttachments;
                    numColorAttachments++;
                }
            }
#if GL_HAS_DSA
            GL.NamedFramebufferDrawBuffers(newFramebuffer.GLResource, numColorAttachments, ref drawBuffers[0]);
#else
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, newFramebuffer.GLResource);
            fixed (ColorBuffer* bufs = drawBuffers)
            {
                GL.DrawBuffers(numColorAttachments, (DrawBufferMode*)bufs);
            }
#endif

            return newFramebuffer;
        }

        private static bool IsColorAttachment(FramebufferAttachment framebufferAttachment)
        {
            return framebufferAttachment >= FramebufferAttachment.ColorAttachment0 &&
                   framebufferAttachment <= FramebufferAttachment.ColorAttachment16;
        }

        [InlineArray(MAX_FRAMEBUFFER_ATTACHMENTS)]
        public struct AttachmentArray
        {
            private Attachment _element;
        }
    }
}
