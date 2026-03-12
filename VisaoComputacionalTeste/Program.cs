using OpenCvSharp;

namespace VisaoComputacionalTeste
{
    // Captura da câmera e desenho focado exclusivamente em UI com os dados do DTO
    class Program
    {
        static void TesteMain(string[] args)
        {
            using var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                Console.WriteLine("Erro: Nenhuma webcam detectada.");
                return;
            }

            Console.WriteLine("Pressione 'ESC' na janela do vídeo para encerrar.");
            Console.WriteLine("DICA: Mantenha seu rosto de fora da zona retangular para evitar falsos positivos!");

            using var frame = new Mat();
            using var processor = new HandProcessor();

            while (true)
            {
                capture.Read(frame);
                if (frame.Empty()) break;

                // Espelha a imagem
                Cv2.Flip(frame, frame, FlipMode.Y);

                // Define apenas UMA Grande Zona de Interesse (Workspace) do lado esquerdo
                // Atuará como um "Mousepad 3D": X, Y (Posição) e Z (Área/Zoom) em uma única mão
                int sideWidth = Math.Min(300, (frame.Width / 2));
                int sideHeight = frame.Height - 60;

                Rect leftRoi = new Rect(10, 30, sideWidth, sideHeight);

                // Desenha HUD fixo para visualização humana
                Cv2.Rectangle(frame, leftRoi, new Scalar(255, 255, 0), 2);
                Cv2.PutText(frame, "CONTROLE 3D (Z = ZOOM)", new Point(leftRoi.X, leftRoi.Y - 10), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);

                // Processa a imagem buscando a mão apenas nessa zona de segurança
                List<HandTrackingResult> hands = processor.Process(frame, leftRoi);

                // APLICAÇÃO NA MÃO PRINCIPAL
                if (hands.Count > 0)
                {
                    HandTrackingResult result = hands[0]; // Interage apenas com a principal detecção
                    if (result.HandDetected && result.Contour != null && result.State != null)
                    {
                        using Mat frameRoi = new Mat(frame, leftRoi);
                        var color = new Scalar(0, 255, 0);

                        Cv2.DrawContours(frameRoi, new[] { result.Contour }, 0, color, 2);
                        Point[] hullPoints = Cv2.ConvexHull(result.Contour);
                        Cv2.DrawContours(frameRoi, new[] { hullPoints }, 0, new Scalar(255, 0, 0), 2);

                        if (result.DefectPoints != null)
                        {
                            foreach (var point in result.DefectPoints) 
                                Cv2.Circle(frameRoi, point, 6, new Scalar(255, 0, 255), -1);
                        }

                        Cv2.PutText(frameRoi, result.State, new Point(result.BoundingRect.X, Math.Max(20, result.BoundingRect.Y - 10)), 
                                    HersheyFonts.HersheySimplex, 1.0, result.IsHandOpen ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

                        if (result.CenterOfMass.X > 0 || result.CenterOfMass.Y > 0)
                        {
                            Cv2.Circle(frameRoi, result.CenterOfMass, 8, color, -1);
                            
                            // Lógica de Profundidade (Eixo Z / Zoom) para Uma Mão
                            // Baseado no Teorema de Tales e Proporção Visível (Area da BoundingBox)
                            // Quando você aproxima a mão da webcam, a área em pixels aumenta exponencialmente.
                            double area = result.BoundingRect.Width * result.BoundingRect.Height;
                            
                            // A Raiz Quadrada ajuda a criar uma escala de Zoom linear suave 
                            // (Dividido por 100 para converter milhares de pixels em fatores ex: 1.20x)
                            double zZoomFactor = Math.Sqrt(area) / 100.0;

                            // Escreve o multiplicador do eixo Z acompanhando o centro da mão
                            Cv2.PutText(frameRoi, $"Z: {zZoomFactor:F2}x", new Point(result.CenterOfMass.X - 40, result.CenterOfMass.Y + 30), 
                                        HersheyFonts.HersheyComplex, 0.7, new Scalar(0, 255, 255), 2);
                        }
                    }
                }

                // Apenas a interface de produção, sem telas de debug HSV
                Cv2.ImShow("Controle 3D - Visao Computacional", frame);

                if (Cv2.WaitKey(30) == 27) break;
            }

            Cv2.DestroyAllWindows();
        }
    }
}