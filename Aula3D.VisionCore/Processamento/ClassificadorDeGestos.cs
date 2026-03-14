#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenCvSharp;

namespace Aula3D.VisionCore.Processamento
{
    /// <summary>
    /// Etapa 3 do pipeline PDI: decide se a mão está ABERTA ou FECHADA
    /// a partir dos defeitos de convexidade, e expõe um ponto de extensão
    /// para comparação com assinaturas pré-gravadas (Momentos de Hu).
    /// </summary>
    public static class ClassificadorDeGestos
    {
        /// <summary>
        /// Classifica o gesto e preenche <see cref="HandTrackingResult.IsHandOpen"/>,
        /// <see cref="HandTrackingResult.State"/> e <see cref="HandTrackingResult.DefectPoints"/>
        /// a partir dos defeitos de convexidade do <paramref name="contour"/>.
        /// </summary>
        public static void Classificar(Point[] contour, HandTrackingResult resultado)
        {
            int[] hullIndices = Cv2.ConvexHullIndices(contour);
            var defectPoints = new List<Point>();
            int defectCount  = 0;

            if (hullIndices.Length > 3 && contour.Length > 3)
            {
                Vec4i[] defects = Cv2.ConvexityDefects(contour, hullIndices);

                foreach (var defect in defects)
                {
                    double depth    = defect.Item3 / 256.0;
                    double minDepth = Math.Max(resultado.BoundingRect.Height * 0.15, 20);

                    if (depth > minDepth)
                    {
                        Point start        = contour[defect.Item0];
                        Point end          = contour[defect.Item1];
                        Point farthestPoint = contour[defect.Item2];

                        // Lei dos cossenos para medir o ângulo no defeito
                        double a = Dist(end, start);
                        double b = Dist(farthestPoint, start);
                        double c = Dist(end, farthestPoint);

                        if (b > 0 && c > 0)
                        {
                            double angle = Math.Acos((b * b + c * c - a * a) / (2 * b * c));
                            if (angle <= Math.PI / 2.0)
                            {
                                defectCount++;
                                defectPoints.Add(farthestPoint);
                            }
                        }
                    }
                }
            }

            double aspectRatio =
                Math.Max(resultado.BoundingRect.Height, resultado.BoundingRect.Width) /
                (double)Math.Min(resultado.BoundingRect.Height, resultado.BoundingRect.Width);

            resultado.DefectPoints = defectPoints.ToArray();
            resultado.IsHandOpen   = defectCount >= 3 || (defectCount < 3 && aspectRatio > 1.35);
            resultado.State        = resultado.IsHandOpen ? "ABERTA" : "FECHADA";
        }

        /// <summary>
        /// Compara <paramref name="momentosHu"/> com assinaturas
        /// pré-gravadas e retorna o nome do gesto reconhecido, ou null.
        /// </summary>
        public static string? ReconhecerPorAssinatura(double[] momentosHu)
        {
            string path = "gestures.json";
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                var gravadas = JsonSerializer.Deserialize<Dictionary<string, double[]>>(json);

                if (gravadas == null || gravadas.Count == 0)
                    return null;

                string? bestGesture = null;
                double minDistance = double.MaxValue;
                double limiar = 12.0; // Aumentamos o threshold bastante porque o momento de Hu oscila

                foreach (var (gesture, signature) in gravadas)
                {
                    double dist = 0;
                    for (int i = 0; i < 6; i++) // Ignoramos o 7º momento (muda de sinal constante e sofre reflexivos)
                    {
                        dist += Math.Pow(momentosHu[i] - signature[i], 2);
                    }
                    dist = Math.Sqrt(dist);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestGesture = gesture;
                    }
                }

                if (minDistance < limiar)
                    return bestGesture;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler assinaturas: {ex.Message}");
            }

            return null;
        }

        // -- helpers --

        public static void SalvarAssinatura(string gesto, double[] momentosHu)
        {
            string path = "gestures.json";
            Dictionary<string, double[]> gravadas = new();

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, double[]>>(json);
                    if (dict != null) gravadas = dict;
                }
                catch { }
            }

            gravadas[gesto] = momentosHu;
            File.WriteAllText(path, JsonSerializer.Serialize(gravadas, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static double Dist(Point a, Point b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }
}
