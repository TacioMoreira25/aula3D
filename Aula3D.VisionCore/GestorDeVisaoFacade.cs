using OpenCvSharp;
using System.Runtime.InteropServices;
using Aula3D.VisionCore.Interfaces;
using Aula3D.VisionCore.Processamento;

using System.Reflection;

namespace Aula3D.VisionCore
{
    /// <summary>
    /// Única classe pública acessada pelo Godot.
    /// Esconde toda dependência do OpenCV e orquestra o pipeline PDI:
    /// FiltroEspacial → ExtratorHu → ClassificadorDeGestos.
    ///
    /// Implementa <see cref="IGestureProvider"/>.
    /// </summary>
    public class GestorDeVisaoFacade : IGestureProvider, IDisposable
    {
        static GestorDeVisaoFacade()
        {
            try
            {
                // Intercepta e carrega a biblioteca nativa do OpenCV corretamente, mitigando erros no Godot (DllNotFoundException).
                // Adapta a extensão automaticamente para suportar colegas utilizando Windows (.dll) ou Linux/Pop!_OS/Fedora (.so).
                NativeLibrary.SetDllImportResolver(typeof(OpenCvSharp.Mat).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == "OpenCvSharpExtern")
                    {
                        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                        string fileName = isWindows ? "OpenCvSharpExtern.dll" : "libOpenCvSharpExtern.so";

                        string cwd = System.IO.Directory.GetCurrentDirectory();
                        string customPath = System.IO.Path.Combine(cwd, fileName);

                        if (System.IO.File.Exists(customPath))
                        {
                            return NativeLibrary.Load(customPath);
                        }
                        string altPath = "/usr/local/lib/libOpenCvSharpExtern.so";
                        if (System.IO.File.Exists(altPath))
                        {
                            return NativeLibrary.Load(altPath);
                        }
                    }
                    return IntPtr.Zero;
                });
            }
            catch {}
        }

        // -- IGestureProvider --
        public float X             { get; private set; }
        public float Y             { get; private set; }
        public bool  GestoDetectado { get; private set; }   // true = ABERTA, false = FECHADA
        public bool  HandDetected   { get; private set; }

        // -- estado adicional exposto para debug no VisionConsole --
        public bool  IsRunning  { get; private set; }
        public bool  IsHandOpen => GestoDetectado;
        public float Z          { get; private set; }       // estimativa de profundidade por área
        public byte[]? FrameBuffer { get; private set; }
        public int CurrentFPS { get; private set; }
        public long CurrentRAM { get; private set; }
        
        public int DebugViewIndex { get; set; } = 0; // 0 = Original, 1 = FFT, 2 = Mask, 3 = Canny

        private CancellationTokenSource? _cts;
        private Task?                    _visionTask;

        public void Iniciar()
        {
            if (IsRunning) return;
            _cts        = new CancellationTokenSource();
            IsRunning   = true;
            _visionTask = Task.Run(() => LoopDeVisao(_cts.Token), _cts.Token);
        }

        public void Parar()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            _visionTask?.Wait();
            IsRunning = false;
        }

        private void LoopDeVisao(CancellationToken token)
        {
            using var capture  = new VideoCapture(0);
            using var frame    = new Mat();
            using var filtro   = new FiltroEspacial();

            if (!capture.IsOpened()) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                double fps = 1000.0 / stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();
                CurrentFPS = (int)Math.Round(fps);
                CurrentRAM = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

                capture.Read(frame);
                if (frame.Empty()) continue;

                Cv2.Flip(frame, frame, FlipMode.Y);

                int sideWidth  = Math.Min(300, frame.Width  / 2);
                int sideHeight = frame.Height - 60;
                Rect roi       = new Rect(10, 30, sideWidth, sideHeight);

                using Mat frameRoi = new Mat(frame, roi);

                // --- AMOSTRAGEM: Reduz resolução para performance do FFT ---
                double scale = 150.0 / sideWidth;
                int newHeight = (int)(sideHeight * scale);
                using Mat lowResRoi = new Mat();
                Cv2.Resize(frameRoi, lowResRoi, new Size(150, newHeight), 0, 0, InterpolationFlags.Area);

                // --- FFT 2D: Remoção de ruídos periódicos ---
                // Para simplificar e evitar distorção de cores, aplicamos FFT no Luma (Brilho) ou ignoramos a cor
                // O ideal é remover o ruido de fundo mas preservar a cor.
                // Usaremos no filtro espacial para ajudar o Canny
                
                // Aplicamos o filtro de Visão (Cor HSV, CLAHE, Morfologia, Canny) no lowResRoi
                filtro.Aplicar(lowResRoi);
                Point[][] contornos = filtro.ExtrairContornos(minArea: 3000 * (scale * scale));

                if (contornos.Length > 0)
                {
                    Point[] contornoOriginal = contornos[0];
                    
                    // --- RE-AMOSTRAGEM: Escala do Contorno de volta para resolução nativa ---
                    Point[] contornoScaleBack = new Point[contornoOriginal.Length];
                    for (int i = 0; i < contornoOriginal.Length; i++)
                    {
                        contornoScaleBack[i] = new Point(
                            (int)(contornoOriginal[i].X / scale),
                            (int)(contornoOriginal[i].Y / scale)
                        );
                    }
                    
                    var resultado    = new HandTrackingResult { HandDetected = true, Contour = contornoScaleBack };

                    ExtratorHu.ExtrairGeometria(contornoScaleBack, resultado);
                    ClassificadorDeGestos.Classificar(contornoScaleBack, resultado);

                    HandDetected    = resultado.HandDetected;
                    GestoDetectado  = resultado.IsHandOpen;

                    // --- DESENHOS DE DEBUG E MELHORIA VISUAL ---
                    Cv2.DrawContours(frameRoi, new[] { resultado.Contour }, 0, new Scalar(0, 255, 0), 2);

                    if (resultado.DefectPoints != null)
                        foreach (var pt in resultado.DefectPoints)
                            Cv2.Circle(frameRoi, pt, 4, new Scalar(255, 0, 255), -1);

                    string textoEstado = GestoDetectado ? "ABERTA (Rotacao)" : "FECHADA (Translacao)";
                    Scalar corTexto = GestoDetectado ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

                    Cv2.PutText(frameRoi, textoEstado,
                        new Point(resultado.BoundingRect.X, Math.Max(20, resultado.BoundingRect.Y - 10)),
                        HersheyFonts.HersheySimplex, 0.6, corTexto, 2);
                    // -------------------------------------------

                    if (resultado.CenterOfMass.X > 0)
                    {
                        X = resultado.CenterOfMass.X;
                        Y = resultado.CenterOfMass.Y;

                        // Desenha o centro de massa
                        Cv2.Circle(frameRoi, resultado.CenterOfMass, 6, new Scalar(0, 255, 255), -1);

                        double area = resultado.BoundingRect.Width * resultado.BoundingRect.Height;
                        Z = (float)(Math.Sqrt(area) / 100.0);
                    }
                }
                else
                {
                    HandDetected = false;
                }

                // Desenha a região de interesse para saber onde colocar a mão
                Cv2.Rectangle(frame, roi, new Scalar(255, 255, 0), 2);
                Cv2.PutText(frame, "AREA DE CONTROLE", new Point(roi.X + 5, roi.Y + 15),
                    HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);

                using Mat frameToEncode = new Mat();
                using Mat debugResized = new Mat();
                int textY = 25; // Abaixei um pouco para não cortar
                Scalar textColor = new Scalar(0, 255, 0); // Verde

                Mat sourceMat;
                string viewText;

                if (DebugViewIndex == 1 && !filtro.MatFFT.Empty())
                {
                    sourceMat = filtro.MatFFT;
                    viewText = "Visualizacao: 2. FFT";
                }
                else if (DebugViewIndex == 2 && !filtro.GetMask().Empty())
                {
                    sourceMat = filtro.GetMask();
                    viewText = "Visualizacao: 3. HSV";
                }
                else if (DebugViewIndex == 3 && !filtro.MatCanny.Empty())
                {
                    sourceMat = filtro.MatCanny;
                    viewText = "Visualizacao: 4. Canny";
                }
                else 
                {
                    sourceMat = frame;
                    viewText = "Visualizacao: 1. Real";
                }

                // Aplica Resize e Color Convert uma uníca vez para a imagem selecionada
                if (sourceMat.Width != frame.Width || sourceMat.Height != frame.Height)
                {
                    Cv2.Resize(sourceMat, debugResized, frame.Size(), 0, 0, InterpolationFlags.Linear);
                    if (debugResized.Channels() == 1)
                        Cv2.CvtColor(debugResized, frameToEncode, ColorConversionCodes.GRAY2BGR);
                    else
                        debugResized.CopyTo(frameToEncode);
                }
                else
                {
                    sourceMat.CopyTo(frameToEncode);
                }

                Cv2.PutText(frameToEncode, viewText, new Point(10, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 2);

                int rightX = frameToEncode.Width - 110;
                Cv2.PutText(frameToEncode, $"FPS: {CurrentFPS}", new Point(rightX, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 2);
                Cv2.PutText(frameToEncode, $"RAM: {CurrentRAM}MB", new Point(rightX, textY + 20), HersheyFonts.HersheySimplex, 0.5, textColor, 2);

                // Codifica o frame JÁ DESENHADO em formato JPG leve
                var encodeParams = new ImageEncodingParam[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 70) };
                Cv2.ImEncode(".jpg", frameToEncode, out byte[] buffer, encodeParams);
                FrameBuffer = buffer;

                Task.Delay(16, token).Wait();
            }
        }

        public void Dispose() => Parar();
    }
}
