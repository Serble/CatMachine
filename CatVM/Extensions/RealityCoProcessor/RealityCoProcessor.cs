using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using CatVM.Serial;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace CatVM.Extensions;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex {
    public float X, Y, Z, W;
    public float U, V;
    public float R, G, B, A;

    public static readonly uint Stride = 10 * sizeof(float);

    public Vector4 Position {
        readonly get => new(X, Y, Z, W);
        set {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
            W = value.W;
        }
    }

    public Vector2 Uv {
        readonly get => new(U, V);
        set {
            U = value.X;
            V = value.Y;
        }
    }

    public Vector4 Color {
        readonly get => new(R, G, B, A);
        set {
            R = value.X;
            G = value.Y;
            B = value.Z;
            A = value.W;
        }
    }
}

public enum BlendSource : uint {
    PixelColor = 0,
    MemoryColor = 1,
    BlendColor = 2,
    FogColor = 3,
}

public enum BlendFactor : uint {
    PixelAlpha = 0,
    FogAlpha = 1,
    One = 2,
    Zero = 3,
}

public enum CycleMode : uint {
    Fill = 0,
    Copy = 1,
    OneCycle = 2,
    TwoCycle = 3,
}

public class RealityCoProcessor : CommandBasedSerialDevice<RealityCoProcessor.Mode>, IDisposable {
    public override uint Type => 0x015512442;
    
    private IWindow? _window;
    private GL? _gl;
    private Thread? _windowThread;
    
    private readonly ConcurrentQueue<Action> _workQueue = new();
    private readonly ManualResetEventSlim _ready = new(false);
    
    private const int VertexCacheSize = 32;
    private const uint VmVertexStride = 16;
    private uint _vao;
    private uint _vbo;          // vertex cache buffer - fixed size, updated with BufferSubData
    private uint _ebo;
    
    private const int FramebufferWidth = 512;
    private const int FramebufferHeight = 384;

    // Ping-pong framebuffers.
    // _frontFboIndex is the framebuffer containing the latest completed image.
    private readonly uint[] _fbos = new uint[2];
    private readonly uint[] _fboTextures = new uint[2];
    private int _frontFboIndex = 0;

    // Shared depth/stencil buffer attached to both FBOs.
    private uint _rbo;
    
    private uint _shader;
    private Matrix4X4<float> _currentTransform = Matrix4X4<float>.Identity;
    
    private const int TextureSlots = 8;
    private uint[] _textures = new uint[TextureSlots];
    private int _activeSlot = -1;
    
    private int _uTextureSize;
    private float _activeTextureWidth = 1;
    private float _activeTextureHeight = 1;
    
    private int _uTransform;
    private int _uUseTexture;
    private int _uTexture;
    private int _uMemory;
    private int _uResolution;
    private int _uCycleMode;

    private int _uASrc;
    private int _uBSrc;
    private int _uPSrc;
    private int _uQSrc;

    private int _uASrc2;
    private int _uBSrc2;
    private int _uPSrc2;
    private int _uQSrc2;

    private int _uFogColor;
    private int _uBlendColor;

    private Exception? _initException;
    private volatile bool _disposed;
    
    public enum Mode {
        // RSP commands
        LoadVertices    = 0x01,  // load verts into cache at offset: (address, count, cacheOffset)
        DrawTriangle    = 0x02,  // draw triangle from cache indices: (i0, i1, i2)
        SetTransform    = 0x03,  // upload MVP matrix from VM memory

        // RDP commands  
        SetTexture      = 0x10,  // bind texture from VM memory
        SetBlendMode    = 0x11,
        SetBlendMode2   = 0x12,
        SetBlendColor   = 0x13,
        SetFogColor     = 0x14,
        SetCycleMode    = 0x15,
    
        // Control
        ExecuteList     = 0x20,  // run a display list at address
        EndList         = 0x21,
        ClearBuffers    = 0x22,
    }

    public RealityCoProcessor() {
        EnsureContext();
    }
    
    protected override int GetArgCount(Mode mode) {
        return mode switch {
            // RSP
            Mode.LoadVertices  => 3,  // address, count, cacheOffset
            Mode.DrawTriangle  => 3,  // i0, i1, i2 (indices into vertex cache)
            Mode.SetTransform  => 1,  // address (4x4 matrix in VM memory)

            // RDP
            Mode.SetTexture    => 4,  // address, width, height
            Mode.SetBlendMode  => 1,  // blend mode flags for first cycle (blender)
            Mode.SetBlendMode2 => 1,  // blend mode flag for second cycle (blender)
            Mode.SetBlendColor => 1,  // packed RGBA u32
            Mode.SetFogColor   => 1,  // packed RGBA u32
            Mode.SetCycleMode  => 1,  // how many cycles the blender should do (1 or 2)

            // Control
            Mode.ExecuteList   => 1,  // address of display list
            Mode.EndList       => 0,
            Mode.ClearBuffers  => 1,  // packed RGBA u32
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    protected override void RunMode(CatVM vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.LoadVertices: {
                uint address = args[0];
                uint count = args[1];
                uint cacheOffset = args[2];

                if (count == 0) {
                    break;
                }

                if (cacheOffset >= VertexCacheSize || cacheOffset + count > VertexCacheSize) {
                    throw new Exception(
                        $"Vertex cache upload out of range. cacheOffset={cacheOffset}, count={count}, cacheSize={VertexCacheSize}"
                    );
                }

                Vertex[] verts = ReadVerts(address, count, vm);

                _workQueue.Enqueue(() => {
                    _gl!.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
                    _gl.BufferSubData(
                        BufferTargetARB.ArrayBuffer,
                        (nint)(cacheOffset * Vertex.Stride),
                        verts.AsSpan()
                    );
                });

                break;
            }
            case Mode.DrawTriangle: {
                 uint i0 = args[0]; 
                 uint i1 = args[1]; 
                 uint i2 = args[2];
                 
                 if (i0 >= VertexCacheSize || i1 >= VertexCacheSize || i2 >= VertexCacheSize) { 
                     throw new Exception($"Triangle index out of range: {i0}, {i1}, {i2}"); 
                 }
                 
                 _workQueue.Enqueue(() => { 
                     uint[] indices = [i0, i1, i2];
                     int backIndex = GetBackFboIndex();

                     CopyFrontColorToBackColor(backIndex);
                     BindBackFboForDrawing(backIndex);
 
                     _gl!.Disable(EnableCap.Blend); // shader does N64-style blending approximation
                     _gl.Enable(EnableCap.DepthTest);

                     _gl.UseProgram(_shader);
 
                     // Normal texture source: uTexture on texture unit 0.
                     _gl.ActiveTexture(TextureUnit.Texture0);
 
                     if (_activeSlot >= 0 && _activeSlot < TextureSlots) {
                         _gl.BindTexture(TextureTarget.Texture2D, _textures[_activeSlot]);
                         _gl.Uniform1(_uUseTexture, 1);
                     }
                     else {
                         _gl.BindTexture(TextureTarget.Texture2D, 0);
                         _gl.Uniform1(_uUseTexture, 0);
                     }

                     _gl.Uniform1(_uTexture, 0);
                     
                      // MemoryColor source: previous framebuffer on texture unit 1.
                     _gl.ActiveTexture(TextureUnit.Texture1);
                     _gl.BindTexture(TextureTarget.Texture2D, _fboTextures[_frontFboIndex]);
                     _gl.Uniform1(_uMemory, 1);

                     _gl.Uniform2(_uResolution, (float)FramebufferWidth, (float)FramebufferHeight);

                     _gl.BindVertexArray(_vao);
                     _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
                     _gl.BufferData(BufferTargetARB.ElementArrayBuffer, indices.AsSpan(), BufferUsageARB.StreamDraw);

                     unsafe {
                         _gl.DrawElements(PrimitiveType.Triangles, 3, DrawElementsType.UnsignedInt, (void*)0);
                     }

                     _gl.BindVertexArray(0);

                     // Restore predictable texture unit.
                     _gl.ActiveTexture(TextureUnit.Texture0);

                     /*
                      * The back FBO now contains the latest framebuffer image.
                      * Make it the new front.
                      */
                     FinishPingPongDraw(backIndex);
                });

                break;
            }
            case Mode.SetTransform: {
                uint address = args[0];
                
                // N64 matrices are s15.16 fixed point, 4x4 = 16 values
                // stored as two separate 16-bit halves: integer part then fractional part
                float[] m = new float[16];
                for (int i = 0; i < 16; i++) {
                    int raw = ((short)vm.Read16(address + (uint)(i * 2)) << 16) | vm.Read16(address + 32 + (uint)(i * 2));
                    m[i] = raw / 65536f;
                }
                
                _currentTransform = new Matrix4X4<float>(
                    m[0],  m[1],  m[2],  m[3],
                    m[4],  m[5],  m[6],  m[7],
                    m[8],  m[9],  m[10], m[11],
                    m[12], m[13], m[14], m[15]
                );
                
                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.UniformMatrix4(_uTransform, 1, true, m.AsSpan());
                });
                
                break;
            }
            case Mode.SetTexture: {
                uint address = args[0];
                uint width = args[1];
                uint height = args[2];
                uint slot = args[3];

                if (slot >= TextureSlots) {
                    throw new Exception($"Invalid texture slot {slot}. Valid range is 0-{TextureSlots - 1}.");
                }

                if (width == 0 || height == 0) {
                    throw new Exception($"Invalid texture size {width}x{height}.");
                }

                ulong pixelCount64 = (ulong)width * height;
                if (pixelCount64 > int.MaxValue / 4) {
                    throw new Exception($"Texture too large: {width}x{height}");
                }

                int pixelCount = checked((int)pixelCount64);
                byte[] rgba8 = new byte[checked(pixelCount * 4)];

                for (int i = 0; i < pixelCount; i++) {
                    uint pixelAddress = address + (uint)(i * 2);
                    ushort pixel = vm.Read16(pixelAddress);

                    byte r = (byte)(((pixel >> 11) & 0x1F) * 255 / 31);
                    byte g = (byte)(((pixel >> 6) & 0x1F) * 255 / 31);
                    byte b = (byte)(((pixel >> 1) & 0x1F) * 255 / 31);
                    byte a = (byte)((pixel & 0x1) * 255);

                    int dst = i * 4;

                    rgba8[dst + 0] = r;
                    rgba8[dst + 1] = g;
                    rgba8[dst + 2] = b;
                    rgba8[dst + 3] = a;
                }

                int slotIndex = (int)slot;

                _workQueue.Enqueue(() => {
                    _gl!.ActiveTexture(TextureUnit.Texture0);
                    _gl.BindTexture(TextureTarget.Texture2D, _textures[slotIndex]);

                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba8.AsSpan());
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

                    _activeSlot = slotIndex;
                    _activeTextureWidth = width;
                    _activeTextureHeight = height;

                    _gl.UseProgram(_shader);
                    _gl.Uniform2(_uTextureSize, (float)width, (float)height);
                });

                break;
            }
            case Mode.SetBlendMode: { 
                uint flags = args[0];

                BlendFactor aSrc = (BlendFactor)((flags >> 12) & 0xF);
                BlendFactor bSrc = (BlendFactor)((flags >> 8) & 0xF);
                BlendSource pSrc = (BlendSource)((flags >> 4) & 0xF);
                BlendSource qSrc = (BlendSource)(flags & 0xF);

                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.Uniform1(_uASrc, (int)aSrc);
                    _gl.Uniform1(_uBSrc, (int)bSrc);
                    _gl.Uniform1(_uPSrc, (int)pSrc);
                    _gl.Uniform1(_uQSrc, (int)qSrc);
                });

                break;
            }
            case Mode.SetBlendMode2: {
                uint flags = args[0];

                BlendFactor aSrc2 = (BlendFactor)((flags >> 12) & 0xF);
                BlendFactor bSrc2 = (BlendFactor)((flags >> 8) & 0xF);
                BlendSource pSrc2 = (BlendSource)((flags >> 4) & 0xF);
                BlendSource qSrc2 = (BlendSource)(flags & 0xF);

                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.Uniform1(_uASrc2, (int)aSrc2);
                    _gl.Uniform1(_uBSrc2, (int)bSrc2);
                    _gl.Uniform1(_uPSrc2, (int)pSrc2);
                    _gl.Uniform1(_uQSrc2, (int)qSrc2);
                });

                break;
            }
            case Mode.SetBlendColor: {
                uint packed = args[0];

                float r = ((packed >> 24) & 0xFF) / 255f;
                float g = ((packed >> 16) & 0xFF) / 255f;
                float b = ((packed >> 8) & 0xFF) / 255f;
                float a = (packed & 0xFF) / 255f;

                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.Uniform4(_uBlendColor, r, g, b, a);
                });

                break;
            }
            case Mode.SetFogColor: {
                uint packed = args[0];

                float r = ((packed >> 24) & 0xFF) / 255f;
                float g = ((packed >> 16) & 0xFF) / 255f;
                float b = ((packed >> 8) & 0xFF) / 255f;
                float a = (packed & 0xFF) / 255f;

                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.Uniform4(_uFogColor, r, g, b, a);
                });

                break;
            }
            case Mode.SetCycleMode: {
                CycleMode cm = (CycleMode)args[0];

                _workQueue.Enqueue(() => {
                    _gl!.UseProgram(_shader);
                    _gl.Uniform1(_uCycleMode, (int)cm);
                });

                break;
            }
            case Mode.ExecuteList: {
                uint address = args[0];
                Stack<uint> returnStack = new();
    
                while (true) {
                    uint opcode = vm.ReadWord(address);
                    address += 4;
        
                    Mode cmd = (Mode)opcode;
        
                    if (cmd == Mode.EndList) {
                        if (returnStack.Count > 0)
                            address = returnStack.Pop(); // return from sub-list
                        else
                            break; // top level done
                        continue;
                    }
        
                    int argCount = GetArgCount(cmd);
                    List<uint> cmdArgs = new();
                    for (int i = 0; i < argCount; i++) {
                        cmdArgs.Add(vm.ReadWord(address));
                        address += 4;
                    }
        
                    if (cmd == Mode.ExecuteList) {
                        returnStack.Push(address); // save return address
                        address = cmdArgs[0];      // jump to sub-list
                        continue;
                    }
        
                    RunMode(vm, cmd, cmdArgs);
                }
                break;
            }
            case Mode.EndList: {
                break;
            }
            case Mode.ClearBuffers: {
                uint packed = args[0];
                
                float r = ((packed >> 24) & 0xFF) / 255f;
                float g = ((packed >> 16) & 0xFF) / 255f;
                float b = ((packed >> 8) & 0xFF) / 255f;
                float a = (packed & 0xFF) / 255f;
                
                _workQueue.Enqueue(() => {
                    _gl!.ClearColor(r, g, b, a);
                    _gl.ClearDepth(1.0f);

                    // clear both ping-pong color buffers.
                    for (int i = 0; i < 2; i++) {
                        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbos[i]);
                        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    }

                    _frontFboIndex = 0;

                    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbos[_frontFboIndex]);
                });

                break;
            }
        }
    }

    private void EnsureContext() {
        SdlWindowing.Use();

        _windowThread = new Thread(() => {
            try {
                WindowOptions options = WindowOptions.Default with {
                    Title = "CatVM RCP",
                    Size = new Vector2D<int>(512, 384),
                    WindowBorder = WindowBorder.Fixed,
                    API = new GraphicsAPI(
                        ContextAPI.OpenGL,
                        ContextProfile.Core,
                        ContextFlags.ForwardCompatible,
                        new APIVersion(4, 6)
                    ),
                    IsVisible = true,
                };

                _window = Window.Create(options);

                _window.Load += () => {
                    _gl = _window.CreateOpenGL();

                    _gl.Enable(EnableCap.DepthTest);
                    _gl.DepthFunc(DepthFunction.Less);

                    SetupBuffers();
                    SetupFramebuffer();
                    SetupShader();
                    SetupTextures();

                    _ready.Set();
                };

                _window.Render += delta => {
                    if (_gl == null) {
                        return;
                    }

                    _gl.Viewport(0, 0, FramebufferWidth, FramebufferHeight);
                    _gl.Disable(EnableCap.Blend);

                    while (_workQueue.TryDequeue(out Action? work)) {
                        try {
                            work?.Invoke();
                        }
                        catch (Exception ex) {
                            Console.Error.WriteLine($"RCP GL work item failed: {ex}");
                        }
                    }

                    if (_disposed || _gl == null) {
                        return;
                    }

                    _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbos[_frontFboIndex]);
                    _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

                    _gl.BlitFramebuffer(
                        0, 0, FramebufferWidth, FramebufferHeight,
                        0, 0, FramebufferWidth, FramebufferHeight,
                        ClearBufferMask.ColorBufferBit,
                        BlitFramebufferFilter.Nearest
                    );

                    _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    _window?.SwapBuffers();
                };

                _window.Run();

                _window.Dispose();
            }
            catch (Exception ex) {
                _initException = ex;
                _ready.Set();
            }
        }) {
            IsBackground = true,
            Name = "RCP Window Thread"
        };

        _windowThread.Start();
        _ready.Wait();

        if (_initException != null) {
            throw new Exception("Failed to initialize RCP OpenGL context.", _initException);
        }
    }
    
    private void SetupBuffers() {
        _vao = _gl!.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        
        Vertex[] emptyVerts = new Vertex[VertexCacheSize];
        _gl.BufferData(BufferTargetARB.ArrayBuffer, emptyVerts.AsSpan(), BufferUsageARB.DynamicDraw);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        unsafe {
            // position: offset 0
            _gl.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, Vertex.Stride, (void*)0);
            _gl.EnableVertexAttribArray(0);

            // uv: offset 16 (4 * 4)
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Vertex.Stride, (void*)(4 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            // color: offset 24 (6 * 4)
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, Vertex.Stride, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
        }

        _gl.BindVertexArray(0);
    }
    
    private void SetupFramebuffer() {
        _gl!.GenFramebuffers(_fbos.AsSpan());
        _gl.GenTextures(_fboTextures.AsSpan());

        // One shared depth/stencil renderbuffer for both ping-pong FBOs.
        _rbo = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, FramebufferWidth, FramebufferHeight);

        for (int i = 0; i < 2; i++) {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbos[i]);

            _gl.BindTexture(TextureTarget.Texture2D, _fboTextures[i]);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, FramebufferWidth, FramebufferHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _fboTextures[i], 0);

            // Attach the same depth/stencil renderbuffer to both FBOs.
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _rbo);

            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete) {
                throw new Exception($"Ping-pong framebuffer {i} incomplete: {status}");
            }

            _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _gl.ClearDepth(1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        _frontFboIndex = 0;

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    private int GetBackFboIndex() {
        return 1 - _frontFboIndex;
    }

    private void CopyFrontColorToBackColor(int backIndex) {
        _gl!.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbos[_frontFboIndex]);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fbos[backIndex]);

        _gl.BlitFramebuffer(
            0, 0, FramebufferWidth, FramebufferHeight,
            0, 0, FramebufferWidth, FramebufferHeight,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Nearest
        );
    }

    private void BindBackFboForDrawing(int backIndex) {
        _gl!.BindFramebuffer(FramebufferTarget.Framebuffer, _fbos[backIndex]);
        _gl.Viewport(0, 0, FramebufferWidth, FramebufferHeight);
    }

    private void FinishPingPongDraw(int backIndex) {
        _frontFboIndex = backIndex;
    }
    
    private void SetupShader() {
        string vert = ReadResource("rcp_default.vert");
        string frag = ReadResource("rcp_default.frag");

        uint vs = _gl!.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vs, vert);
        _gl.CompileShader(vs);
        _gl.GetShader(vs, ShaderParameterName.CompileStatus, out int vsStatus);
        if (vsStatus == 0) {
            throw new Exception($"Vertex shader error: {_gl.GetShaderInfoLog(vs)}");
        }

        uint fs = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fs, frag);
        _gl.CompileShader(fs);
        _gl.GetShader(fs, ShaderParameterName.CompileStatus, out int fsStatus);
        if (fsStatus == 0) {
            throw new Exception($"Fragment shader error: {_gl.GetShaderInfoLog(fs)}");
        }

        _shader = _gl.CreateProgram();
        _gl.AttachShader(_shader, vs);
        _gl.AttachShader(_shader, fs);
        _gl.LinkProgram(_shader);
        _gl.GetProgram(_shader, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0) {
            throw new Exception($"Shader link error: {_gl.GetProgramInfoLog(_shader)}");
        }

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        _gl.UseProgram(_shader);

        _uTransform = _gl.GetUniformLocation(_shader, "uTransform");
        _uUseTexture = _gl.GetUniformLocation(_shader, "uUseTexture");
        _uTexture = _gl.GetUniformLocation(_shader, "uTexture");
        _uMemory = _gl.GetUniformLocation(_shader, "uMemory");
        _uResolution = _gl.GetUniformLocation(_shader, "uResolution");
        _uCycleMode = _gl.GetUniformLocation(_shader, "uCycleMode");

        _uASrc = _gl.GetUniformLocation(_shader, "uASrc");
        _uBSrc = _gl.GetUniformLocation(_shader, "uBSrc");
        _uPSrc = _gl.GetUniformLocation(_shader, "uPSrc");
        _uQSrc = _gl.GetUniformLocation(_shader, "uQSrc");

        _uASrc2 = _gl.GetUniformLocation(_shader, "uASrc2");
        _uBSrc2 = _gl.GetUniformLocation(_shader, "uBSrc2");
        _uPSrc2 = _gl.GetUniformLocation(_shader, "uPSrc2");
        _uQSrc2 = _gl.GetUniformLocation(_shader, "uQSrc2");

        _uFogColor = _gl.GetUniformLocation(_shader, "uFogColor");
        _uBlendColor = _gl.GetUniformLocation(_shader, "uBlendColor");
        _uTextureSize = _gl.GetUniformLocation(_shader, "uTextureSize");

        float[] identity = [
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        ];

        _gl.UniformMatrix4(_uTransform, 1, false, identity.AsSpan());
        _gl.Uniform1(_uUseTexture, 0);
        _gl.Uniform1(_uTexture, 0);
        _gl.Uniform1(_uMemory, 1);
        _gl.Uniform1(_uCycleMode, (int)CycleMode.OneCycle);
        _gl.Uniform2(_uResolution, 512f, 384f);
        
        // Default blend mode: output pixel color directly.
        _gl.Uniform1(_uASrc, (int)BlendFactor.PixelAlpha);
        _gl.Uniform1(_uBSrc, (int)BlendFactor.Zero);
        _gl.Uniform1(_uPSrc, (int)BlendSource.PixelColor);
        _gl.Uniform1(_uQSrc, (int)BlendSource.MemoryColor);

        // Same default for cycle 2.
        _gl.Uniform1(_uASrc2, (int)BlendFactor.PixelAlpha);
        _gl.Uniform1(_uBSrc2, (int)BlendFactor.Zero);
        _gl.Uniform1(_uPSrc2, (int)BlendSource.PixelColor);
        _gl.Uniform1(_uQSrc2, (int)BlendSource.MemoryColor);
    }
    
    private void SetupTextures() {
        _gl!.GenTextures(_textures.AsSpan());
        foreach (uint tex in _textures) {
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;

        if (_windowThread == null || _window == null) {
            GC.SuppressFinalize(this);
            return;
        }

        ManualResetEventSlim done = new(false);

        _workQueue.Enqueue(() => {
            try {
                DeleteGlResources();

                _window?.Close();
            }
            finally {
                done.Set();
            }
        });

        done.Wait(TimeSpan.FromSeconds(2));

        if (_windowThread.IsAlive) {
            _windowThread.Join(TimeSpan.FromSeconds(2));
        }

        _gl?.Dispose();

        _gl = null;
        _window = null;

        GC.SuppressFinalize(this);
    }
    
    float ReadFixed(CatVM vm, uint address) {
        short raw = (short)vm.Read16(address);
        return raw / 32f;
    }

    Vertex[] ReadVerts(uint address, uint count, CatVM vm) {
        int vertexCount = checked((int)count);

        Vertex[] verts = new Vertex[vertexCount];

        for (int i = 0; i < vertexCount; i++) {
            uint base_ = address + (uint)(i * VmVertexStride);

            verts[i] = new Vertex {
                Position = new Vector4(
                    ReadFixed(vm, base_ + 0),  // x, s10.5
                    ReadFixed(vm, base_ + 2),  // y, s10.5
                    ReadFixed(vm, base_ + 4),  // z, s10.5
                    ReadFixed(vm, base_ + 6)   // w, s10.5
                ),

                Uv = new Vector2(
                    ReadFixed(vm, base_ + 8),   // u, s10.5
                    ReadFixed(vm, base_ + 10)   // v, s10.5
                ),

                Color = new Vector4(
                    vm.Read8(base_ + 12) / 255f,
                    vm.Read8(base_ + 13) / 255f,
                    vm.Read8(base_ + 14) / 255f,
                    vm.Read8(base_ + 15) / 255f
                )
            };
        }

        return verts;
    }
    
    public static string ReadResource(string name) {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(name));

        if (resourceName == null) {
            string available = string.Join(Environment.NewLine, assembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"Embedded resource '{name}' not found. Available resources:{Environment.NewLine}{available}"
            );
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"Resource stream '{resourceName}' could not be opened.");

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
    
    private void DeleteGlResources() {
        if (_gl == null) {
            return;
        }

        if (_vbo != 0) {
            _gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_ebo != 0) {
            _gl.DeleteBuffer(_ebo);
            _ebo = 0;
        }

        if (_vao != 0) {
            _gl.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_shader != 0) {
            _gl.DeleteProgram(_shader);
            _shader = 0;
        }

        for (int i = 0; i < 2; i++) {
            if (_fboTextures[i] != 0) {
                _gl.DeleteTexture(_fboTextures[i]);
                _fboTextures[i] = 0;
            }

            if (_fbos[i] != 0) {
                _gl.DeleteFramebuffer(_fbos[i]);
                _fbos[i] = 0;
            }
        }

        if (_rbo != 0) {
            _gl.DeleteRenderbuffer(_rbo);
            _rbo = 0;
        }

        for (int i = 0; i < _textures.Length; i++) {
            if (_textures[i] != 0) {
                _gl.DeleteTexture(_textures[i]);
                _textures[i] = 0;
            }
        }
    }
}
