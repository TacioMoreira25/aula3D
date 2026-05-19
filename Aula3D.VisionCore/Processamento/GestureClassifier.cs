using System.Collections.Generic;
using System.Linq;

namespace Aula3D.VisionCore.Processamento
{
    public static class GestureClassifier
    {
        /// <summary>
        /// Classifica o gesto da mão com base nos marcos (Landmarks) do MediaPipe.
        /// Assume que Y cresce para baixo.
        /// </summary>
        public static void Classify(this HandData hand)
        {
            if (hand == null || hand.Landmarks == null || hand.Landmarks.Count < 21)
                return;

            var landmarks = hand.Landmarks;

            // Pontas dos dedos: Indicador (8), Médio (12), Anelar (16), Mínimo (20)
            // Juntas PIP: Indicador (6), Médio (10), Anelar (14), Mínimo (18)

            bool isIndexExtended = landmarks[8].Y < landmarks[6].Y;
            bool isMiddleExtended = landmarks[12].Y < landmarks[10].Y;
            bool isRingExtended = landmarks[16].Y < landmarks[14].Y;
            bool isPinkyExtended = landmarks[20].Y < landmarks[18].Y;

            // IsOpen: Todos os quatro dedos estendidos (Y da ponta < Y da articulação PIP)
            // Lembre-se: no MediaPipe/Imagens, Y cresce para baixo, então < significa "acima" no espaço da imagem.
            hand.IsOpen = isIndexExtended && isMiddleExtended && isRingExtended && isPinkyExtended;

            // IsPointing: Apenas o indicador estendido, demais dobrados
            hand.IsPointing = isIndexExtended && !isMiddleExtended && !isRingExtended && !isPinkyExtended;
        }
    }
}
