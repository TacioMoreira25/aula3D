using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Linq;

namespace Aula3D.VisionCore.Processamento
{
    public class SegmentadorNeural : IDisposable
    {
        private InferenceSession _sessao;
        private int _tamanhoRede = 224;

        public SegmentadorNeural(string caminhoOnnx)
        {
            _sessao = new InferenceSession(caminhoOnnx);
        }

        public Mat ObterMascara(Mat frameRoi)
        {
            // 1. Redimensionar e converter cor
            using Mat frameRedimensionado = new Mat();
            Cv2.Resize(frameRoi, frameRedimensionado, new Size(_tamanhoRede, _tamanhoRede));
            using Mat frameRgb = new Mat();
            Cv2.CvtColor(frameRedimensionado, frameRgb, ColorConversionCodes.BGR2RGB);

            // 2. Preencher o Tensor de Entrada
            var tensorEntrada = new DenseTensor<float>(new[] { 1, 3, _tamanhoRede, _tamanhoRede });
            
            for (int y = 0; y < _tamanhoRede; y++)
            {
                for (int x = 0; x < _tamanhoRede; x++)
                {
                    Vec3b pixel = frameRgb.At<Vec3b>(y, x);
                    tensorEntrada[0, 0, y, x] = pixel.Item0 / 255.0f;
                    tensorEntrada[0, 1, y, x] = pixel.Item1 / 255.0f;
                    tensorEntrada[0, 2, y, x] = pixel.Item2 / 255.0f;
                }
            }

            // 3. Executar a Inferência
            var inputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("input", tensorEntrada) };
            using var resultados = _sessao.Run(inputs);
            
            // 4. Ler o Tensor de Saída de forma segura (sem usar .Span)
            var tensorSaida = resultados.First().AsTensor<float>();

            Mat mascara = new Mat(_tamanhoRede, _tamanhoRede, MatType.CV_8UC1);
            var indexer = mascara.GetGenericIndexer<byte>();

            for (int y = 0; y < _tamanhoRede; y++)
            {
                for (int x = 0; x < _tamanhoRede; x++)
                {
                    float probabilidade = tensorSaida[0, 0, y, x];
                    // Binarização: > 0.5 vira branco (mão), senão preto (fundo)
                    indexer[y, x] = probabilidade > 0.5f ? (byte)255 : (byte)0;
                }
            }

            // 5. Redimensionar a máscara de volta para o tamanho original do ROI
            Cv2.Resize(mascara, mascara, frameRoi.Size(), 0, 0, InterpolationFlags.Nearest);

            return mascara;
        }

        public void Dispose()
        {
            _sessao?.Dispose();
        }
    }
}