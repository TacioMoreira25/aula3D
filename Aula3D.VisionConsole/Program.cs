using System;
using OpenCvSharp;
using Aula3D.VisionCore;
using Aula3D.VisionCore.Processamento;

namespace Aula3D.VisionConsole
{
    /// <summary>
    /// Ambiente de testes isolado.
    /// Liga a webcam e abre janelas nativas no Linux para depurar o pipeline PDI
    /// sem precisar abrir o Godot Editor.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            using var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                Console.WriteLine("Erro: Nenhuma webcam detectada.");
                return;
            }

            Console.WriteLine("Pressione 'ESC' na janela do vídeo para encerrar.");

            using var frame   = new Mat();
            using var filtro  = new FiltroEspacial();

            while (true)
            {
                capture.Read(frame);
                if (frame.Empty()) break;

                Cv2.Flip(frame, frame, FlipMode.Y);

                int sideWidth  = Math.Min(300, frame.Width  / 2);
                int sideHeight = frame.Height - 60;
                Rect leftRoi   = new Rect(10, 30, sideWidth, sideHeight);

                // Desenha a região de interesse no frame principal
                Cv2.Rectangle(frame, leftRoi, new Scalar(255, 255, 0), 2);
                Cv2.PutText(frame, "CONTROLE 3D (Z = ZOOM)", new Point(leftRoi.X, leftRoi.Y - 10),
                    HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);

                // --- Pipeline PDI ---
                using Mat frameRoi  = new Mat(frame, leftRoi);
                filtro.Aplicar(frameRoi);
                Point[][] contornos = filtro.ExtrairContornos();

                if (contornos.Length > 0)
                {
                    Point[] contorno = contornos[0];
                    var resultado    = new HandTrackingResult { HandDetected = true, Contour = contorno };

                    ExtratorHu.ExtrairGeometria(contorno, resultado);
                    ClassificadorDeGestos.Classificar(contorno, resultado);

                    double[] hu = ExtratorHu.CalcularMomentosHu(contorno);
                    string? bestGesture = ClassificadorDeGestos.ReconhecerPorAssinatura(hu);
                    if (bestGesture != null)
                    {
                        resultado.State = bestGesture;
                        resultado.IsHandOpen = bestGesture == "ABERTA";
                    }

                    // Desenho de debug no ROI
                    if (resultado.Contour != null)
                    {
                        Cv2.DrawContours(frameRoi, new[] { resultado.Contour }, 0,
                            new Scalar(0, 255, 0), 2);

                        Point[] hullPoints = Cv2.ConvexHull(resultado.Contour);
                        Cv2.DrawContours(frameRoi, new[] { hullPoints }, 0,
                            new Scalar(255, 0, 0), 2);
                    }

                    if (resultado.DefectPoints != null)
                        foreach (var pt in resultado.DefectPoints)
                            Cv2.Circle(frameRoi, pt, 6, new Scalar(255, 0, 255), -1);

                    if (resultado.State != null)
                        Cv2.PutText(frameRoi, resultado.State,
                            new Point(resultado.BoundingRect.X,
                                      Math.Max(20, resultado.BoundingRect.Y - 10)),
                            HersheyFonts.HersheySimplex, 1.0,
                            resultado.IsHandOpen ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

                    if (resultado.CenterOfMass.X > 0 || resultado.CenterOfMass.Y > 0)
                    {
                        Cv2.Circle(frameRoi, resultado.CenterOfMass, 8, new Scalar(0, 255, 0), -1);

                        double area         = resultado.BoundingRect.Width * resultado.BoundingRect.Height;
                        double zZoomFactor  = Math.Sqrt(area) / 100.0;

                        Cv2.PutText(frameRoi, $"Z: {zZoomFactor:F2}x",
                            new Point(resultado.CenterOfMass.X - 40, resultado.CenterOfMass.Y + 30),
                            HersheyFonts.HersheyComplex, 0.7, new Scalar(0, 255, 255), 2);
                    }

                    // Exibe Momentos de Hu no console (útil para calibrar o classificador)
                    Console.Write($"\r[{resultado.State,-7}] Hu: [{string.Join(", ", Array.ConvertAll(hu, h => $"{h:F3}"))}]   ");
                }
                else
                {
                    Console.Write("\r[SEM MÃO]                                                          ");
                }

                // Janela da máscara binária — útil para calibrar HSV
                Cv2.ImShow("Máscara HSV - Dupla 1 Debug", filtro.GetMask());
                Cv2.ImShow("Controle 3D - Visão Computacional", frame);

                int key = Cv2.WaitKey(30);
                if (key == 27) break;
                if (contornos.Length > 0)
                {
                    double[] currentHu = ExtratorHu.CalcularMomentosHu(contornos[0]);
                    if (key == 'a' || key == 'A')
                    {
                        ClassificadorDeGestos.SalvarAssinatura("ABERTA", currentHu);
                        Console.WriteLine("\n[TREINO] Assinatura salva para: ABERTA");
                    }
                    else if (key == 'f' || key == 'F')
                    {
                        ClassificadorDeGestos.SalvarAssinatura("FECHADA", currentHu);
                        Console.WriteLine("\n[TREINO] Assinatura salva para: FECHADA");
                    }
                }
            }

            Console.WriteLine();
            Cv2.DestroyAllWindows();
        }
    }
}
