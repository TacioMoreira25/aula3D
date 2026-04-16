using System;
using OpenCvSharp;
using Aula3D.VisionCore;

namespace Aula3D.VisionConsole
{
    /// <summary>
    /// Ambiente de testes isolado.
    /// Consome a mesma GestorDeVisaoFacade utilizada pelo Godot para garantir
    /// que o comportamento testado no Console será idêntico ao do App 3D.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Iniciando rastreamento de visão avançado (Neural + Kalman)...");
            Console.WriteLine("Pressione 'ESC' na janela do vídeo para encerrar.");
            Console.WriteLine("Pressione '1' (Câmera Original) ou '2' (Máscara Neural) para trocar as visualizações.");
            Console.WriteLine("Pressione 'A' para Salvar Assinatura Mão Aberta ou 'F' para Mão Fechada.");

            // Instancia e inicia o Facade exatamente como o Objeto3D no Godot faz
            using var facade = new GestorDeVisaoFacade();
            facade.Iniciar();

            while (true)
            {
                // Verifica se já temos um frame processado disponivel
                if (facade.FrameBuffer != null && facade.FrameBuffer.Length > 0)
                {
                    // Decodifica o JPG em tempo real simulando a leitura do Godot
                    using Mat frame = Cv2.ImDecode(facade.FrameBuffer, ImreadModes.Color);
                    if (!frame.Empty())
                    {
                        Cv2.ImShow("Controle 3D - Visao Computacional", frame);
                    }
                }

                // Loga as informações tratadas no console
                if (facade.HandDetected)
                {
                    string gesto = facade.IsHandOpen ? "ABERTA " : "FECHADA";
                    Console.Write($"\r[{gesto}] X: {facade.X,-5:F1} Y: {facade.Y,-5:F1} Z: {facade.Z,-5:F2} | FPS: {facade.CurrentFPS} | RAM: {facade.CurrentRAM}MB      ");
                }
                else
                {
                    Console.Write($"\r[SEM MAO] FPS: {facade.CurrentFPS} | RAM: {facade.CurrentRAM}MB                                              ");
                }

                // Inputs do teclado para testes
                int key = Cv2.WaitKey(30);
                if (key == 27) break; // ESC
                
                if (key == '1') facade.DebugViewIndex = 0;
                if (key == '2') facade.DebugViewIndex = 1;
                
                // Opções para treinar classificador de forma simplificada no console
                if (key == 'a' || key == 'A')
                {
                    facade.SalvarAssinaturaAberta();
                    Console.WriteLine("\n[INFO] Assinatura de Mão ABERTA salva!");
                }
                if (key == 'f' || key == 'F')
                {
                    facade.SalvarAssinaturaFechada();
                    Console.WriteLine("\n[INFO] Assinatura de Mão FECHADA salva!");
                }
            }

            Console.WriteLine("\nEncerrando provedor de visão...");
            facade.Parar();
            Cv2.DestroyAllWindows();
        }
    }
}
