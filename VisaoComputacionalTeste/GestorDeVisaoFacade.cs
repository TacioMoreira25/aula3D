using OpenCvSharp;

namespace VisaoComputacionalTeste
{
    public class GestorDeVisaoFacade : IDisposable
    {
        // Variáveis de leitura pública para o Godot
        public bool IsRunning { get; private set; }
        public bool HandDetected { get; private set; }
        public bool IsHandOpen { get; private set; } = true;
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        private CancellationTokenSource _cts;
        private Task _visionTask;

        public void Iniciar()
        {
            if (IsRunning) return;
            
            _cts = new CancellationTokenSource();
            IsRunning = true;

            // Inicia o processamento pesado em uma thread paralela
            _visionTask = Task.Run(() => LoopDeVisao(_cts.Token), _cts.Token);
        }

        public void Parar()
        {
            if (!IsRunning) return;
            
            _cts.Cancel();
            _visionTask.Wait(); // Aguarda a thread fechar com segurança
            IsRunning = false;
        }

        private void LoopDeVisao(CancellationToken token)
        {
            using var capture = new VideoCapture(0);
            using var frame = new Mat();
            using var processor = new HandProcessor();

            if (!capture.IsOpened()) return;

            // Fica lendo a câmera até o Godot pedir para fechar
            while (!token.IsCancellationRequested)
            {
                capture.Read(frame);
                if (frame.Empty()) continue;

                Cv2.Flip(frame, frame, FlipMode.Y);
                
                // Zona de interesse restrita (Workspace)
                int sideWidth = Math.Min(300, (frame.Width / 2));
                int sideHeight = frame.Height - 60;
                Rect leftRoi = new Rect(10, 30, sideWidth, sideHeight);

                var hands = processor.Process(frame, leftRoi);

                if (hands.Count > 0)
                {
                    var result = hands[0];
                    HandDetected = result.HandDetected;
                    
                    if (HandDetected && result.CenterOfMass.X > 0)
                    {
                        // Atualiza as variáveis que o Godot vai ler
                        X = result.CenterOfMass.X;
                        Y = result.CenterOfMass.Y;
                        IsHandOpen = result.IsHandOpen;
                        
                        double area = result.BoundingRect.Width * result.BoundingRect.Height;
                        Z = (float)(Math.Sqrt(area) / 100.0);
                    }
                }
                else
                {
                    HandDetected = false;
                }

                Cv2.ImShow("Debug OpenCV - Câmera Invisível", frame);
                Cv2.WaitKey(1); // O OpenCV EXIGE o WaitKey(1) para atualizar os pixels da janela

                // Pausa de 30ms para não consumir 100% da CPU do Pop!_OS em background
                Thread.Sleep(30); 
            }
        }

        public void Dispose()
        {
            Parar();
        }
    }
}