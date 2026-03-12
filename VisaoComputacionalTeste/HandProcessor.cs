using OpenCvSharp;

namespace VisaoComputacionalTeste
{
    // 2. PROCESSADOR DE IMAGEM
    // Isola toda a lógica de HSV, contornos, área e matemática de ângulos e defeitos
    // Implementa IDisposable para limpar corretamente recursos na memória (Mats)
    public class HandProcessor : IDisposable
    {
        private readonly Mat _blurred;
        private readonly Mat _hsv;
        private readonly Mat _mask;
        private readonly Mat _kernel;

        private readonly Scalar _lowerBound;
        private readonly Scalar _upperBound;

        public HandProcessor()
        {
            // Pré-aloca matrizes para otimizar desempenho de memória
            _blurred = new Mat();
            _hsv = new Mat();
            _mask = new Mat();
            _kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));

            _lowerBound = new Scalar(0, 30, 60);
            _upperBound = new Scalar(25, 180, 255);
        }

        public List<HandTrackingResult> Process(Mat frame, Rect roi)
        {
            var results = new List<HandTrackingResult>();
            
            // CORTA a imagem! Cria ponteiro para a memória do 'frame' na região ROI
            using Mat frameRoi = new Mat(frame, roi);

            // Aplica filtros, espaço de cores e limiarização
            Cv2.GaussianBlur(frameRoi, _blurred, new Size(7, 7), 0);
            Cv2.CvtColor(_blurred, _hsv, ColorConversionCodes.BGR2HSV);
            Cv2.InRange(_hsv, _lowerBound, _upperBound, _mask);

            // Aplica morfologia matemática
            Cv2.MorphologyEx(_mask, _mask, MorphTypes.Open, _kernel);
            Cv2.MorphologyEx(_mask, _mask, MorphTypes.Close, _kernel);
            Cv2.Dilate(_mask, _mask, _kernel, iterations: 2);

            // Encontra contornos na máscara
            Cv2.FindContours(_mask, out Point[][] contours, out var hierarchy, 
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours != null && contours.Length > 0)
            {
                // Lógica C# (LINQ): Filtra e ordena áreas com segurança. Pega até as 2 maiores (duas mãos)
                var biggestContours = contours
                    .Where(c => Cv2.ContourArea(c) > 3000)
                    .OrderByDescending(c => Cv2.ContourArea(c))
                    .Take(2);

                foreach (var contour in biggestContours)
                {
                    var result = new HandTrackingResult { HandDetected = true };
                    result.Contour = contour;
                    result.BoundingRect = Cv2.BoundingRect(contour);

                    int[] hullIndices = Cv2.ConvexHullIndices(contour);
                    var defectPoints = new List<Point>();
                    int defectCount = 0;

                    if (hullIndices.Length > 3 && contour.Length > 3)
                    {
                        Vec4i[] defects = Cv2.ConvexityDefects(contour, hullIndices);

                        foreach (var defect in defects)
                        {
                            double depth = defect.Item3 / 256.0;
                            double minDepth = Math.Max(result.BoundingRect.Height * 0.15, 20);

                            if (depth > minDepth)
                            {
                                Point start = contour[defect.Item0];
                                Point end = contour[defect.Item1];
                                Point farthestPoint = contour[defect.Item2];

                                // Teorema dos cossenos para garantir forma de  'V'
                                double a = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                                double b = Math.Sqrt(Math.Pow(farthestPoint.X - start.X, 2) + Math.Pow(farthestPoint.Y - start.Y, 2));
                                double c = Math.Sqrt(Math.Pow(end.X - farthestPoint.X, 2) + Math.Pow(end.Y - farthestPoint.Y, 2));
                                
                                if (b > 0 && c > 0)
                                {
                                    double angle = Math.Acos((b * b + c * c - a * a) / (2 * b * c));

                                    if (angle <= Math.PI / 2.0) // <= 90 graus
                                    {
                                        defectCount++;
                                        defectPoints.Add(farthestPoint);
                                    }
                                }
                            }
                        }
                    }

                    // Combinação de Defeitos e Geometria da Bounding Box
                    // Se não houver vales entre os dedos (dedos colados), uma mão plana 
                    // costuma ser bem mais "comprida" que larga. Um punho fechado tende a ser quadrado/redondo.
                    double aspectRatio = Math.Max(result.BoundingRect.Height, result.BoundingRect.Width) / 
                                        (double)Math.Min(result.BoundingRect.Height, result.BoundingRect.Width);

                    result.DefectPoints = defectPoints.ToArray();
                    // Considera ABERTA se encontrou os vales OU se a proporção da caixa mostra ser retangular o suficiente
                    result.IsHandOpen = defectCount >= 3 || (defectCount < 3 && aspectRatio > 1.35);
                    result.State = result.IsHandOpen ? "ABERTA" : "FECHADA";

                    // Matemática do Centro de massa M00
                    Moments moments = Cv2.Moments(contour);
                    if (moments.M00 > 0)
                    {
                        result.CenterOfMass = new Point(
                            (int)(moments.M10 / moments.M00),
                            (int)(moments.M01 / moments.M00)
                        );
                    }

                    results.Add(result);
                }
            }

            return results;
        }

        // Retorna a máscara para debugar as cores
        public Mat GetMask() => _mask;

        public void Dispose()
        {
            _blurred?.Dispose();
            _hsv?.Dispose();
            _mask?.Dispose();
            _kernel?.Dispose();
        }
    }
}