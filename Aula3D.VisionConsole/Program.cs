using Aula3D.VisionCore;
using System;
using System.IO;
using System.Diagnostics;

// 1. Criação dos arquivos de telemetria
File.WriteAllText("dados_fps_hibrida.csv", "Tempo;FPS\n");
File.WriteAllText("dados_estabilidade.csv", "Tempo;X_Bruto;X_Suavizado\n");

var facade = new GestorDeVisaoFacade();
facade.Iniciar();

Console.WriteLine("Cérebro PDI Híbrido iniciado. Aguardando MediaPipe via UDP...\nPressione ESC para sair e gerar os gráficos.\n");

var cronometro = Stopwatch.StartNew();
float xSuavizado = 0f; // Variável de estado para o filtro matemático

while (true)
{
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        break;

    if (facade.LatestHands.Count > 0)
    {
        var h = facade.LatestHands[0];
        
        // --- EXTRAÇÃO PARA O GRÁFICO 3 (ESTABILIDADE LERP) ---
        float xBruto = h.CenterX;
        
        // Aplicação do Filtro Passa-Baixa (Lerp com fator de 0.15f)
        if (xSuavizado == 0f) xSuavizado = xBruto; // Previne salto no primeiro frame
        xSuavizado = xSuavizado + (xBruto - xSuavizado) * 0.15f;

        // Grava no CSV o ruído da rede vs a correção do C#
        File.AppendAllText("dados_estabilidade.csv", $"{DateTime.Now:HH:mm:ss.fff};{xBruto:F2};{xSuavizado:F2}\n");

        Console.Write($"\rMãos: {facade.LatestHands.Count} | X_Rede: {xBruto:F1} | X_Filtro: {xSuavizado:F1} | Aberta? {h.IsOpen} | FPS UDP: {facade.CurrentFPS}    ");
    }
    else
    {
        Console.Write($"\rNenhuma mão detectada. FPS UDP: {facade.CurrentFPS}                                ");
    }

    // --- EXTRAÇÃO PARA O GRÁFICO 2 (DESEMPENHO/FPS) ---
    // Grava a taxa de pacotes processados pelo Facade a cada segundo
    if (cronometro.ElapsedMilliseconds >= 1000)
    {
        File.AppendAllText("dados_fps_hibrida.csv", $"{DateTime.Now:HH:mm:ss};{facade.CurrentFPS}\n");
        cronometro.Restart();
    }

    System.Threading.Thread.Sleep(50); // Alivia a thread do Console
}

facade.Parar();
Console.WriteLine("\n\nEncerrando serviços... Teste concluído!");
Console.WriteLine("Os arquivos 'dados_fps_hibrida.csv' e 'dados_estabilidade.csv' estão na pasta de execução.");