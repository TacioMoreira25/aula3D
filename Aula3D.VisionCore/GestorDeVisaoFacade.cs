#nullable enable
using OpenCvSharp;
using System.Runtime.InteropServices;
using Aula3D.VisionCore.Interfaces;
using Aula3D.VisionCore.Processamento;

using System.Reflection;

namespace Aula3D.VisionCore
{
    /// <summary>
    /// Única classe pública acessada pelo Godot (Dupla 2).
    /// Esconde toda dependência do OpenCV e orquestra o pipeline PDI:
    /// FiltroEspacial → ExtratorHu → ClassificadorDeGestos.
    /// 
    /// Implementa <see cref="IGestureProvider"/> — o contrato acordado entre as duplas.
    /// </summary>
    public class GestorDeVisaoFacade : IGestureProvider, IDisposable
    {
        static GestorDeVisaoFacade()
        {
            try
            {
                // Intercepta e ensina o Godot exatamente ONDE está a biblioteca C++ no Linux
                NativeLibrary.SetDllImportResolver(typeof(OpenCvSharp.Mat).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == "OpenCvSharpExtern")
                    {
                        string cwd = System.IO.Directory.GetCurrentDirectory();
                        string customPath = System.IO.Path.Combine(cwd, "libOpenCvSharpExtern.so");
                        
                        if (System.IO.File.Exists(customPath))
                        {
                            return NativeLibrary.Load(customPath);
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
        public byte[] FrameBuffer { get; private set; }

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

            while (!token.IsCancellationRequested)
            {
                capture.Read(frame);
                if (frame.Empty()) continue;

                Cv2.Flip(frame, frame, FlipMode.Y);

                int sideWidth  = Math.Min(300, frame.Width  / 2);
                int sideHeight = frame.Height - 60;
                Rect roi       = new Rect(10, 30, sideWidth, sideHeight);

                using Mat frameRoi = new Mat(frame, roi);
                filtro.Aplicar(frameRoi);
                Point[][] contornos = filtro.ExtrairContornos();

                if (contornos.Length > 0)
                {
                    Point[] contorno = contornos[0];
                    var resultado    = new HandTrackingResult { HandDetected = true, Contour = contorno };

                    ExtratorHu.ExtrairGeometria(contorno, resultado);
                    ClassificadorDeGestos.Classificar(contorno, resultado);

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

                // Desenha a região de interesse para o professor saber onde colocar a mão
                Cv2.Rectangle(frame, roi, new Scalar(255, 255, 0), 2);
                Cv2.PutText(frame, "AREA DE CONTROLE 3D", new Point(roi.X, roi.Y - 10),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);

                // Codifica o frame JÁ DESENHADO em formato JPG leve
                var encodeParams = new ImageEncodingParam[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 70) };
                Cv2.ImEncode(".jpg", frame, out byte[] buffer, encodeParams);
                FrameBuffer = buffer;

                // Trocamos o Sleep estressante por um Task.Delay moderno (Melhoria de desempenho no Godot)
                Task.Delay(16, token).Wait();
            }
        }

        public void Dispose() => Parar();
    }
}
