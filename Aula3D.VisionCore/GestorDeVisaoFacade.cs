using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Aula3D.VisionCore.Processamento;
using Aula3D.VisionCore.Utils;
using Aula3D.VisionCore.Interfaces;

namespace Aula3D.VisionCore
{
    public class GestorDeVisaoFacade : IGestureProvider, IDisposable
    {
        public List<HandData> LatestHands { get; private set; } = new();
        public bool IsRunning { get; private set; }
        public int CurrentFPS { get; private set; }

        public event Action<List<HandData>>? OnHandsDetected;

        private Task? _visionTask;
        private CancellationTokenSource? _cts;
        private Process? _pythonProcess;
        private UdpClient? _udpClient;
        private const int PortaUDP = 5005;

        public void Iniciar()
        {
            if (IsRunning) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = "uv",
                Arguments = "run main.py",
                WorkingDirectory = PathResolver.ObterCaminhoTrackerService(),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                _pythonProcess = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar o microserviço Python: {ex.Message}");
            }

            _udpClient = new UdpClient(PortaUDP);
            _cts = new CancellationTokenSource();
            IsRunning = true;
            
            _visionTask = Task.Run(() => LoopDeVisaoUDP(_cts.Token), _cts.Token);
        }

        public void Parar()
        {
            if (!IsRunning) return;

            _cts?.Cancel();
            _udpClient?.Close();

            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                _pythonProcess.Kill();
                _pythonProcess.Dispose();
            }

            _visionTask?.Wait();
            IsRunning = false;
        }

        private void LoopDeVisaoUDP(CancellationToken token)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PortaUDP);
            var stopwatch = Stopwatch.StartNew();
            int frameCount = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient!.Available > 0)
                    {
                        byte[] bytesRecebidos = _udpClient.Receive(ref endPoint);
                        string json = System.Text.Encoding.UTF8.GetString(bytesRecebidos);

                        var hands = JsonSerializer.Deserialize<List<HandData>>(json);

                        if (hands != null)
                        {
                            foreach (var hand in hands)
                            {
                                hand.Classify(); // Executa a classificação baseada em Landmarks
                            }
                            
                            LatestHands = hands;
                            frameCount++;

                            // Dispara o evento reativo e atualiza estado
                            OnHandsDetected?.Invoke(hands);

                            if (stopwatch.ElapsedMilliseconds >= 1000)
                            {
                                CurrentFPS = frameCount;
                                frameCount = 0;
                                stopwatch.Restart();
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1); // Evita consumo excessivo de CPU
                    }
                }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested) break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro no processamento UDP: {ex.Message}");
                }
            }
        }

        public void Dispose() => Parar();
    }
}