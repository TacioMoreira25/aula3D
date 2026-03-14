#nullable enable
using OpenCvSharp;
using Aula3D.VisionCore.Interfaces;
using Aula3D.VisionCore.Processamento;

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
        // -- IGestureProvider --
        public float X             { get; private set; }
        public float Y             { get; private set; }
        public bool  GestoDetectado { get; private set; }   // true = ABERTA, false = FECHADA
        public bool  HandDetected   { get; private set; }

        // -- estado adicional exposto para debug no VisionConsole --
        public bool  IsRunning  { get; private set; }
        public bool  IsHandOpen => GestoDetectado;
        public float Z          { get; private set; }       // estimativa de profundidade por área

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

                    if (resultado.CenterOfMass.X > 0)
                    {
                        X = resultado.CenterOfMass.X;
                        Y = resultado.CenterOfMass.Y;
                        double area = resultado.BoundingRect.Width * resultado.BoundingRect.Height;
                        Z = (float)(Math.Sqrt(area) / 100.0);
                    }
                }
                else
                {
                    HandDetected = false;
                }

                Cv2.ImShow("Debug OpenCV - Câmera Invisível", frame);
                Cv2.WaitKey(1);
                Thread.Sleep(30);
            }
        }

        public void Dispose() => Parar();
    }
}
