using OpenCvSharp;
using System.Runtime.InteropServices;
using Aula3D.VisionCore.Interfaces;
using Aula3D.VisionCore.Processamento;
using System.Reflection;

namespace Aula3D.VisionCore
{
    public class GestorDeVisaoFacade : IGestureProvider, IDisposable
    {
        static GestorDeVisaoFacade()
        {
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(OpenCvSharp.Mat).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == "OpenCvSharpExtern")
                    {
                        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                        string fileName = isWindows ? "OpenCvSharpExtern.dll" : "libOpenCvSharpExtern.so";

                        string cwd = System.IO.Directory.GetCurrentDirectory();
                        string customPath = System.IO.Path.Combine(cwd, fileName);

                        if (System.IO.File.Exists(customPath)) return NativeLibrary.Load(customPath);
                        
                        string altPath = "/usr/local/lib/libOpenCvSharpExtern.so";
                        if (System.IO.File.Exists(altPath)) return NativeLibrary.Load(altPath);
                    }
                    return IntPtr.Zero;
                });
            }
            catch {}
        }

        public float X             { get; private set; }
        public float Y             { get; private set; }
        public bool  GestoDetectado { get; private set; }
        public bool  HandDetected   { get; private set; }

        public bool  IsRunning  { get; private set; }
        public bool  IsHandOpen => GestoDetectado;
        public float Z          { get; private set; }
        public byte[]? FrameBuffer { get; private set; }
        public int CurrentFPS { get; private set; }
        public long CurrentRAM { get; private set; }
        public double[]? UltimosMomentosHu { get; private set; }
        
        public int DebugViewIndex { get; set; } = 0; // 0 = Original, 1 = Neural Mask

        private int _cameraIndex;
        private CancellationTokenSource? _cts;
        private Task?                    _visionTask;

        public void Iniciar(int cameraIndex = 0)
        {
            if (IsRunning) return;
            _cameraIndex = cameraIndex;
            _cts        = new CancellationTokenSource();
            IsRunning   = true;
            _visionTask = Task.Run(() => LoopDeVisao(_cts.Token), _cts.Token);
        }

        public void SalvarAssinaturaAberta()
        {
            if (UltimosMomentosHu != null) ClassificadorDeGestos.SalvarAssinatura("ABERTA", UltimosMomentosHu);
        }
        
        public void SalvarAssinaturaFechada()
        {
            if (UltimosMomentosHu != null) ClassificadorDeGestos.SalvarAssinatura("FECHADA", UltimosMomentosHu);
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
            try
            {
                using var capture = new VideoCapture(_cameraIndex);
                if (!capture.IsOpened())
                {
                    Console.WriteLine($"\n[ERRO CRITICO] Nao foi possivel abrir a camera no indice {_cameraIndex}. O OpenCV nao a encontrou.");
                    return;
                }

                string baseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string caminhoOnnx = System.IO.Path.Combine(baseDir, "modelo_mao.onnx");

                if (!System.IO.File.Exists(caminhoOnnx))
                {
                    Console.WriteLine($"\n[ERRO CRITICO] Nao foi encontrado o modelo ONNX em: '{caminhoOnnx}'.");
                    return;
                }

                using var frame = new Mat();
                using var kalman = new SuavizadorKalman();
                using var segmentador = new SegmentadorNeural(caminhoOnnx);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long frameCount = 0;
            Mat ultimaMascara = new Mat();

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

                frameCount++;
                bool processarCargaPesada = frameCount % 3 == 0; 

                if (processarCargaPesada)
                {
                    if (!ultimaMascara.Empty()) ultimaMascara.Dispose();
                    ultimaMascara = segmentador.ObterMascara(frameRoi);

                    Cv2.FindContours(ultimaMascara, out Point[][] contornos, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                    Point[]? melhorContorno = null;
                    double maxArea = 0;
                    
                    foreach (var c in contornos)
                    {
                        double area = Cv2.ContourArea(c);
                        if (area > 3000 && area > maxArea)
                        {
                            maxArea = area;
                            melhorContorno = c;
                        }
                    }

                    if (melhorContorno != null)
                    {
                        var resultado = new HandTrackingResult { HandDetected = true, Contour = melhorContorno };

                        ExtratorHu.ExtrairGeometria(melhorContorno, resultado);
                        resultado.HuMoments = ExtratorHu.CalcularMomentosHu(melhorContorno);
                        UltimosMomentosHu = resultado.HuMoments;
                        
                        ClassificadorDeGestos.Classificar(melhorContorno, resultado);

                        HandDetected    = resultado.HandDetected;
                        GestoDetectado  = resultado.IsHandOpen;

                        var posCorrigida = kalman.Corrigir(resultado.CenterOfMass.X, resultado.CenterOfMass.Y);
                        X = posCorrigida.X;
                        Y = posCorrigida.Y;

                        Cv2.DrawContours(frameRoi, new[] { resultado.Contour }, 0, new Scalar(0, 255, 0), 2);

                        if (resultado.DefectPoints != null)
                            foreach (var pt in resultado.DefectPoints)
                                Cv2.Circle(frameRoi, pt, 4, new Scalar(255, 0, 255), -1);

                        string textoEstado = GestoDetectado ? "ABERTA (Rotacao)" : "FECHADA (Translacao)";
                        Scalar corTexto = GestoDetectado ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

                        Cv2.PutText(frameRoi, textoEstado,
                            new Point(resultado.BoundingRect.X, Math.Max(20, resultado.BoundingRect.Y - 10)),
                            HersheyFonts.HersheySimplex, 0.6, corTexto, 2);

                        Cv2.Circle(frameRoi, resultado.CenterOfMass, 6, new Scalar(0, 255, 255), -1);

                        double areaDetectada = resultado.BoundingRect.Width * resultado.BoundingRect.Height;
                        Z = (float)(Math.Sqrt(areaDetectada) / 100.0);
                    }
                    else
                    {
                        HandDetected = false;
                    }
                }
                else
                {
                    if (HandDetected)
                    {
                        var pred = kalman.ObterPredicao();
                        X = pred.X;
                        Y = pred.Y;

                        Cv2.Circle(frameRoi, new Point((int)X, (int)Y), 6, new Scalar(0, 165, 255), -1);
                    }
                }

                Cv2.Rectangle(frame, roi, new Scalar(255, 255, 0), 2);
                Cv2.PutText(frame, "AREA DE CONTROLE", new Point(roi.X + 5, roi.Y + 15),
                    HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);

                using Mat frameToEncode = new Mat();
                int textY = 25;
                Scalar textColor = new Scalar(0, 255, 0);

                if (DebugViewIndex == 1 && !ultimaMascara.Empty())
                {
                    using Mat debugResized = new Mat();
                    Cv2.Resize(ultimaMascara, debugResized, frame.Size(), 0, 0, InterpolationFlags.Linear);
                    Cv2.CvtColor(debugResized, frameToEncode, ColorConversionCodes.GRAY2BGR);
                    Cv2.PutText(frameToEncode, "Visualizacao: Neural Mask", new Point(10, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 2);
                }
                else 
                {
                    frame.CopyTo(frameToEncode);
                    Cv2.PutText(frameToEncode, "Visualizacao: 1. Real", new Point(10, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 2);
                }

                int rightX = frameToEncode.Width - 110;
                Cv2.PutText(frameToEncode, $"FPS: {CurrentFPS}", new Point(rightX, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 2);
                Cv2.PutText(frameToEncode, $"RAM: {CurrentRAM}MB", new Point(rightX, textY + 20), HersheyFonts.HersheySimplex, 0.5, textColor, 2);

                var encodeParams = new ImageEncodingParam[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 70) };
                Cv2.ImEncode(".jpg", frameToEncode, out byte[] buffer, encodeParams);
                FrameBuffer = buffer;

                Task.Delay(16, token).Wait();
            }

            if (!ultimaMascara.Empty()) ultimaMascara.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO CRITICO EM LOOP DE VISAO] {ex.Message}");
            }
        }

        public void Dispose() => Parar();
    }
}
