namespace Aula3D.VisionCore.Interfaces
{
    public interface IGestureProvider
    {
        /// <summary>Coordenada X normalizada do centro de massa da mão (pixels da câmera).</summary>
        float X { get; }

        /// <summary>Coordenada Y normalizada do centro de massa da mão (pixels da câmera).</summary>
        float Y { get; }

        /// <summary>
        /// Gesto detectado no momento.
        /// true  = mão ABERTA 
        /// false = mão FECHADA 
        /// </summary>
        bool GestoDetectado { get; }

        /// <summary>Indica se a câmera/mouse está com rastreamento ativo.</summary>
        bool HandDetected { get; }
    }
}
