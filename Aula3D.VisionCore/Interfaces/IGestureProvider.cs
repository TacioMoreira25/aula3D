using System.Collections.Generic;

namespace Aula3D.VisionCore.Interfaces
{
    public interface IGestureProvider
    {
        List<HandData> LatestHands { get; }
        bool IsRunning { get; }
    }
}
