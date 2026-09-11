using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;

namespace BBOpenGL;

public static partial class BBG
{
    public unsafe class Buffer : IDisposable
    {
        public enum BufferTarget : uint
        {
            DispatchIndirect = OpenTK.Graphics.OpenGL.BufferTarget.DispatchIndirectBuffer,
        }

        public enum BufferBackedBlockTarget : uint
        {
            ShaderStorage = OpenTK.Graphics.OpenGL.BufferTarget.ShaderStorageBuffer,
            Uniform = OpenTK.Graphics.OpenGL.BufferTarget.UniformBuffer,
        }

        public enum MemLocation : uint
        {
            /// <summary>
            /// The buffer resides in DEVICE memory
            /// </summary>
            DeviceLocal = 0,

            /// <summary>
            /// The buffer resides in HOST memory
            /// </summary>
            HostLocal = BufferStorageMask.ClientStorageBit,
        }

        public enum MemAccess : uint
        {
            /// <summary>
            /// The buffer must be written or read to by using the mapped memory pointer or read by using the Download functions.
            /// Writes by the HOST only become visible to the DEVICE after a call to glMemoryBarrier(CLIENT_MAPPED_BUFFER_BARRIER_BIT) and glFlushMappedBufferRange.
            /// Writes by the DEVICE only become visible to the HOST after a call to glMemoryBarrier(CLIENT_MAPPED_BUFFER_BARRIER_BIT) followed by a wait for glFenceSync(SYNC_GPU_COMMANDS_COMPLETE, 0)
            /// Note: AMD driver places this in HOST memory!
            /// </summary>
            MappedIncoherent = BufferStorageMask.MapPersistentBit | BufferStorageMask.MapReadBit | BufferStorageMask.MapWriteBit /* MapFlushExplicitBit*/,

            /// <summary>
            /// The buffer must be written or read to by using the mapped memory pointer or read by using the Download functions.
            /// Writes by the DEVICE only become visible to the HOST after waiting for glFenceSync(SYNC_GPU_COMMANDS_COMPLETE, 0).
            /// Note: AMD driver places this in HOST memory!
            /// </summary>
            MappedCoherent = BufferStorageMask.MapPersistentBit | BufferStorageMask.MapCoherentBit | BufferStorageMask.MapReadBit | BufferStorageMask.MapWriteBit,

            /// <summary>
            /// Same as <see cref="MappedIncoherent"/> except that it's write-only AND leverages ReBAR/SAM on AMD (and NVIDIA?) drivers. <br/>
            /// Read buffers are placed in HOST mem by AMD (and NVIDIA?) drivers because:
            /// "there is a massive CPU performance penalty, on the level of 2 to 3 orders of magnitude if you issue a CPU instruction to read from DEVICE_LOCAL
            /// there exists poorly programmed OGL apps who call MapBuffer and similar with flags thats do not properly indicate the app is going to read that memory via CPU
            /// out of caution for these apps we default to HOST memory"
            /// </summary>
            MappedCoherentWriteOnlyReBAR = BufferStorageMask.MapPersistentBit | BufferStorageMask.MapCoherentBit | BufferStorageMask.MapWriteBit,

            /// <summary>
            /// The buffer must be written or read by using the Upload/Download functions.
            /// Synchronization is taken care of by automatically.
            /// </summary>
            AutoSync = BufferStorageMask.DynamicStorageBit,
        }

        public int ID { get; private set; }
        public nint Size { get; private set; }
        public void* Memory { get; private set; }

        public BufferBackedBlockTarget BackingBlock { get; private set; }
        public int BlockIndex { get; private set; }

#if !GL_HAS_DSA
        // stands in for a persistently mapped pointer; see Allocate
        private void* shadow;

        // CopyWriteBuffer is the scratch target: nothing in the renderer draws
        // from it, so binding here does not disturb any binding it cares about.
        private const OpenTK.Graphics.OpenGL.BufferTarget EditTarget =
            OpenTK.Graphics.OpenGL.BufferTarget.CopyWriteBuffer;

        private void BindForEdit()
        {
            GL.BindBuffer(EditTarget, ID);
        }
#endif

        public Buffer()
        {
            int id = 0;
#if GL_HAS_DSA
            GL.CreateBuffers(1, ref id);
#else
            GL.GenBuffers(1, ref id);
#endif

            ID = id;
        }

        public void BindToBufferBackedBlock(BufferBackedBlockTarget target, int index)
        {
            GL.BindBufferBase((OpenTK.Graphics.OpenGL.BufferTarget)target, (uint)index, ID);
            BackingBlock = target;
            BlockIndex = index;
        }

        public void InvalidateData()
        {
#if GL_HAS_INVALIDATE_SUBDATA
            GL.InvalidateBufferData(ID);
#endif
        }

        public void Allocate(MemLocation memLocation, MemAccess memAccess, nint size)
        {
            Allocate(memLocation, memAccess, size, null);
        }

        public void Allocate(MemLocation memLocation, MemAccess memAccess, nint size, void* data)
        {
            Size = size;
            Memory = null;

            if (Size == 0)
            {
                Dispose();
            }
            else
            {
                // We use staging buffer upload pattern
                // We only do this when the buffer is not mapped, otherwise a glFinish/glFence is required to have the new content be immediately visible.
                // Reasons:
                // * On AMD uploading a lot of data to a GPU-side buffer driver the classical way takes a lot of time.
                //   Staging buffer approach is much faster (800ms vs 30ms, 101MB).
                // * On NVIDIA the uploaded data is kept arround even with GL_NONE flag
                //   https://discord.com/channels/337627185248468993/337629838770700290/1361819795573575730

                bool doStagingBufferUpload =
                    memLocation == MemLocation.DeviceLocal && 
                    memAccess != MemAccess.MappedCoherent &&
                    memAccess != MemAccess.MappedIncoherent && 
                    memAccess != MemAccess.MappedCoherentWriteOnlyReBAR &&
                    data != null;

#if GL_HAS_DSA && GL_HAS_BUFFER_STORAGE
                GL.NamedBufferStorage(ID, size, doStagingBufferUpload ? null : data, (BufferStorageMask)memLocation | (BufferStorageMask)memAccess);
#else
                // glBufferStorage is 4.4. glBufferData gives mutable storage with
                // a usage hint instead, which is as close as 4.1 gets.
                BindForEdit();
                GL.BufferData(EditTarget, size, doStagingBufferUpload ? null : data,
                              memLocation == MemLocation.DeviceLocal ? BufferUsage.StaticDraw : BufferUsage.DynamicDraw);
#endif
                if (doStagingBufferUpload)
                {
                    using Buffer stagingBuffer = new Buffer();
                    stagingBuffer.Allocate(MemLocation.HostLocal, MemAccess.AutoSync, size, data);

                    stagingBuffer.CopyTo(this, 0, 0, stagingBuffer.Size);
                }

#if GL_HAS_DSA && GL_HAS_BUFFER_STORAGE
                if (memAccess == MemAccess.MappedCoherent || memAccess == MemAccess.MappedCoherentWriteOnlyReBAR)
                {
                    Memory = GL.MapNamedBufferRange(ID, 0, size, (MapBufferAccessMask)memAccess);
                }
                else if (memAccess == MemAccess.MappedIncoherent)
                {
                    Memory = GL.MapNamedBufferRange(ID, 0, size, (MapBufferAccessMask)memAccess | MapBufferAccessMask.MapFlushExplicitBit);
                }
#else
                // Persistent mapping needs 4.4. Hand out a CPU block of the same
                // size instead and push it to the buffer on flush, so a caller
                // that writes through Memory still gets its data to the GPU.
                if (memAccess == MemAccess.MappedCoherent ||
                    memAccess == MemAccess.MappedCoherentWriteOnlyReBAR ||
                    memAccess == MemAccess.MappedIncoherent)
                {
                    WarnNoPersistentMapping();
                    shadow = NativeMemory.Alloc((nuint)size);
                    if (data != null)
                    {
                        // Copy initial data into the shadow so callers who read
                        // through Memory (e.g. the BVH builder) see real data.
                        NativeMemory.Copy(data, shadow, (nuint)size);
                    }
                    else
                    {
                        NativeMemory.Clear(shadow, (nuint)size);
                    }
                    Memory = shadow;
                }
#endif
            }
        }

        public void UploadData<T>(nint offset, nint size, in T data) where T : unmanaged
        {
            fixed (void* ptr = &data)
            {
                UploadData(offset, size, ptr);
            }
        }

        public void UploadData(nint offset, nint size, void* data)
        {
            if (size == 0) return;
#if GL_HAS_DSA
            GL.NamedBufferSubData(ID, offset, size, data);
#else
            BindForEdit();
            GL.BufferSubData(EditTarget, offset, size, data);
#endif
        }

        public void DownloadData(nint offset, nint size, void* data)
        {
            if (size == 0) return;
#if GL_HAS_DSA
            GL.GetNamedBufferSubData(ID, offset, size, data);
#else
            BindForEdit();
            GL.GetBufferSubData(EditTarget, offset, size, data);
#endif
        }

        public void CopyTo(Buffer buffer, nint readOffset, nint writeOffset, nint size)
        {
            if (size == 0) return;
#if GL_HAS_DSA
            GL.CopyNamedBufferSubData(ID, buffer.ID, readOffset, writeOffset, size);
#else
            GL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.CopyReadBuffer, ID);
            GL.BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget.CopyWriteBuffer, buffer.ID);
            GL.CopyBufferSubData(CopyBufferSubDataTarget.CopyReadBuffer, CopyBufferSubDataTarget.CopyWriteBuffer,
                                 readOffset, writeOffset, size);
#endif
        }

        public void Fill(in uint data)
        {
            Fill(0, Size, data);
        }

        public void Fill(nint offset, nint size, in uint data)
        {
            fixed (void* ptr = &data)
            {
                Fill(Texture.InternalFormat.R32UInt, Texture.PixelFormat.RInteger, Texture.PixelType.Int, offset, size, ptr);
            }
        }
        
        public void Fill(nint offset, nint size, in float data)
        {
            fixed (void* ptr = &data)
            {
                Fill(Texture.InternalFormat.R32Float, Texture.PixelFormat.R, Texture.PixelType.Float, offset, size, ptr);
            }
        }
        
        public void Fill(Texture.InternalFormat internalFormat, Texture.PixelFormat pixelFormat, Texture.PixelType pixelType, nint offset, nint size, void* data)
        {
            if (size == 0) return;
#if GL_HAS_DSA
            GL.ClearNamedBufferSubData(ID, (SizedInternalFormat)internalFormat, offset, size, (PixelFormat)pixelFormat, (PixelType)pixelType, data);
#else
            // glClearBufferSubData is 4.3; write the pattern from the CPU instead
            FillFromCpu(offset, size, data);
#endif
        }

        public void FlushMemory(nint offset, nint size)
        {
            if (size == 0) return;
#if GL_HAS_DSA
            GL.FlushMappedNamedBufferRange(ID, offset, size);
#else
            // the shadow is ordinary memory, so a flush is an upload
            if (shadow != null)
            {
                BindForEdit();
                GL.BufferSubData(EditTarget, offset, size, (byte*)shadow + offset);
            }
#endif
        }


#if !GL_HAS_DSA
        private static bool warnedMapping;

        private static void WarnNoPersistentMapping()
        {
            if (warnedMapping) return;
            warnedMapping = true;
            BBLogger.Logger.Log(BBLogger.Logger.LogLevel.Warn,
                "Persistent buffer mapping needs GL 4.4; this build uploads instead");
        }

        private void FillFromCpu(nint offset, nint size, void* pattern)
        {
            // the pattern is one element wide; repeat it across the range
            byte[] scratch = new byte[size];
            byte* src = (byte*)pattern;
            for (nint i = 0; i < size; i++)
            {
                scratch[i] = src[i % 4];
            }

            fixed (byte* dst = scratch)
            {
                BindForEdit();
                GL.BufferSubData(EditTarget, offset, size, dst);
            }
        }
#endif

        public bool IsDeleted()
        {
            return ID == 0;
        }

        public void Dispose()
        {
            if (IsDeleted())
            {
                return;
            }

            if (Memory != null)
            {
#if GL_HAS_DSA
                GL.UnmapNamedBuffer(ID);
#else
                if (shadow != null)
                {
                    NativeMemory.Free(shadow);
                    shadow = null;
                }
                Memory = null;
#endif
            }

            GL.DeleteBuffer(ID);
            ID = 0;
        }

        public static void Recreate(ref Buffer buffer, MemLocation memLocation, MemAccess memAccess, nint size)
        {
            Recreate(ref buffer, memLocation, memAccess, size, null);
        }

        public static void Recreate(ref Buffer buffer, MemLocation memLocation, MemAccess memAccess, nint size, void* data = null)
        {
            buffer.Dispose();

            Buffer newBuffer = new Buffer();
            newBuffer.Allocate(memLocation, memAccess, size, data);   

            if (buffer.BackingBlock != 0)
            {
                newBuffer.BindToBufferBackedBlock(buffer.BackingBlock, buffer.BlockIndex);
            }

            buffer = newBuffer;
        }

        public static void Recreate<T>(ref TypedBuffer<T> buffer, MemLocation memLocation, MemAccess memAccess, ReadOnlySpan<T> newValues) where T : unmanaged
        {
            Recreate(ref buffer, memLocation, memAccess, newValues.Length, MemoryMarshal.GetReference(newValues));
        }

        public static void Recreate<T>(ref TypedBuffer<T> buffer, MemLocation memLocation, MemAccess memAccess, nint count, in T data) where T : unmanaged
        {
            fixed (void* ptr = &data)
            {
                Recreate(ref buffer, memLocation, memAccess, count, ptr);
            }
        }

        public static void Recreate<T>(ref TypedBuffer<T> buffer, MemLocation memLocation, MemAccess memAccess, nint count) where T : unmanaged
        {
            Recreate(ref buffer, memLocation, memAccess, count, null);
        }

        public static void Recreate<T>(ref TypedBuffer<T> buffer, MemLocation memLocation, MemAccess memAccess, nint count, void* data) where T : unmanaged
        {
            ref Buffer baseBuffer = ref Unsafe.As<TypedBuffer<T>, Buffer>(ref buffer);
            Recreate(ref baseBuffer, memLocation, memAccess, sizeof(T) * count, data);
        }
    }

    public unsafe class TypedBuffer<T> : Buffer where T : unmanaged
    {
        public new T* Memory => (T*)base.Memory;
        public int NumElements => (int)(Size / sizeof(T));

        public TypedBuffer()
            : base()
        {

        }

        public void AllocateElements(MemLocation memLocation, MemAccess memAccess, ReadOnlySpan<T> data)
        {
            AllocateElements(memLocation, memAccess, data.Length, MemoryMarshal.GetReference(data));
        }

        public void AllocateElements(MemLocation memLocation, MemAccess memAccess, nint count, in T data)
        {
            fixed (void* ptr = &data)
            {
                AllocateElements(memLocation, memAccess, count, ptr);
            }
        }

        public void AllocateElements(MemLocation memLocation, MemAccess memAccess, nint count)
        {
            AllocateElements(memLocation, memAccess, count, null);
        }

        public void AllocateElements(MemLocation memLocation, MemAccess memAccess, nint count, void* data)
        {
            Allocate(memLocation, memAccess, sizeof(T) * count, data);
        }

        public void UploadElements(ReadOnlySpan<T> data, nint startIndex = 0)
        {
            UploadElements(startIndex, data.Length, MemoryMarshal.GetReference(data));
        }

        public void UploadElements(in T data, nint startIndex = 0)
        {
            UploadElements(startIndex, 1, data);
        }

        public void UploadElements(nint startIndex, nint count, in T data)
        {
            fixed (void* ptr = &data)
            {
                UploadData(startIndex * sizeof(T), count * sizeof(T), ptr);
            }
        }

        public void DownloadElements(out T[] values)
        {
            values = new T[NumElements];
            DownloadElements(values, 0);
        }

        public void DownloadElements(Span<T> data, nint startIndex = 0)
        {
            DownloadElements(startIndex, data.Length, ref MemoryMarshal.GetReference(data));
        }

        public void DownloadElements(nint startIndex, nint count, ref T data)
        {
            fixed (void* ptr = &data)
            {
                DownloadData(startIndex * sizeof(T), count * sizeof(T), ptr);
            }
        }

        public void CopyElementsTo(TypedBuffer<T> buffer, nint readElementOffset, nint writeElementOffset, nint count)
        {
            CopyTo(buffer, readElementOffset * sizeof(T), writeElementOffset * sizeof(T), count * sizeof(T));
        }

        public void CopyElementsTo(TypedBuffer<T> buffer, nint readElementOffset = 0, nint writeElementOffset = 0)
        {
            CopyElementsTo(buffer, readElementOffset, writeElementOffset, NumElements);
        }
    }
}
