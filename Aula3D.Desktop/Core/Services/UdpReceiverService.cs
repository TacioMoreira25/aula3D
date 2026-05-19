using Aula3D.Desktop.Core.Interfaces;
using Aula3D.Desktop.Core.Models;
using Aula3D.VisionCore;

namespace Aula3D.Desktop.Core.Services;

public class UdpReceiverService : IUdpReceiverService, IDisposable
{
    private GestorDeVisaoFacade? _facade;
    public event Action<TrackingData>? OnDataReceived;

    public void StartListening(int port = 5005)
    {
        if (_facade != null) return;

        _facade = new GestorDeVisaoFacade();
        
        _facade.OnHandsDetected += HandleHandsDetected;
        _facade.Iniciar(); 
    }

    private void HandleHandsDetected(List<HandData> hands)
    {
        var data = new TrackingData { Hands = hands };
        OnDataReceived?.Invoke(data);
    }

    public void StopListening()
    {
        if (_facade != null)
        {
            _facade.OnHandsDetected -= HandleHandsDetected;
            _facade.Parar();
            _facade.Dispose();
            _facade = null;
        }
    }

    public void Dispose() => StopListening();
}