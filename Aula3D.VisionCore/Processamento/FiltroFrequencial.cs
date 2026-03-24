using OpenCvSharp;

namespace Aula3D.VisionCore.Processamento
{
    public class FiltroFrequencial
    {
        public Mat AplicarPassaBaixa(Mat input)
        {
            using Mat gray = new Mat();
            if (input.Channels() == 3)
            {
                Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                input.CopyTo(gray);
            }

            // Converter para float para a DFT
            using Mat padded = new Mat();
            int m = Cv2.GetOptimalDFTSize(gray.Rows);
            int n = Cv2.GetOptimalDFTSize(gray.Cols);

            Cv2.CopyMakeBorder(gray, padded, 0, m - gray.Rows, 0, n - gray.Cols, BorderTypes.Constant, Scalar.All(0));

            using Mat paddedFloat = new Mat();
            padded.ConvertTo(paddedFloat, MatType.CV_32F);

            // Preparar as partições para a parte complexa (Real e Imaginário)
            Mat[] planes = { paddedFloat, Mat.Zeros(padded.Size(), MatType.CV_32F) };
            using Mat complexI = new Mat();
            Cv2.Merge(planes, complexI);

            // Calcular a DFT
            Cv2.Dft(complexI, complexI);

            // Centralizar o espectro
            DeslocarQuadrantes(complexI);

            // Criar e aplicar o filtro passa-baixa (máscara circular)
            using Mat filterMask = Mat.Zeros(complexI.Size(), MatType.CV_32FC2);
            Point center = new Point(complexI.Cols / 2, complexI.Rows / 2);
            int radius = 50; // Ajustar conforme a necessidade para reter detalhes
            Cv2.Circle(filterMask, center, radius, new Scalar(1, 1), -1);

            using Mat complexIFiltered = new Mat();
            Cv2.MulSpectrums(complexI, filterMask, complexIFiltered, DftFlags.None);

            // Desfazer a centralização
            DeslocarQuadrantes(complexIFiltered);

            // Calcular a DFT inversa
            using Mat inverseTransform = new Mat();
            Cv2.Idft(complexIFiltered, inverseTransform, DftFlags.Scale | DftFlags.RealOutput);

            Mat saida = new Mat();
            inverseTransform.ConvertTo(saida, MatType.CV_8U);

            // Cortar a parte do padding
            return new Mat(saida, new Rect(0, 0, gray.Cols, gray.Rows));
        }

        private void DeslocarQuadrantes(Mat magI)
        {
            magI = magI[new Rect(0, 0, magI.Cols & -2, magI.Rows & -2)];

            int cx = magI.Cols / 2;
            int cy = magI.Rows / 2;

            Mat q0 = new Mat(magI, new Rect(0, 0, cx, cy));   // Top-Left
            Mat q1 = new Mat(magI, new Rect(cx, 0, cx, cy));  // Top-Right
            Mat q2 = new Mat(magI, new Rect(0, cy, cx, cy));  // Bottom-Left
            Mat q3 = new Mat(magI, new Rect(cx, cy, cx, cy)); // Bottom-Right

            Mat tmp = new Mat();

            q0.CopyTo(tmp);
            q3.CopyTo(q0);
            tmp.CopyTo(q3);

            q1.CopyTo(tmp);
            q2.CopyTo(q1);
            tmp.CopyTo(q2);
        }
    }
}