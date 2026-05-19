using Aula3D.Desktop.Core.Models;

namespace Aula3D.Desktop.Core.Interfaces;

public interface IUdpReceiverService
{
    event Action<TrackingData>? OnDataReceived;
    void StartListening(int port = 5005);
    void StopListening();
}
