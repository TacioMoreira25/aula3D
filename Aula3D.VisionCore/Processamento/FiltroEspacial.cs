using OpenCvSharp;

namespace Aula3D.VisionCore.Processamento
{
    /// <summary>
    /// Etapa 1 do pipeline PDI: aplica desfoque Gaussiano, converte para HSV
    /// e isola os pixels de cor da pele por limiarização de faixa.
    /// </summary>
    public class FiltroEspacial : IDisposable
    {
        private readonly Mat _blurred;
        private readonly Mat _hsv;
        private readonly Mat _mask;
        private readonly Mat _kernel;

        // Faixa HSV calibrada para pele sob iluminação de laboratório.
        // Dupla 1: ajuste estes valores se o ambiente tiver luz diferente.
        private readonly Scalar _lowerBound;
        private readonly Scalar _upperBound;

        public FiltroEspacial()
        {
            _blurred = new Mat();
            _hsv     = new Mat();
            _mask    = new Mat();
            _kernel  = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));

            // H: 0-25 (laranja-amarelo da pele), S: 30-180, V: 60-255
            _lowerBound = new Scalar(0, 30, 60);
            _upperBound = new Scalar(25, 180, 255);
        }

        /// <summary>
        /// Aplica o filtro em <paramref name="frameRoi"/> e devolve a máscara binária.
        /// Pixels brancos na máscara = região de pele detectada.
        /// </summary>
        public Mat Aplicar(Mat frameRoi)
        {
            // 1. Desfoque Gaussiano para reduzir ruído de alta frequência
            Cv2.GaussianBlur(frameRoi, _blurred, new Size(7, 7), 0);

            // 2. Conversão BGR → HSV para segmentação por cor independente de brilho
            Cv2.CvtColor(_blurred, _hsv, ColorConversionCodes.BGR2HSV);

            // 3. Limiarização dentro da faixa de cor da pele
            Cv2.InRange(_hsv, _lowerBound, _upperBound, _mask);

            // 4. Abertura morfológica: remove ruídos pequenos
            Cv2.MorphologyEx(_mask, _mask, MorphTypes.Open,  _kernel);

            // 5. Fechamento morfológico: preenche buracos na mão
            Cv2.MorphologyEx(_mask, _mask, MorphTypes.Close, _kernel);

            // 6. Dilatação: expande levemente a região para englobar bordas da pele
            Cv2.Dilate(_mask, _mask, _kernel, iterations: 2);

            // 7. Cortar o braço preservando apenas a mão (Transformada de Distância)
            using Mat dist = new Mat();
            Cv2.DistanceTransform(_mask, dist, DistanceTypes.L2, DistanceTransformMasks.Mask5);
            Cv2.MinMaxLoc(dist, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal > 15) // Verifica se existe volume suficiente para ser uma mão
            {
                // maxLoc é o centro do "círculo mais gordo" (palma)
                // Vamos calcular a altura (eixo Y) aproximada onde fica o pulso
                int limiteY = (int)Math.Min(_mask.Height, maxLoc.Y + maxVal * 1.0);

                // Criamos uma tela retangular revelando tudo entre o teto e a linha do pulso
                using Mat limiteMask = Mat.Zeros(_mask.Size(), MatType.CV_8UC1);
                Cv2.Rectangle(limiteMask, new Point(0, 0), new Point(_mask.Width, limiteY), Scalar.White, -1);

                // Remove da máscara principal os pixels indesejados abaixo da linha (braço)
                Cv2.BitwiseAnd(_mask, limiteMask, _mask);
            }

            return _mask;
        }

        /// <summary>Retorna os contornos externos com área mínima de <paramref name="minArea"/>.</summary>
        public Point[][] ExtrairContornos(double minArea = 3000)
        {
            Cv2.FindContours(_mask, out Point[][] contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours == null || contours.Length == 0)
                return Array.Empty<Point[]>();

            return contours
                .Where(c => Cv2.ContourArea(c) > minArea)
                .OrderByDescending(c => Cv2.ContourArea(c))
                .Take(2)
                .ToArray();
        }

        /// <summary>Retorna a última máscara calculada (útil para janelas de debug).</summary>
        public Mat GetMask() => _mask;

        public void Dispose()
        {
            _blurred.Dispose();
            _hsv.Dispose();
            _mask.Dispose();
            _kernel.Dispose();
        }
    }
}
