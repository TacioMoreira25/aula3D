using Aula3D.VisionCore;

namespace Aula3D.Desktop.Core.Services;

/// <summary>
/// Processa dados brutos de rastreamento e os converte em transformações 3D aplicáveis.
/// Aplica filtragem IIR Passa-Baixas (Lerp) para estabilização de PDS.
/// </summary>
public class InteractionProcessor
{
    // Configurações Técnicas
    private const float PAN_SENSITIVITY = 15.0f;
    private const float ROT_SENSITIVITY = 4.0f;
    private const float PAN_LIMIT = 3.0f;
    private const float SAFE_BOUNDARY = 0.85f;
    
    // Fator do Filtro Passa-Baixas (Lerp) - Conforme o Artigo (alpha = 0.15)
    private const float LERP_ALPHA = 0.15f; 

    // Estado Atual
    public float PanX { get; private set; }
    public float PanY { get; private set; }
    public float Theta { get; private set; }
    public float Phi { get; private set; } = (float)Math.PI / 2.0f;
    public float Scale { get; private set; } = 1.0f;

    // Cache de Rastreamento Bruto e Suavizado
    private float? _lastX, _lastY, _lastDist;
    private float? _smoothedX, _smoothedY, _smoothedDist; // Variáveis de estado do Filtro
    private string _lastState = "";

    public (bool updated, string mode) Process(List<HandData> hands)
    {
        if (hands.Count == 0)
        {
            ResetTracking();
            return (false, "Nenhum");
        }

        if (hands.Count == 2) return ProcessBimanual(hands[0], hands[1]);
        return ProcessUnimanual(hands[0]);
    }

    private (bool, string) ProcessUnimanual(HandData hand)
    {
        // 1. Sinal Bruto (Com ruído/jitter da câmera)
        float rawNormX = (hand.CenterX - 320f) / 320f;
        float rawNormY = (hand.CenterY - 240f) / 240f;

        // 2. APLICAÇÃO DO FILTRO IIR PASSA-BAIXAS (LERP)
        if (!_smoothedX.HasValue || !_smoothedY.HasValue)
        {
            _smoothedX = rawNormX;
            _smoothedY = rawNormY;
        }
        else
        {
            // y[n] = y[n-1] + (x[n] - y[n-1]) * alpha
            _smoothedX = _smoothedX.Value + (rawNormX - _smoothedX.Value) * LERP_ALPHA;
            _smoothedY = _smoothedY.Value + (rawNormY - _smoothedY.Value) * LERP_ALPHA;
        }

        // Usamos as coordenadas suavizadas pelo filtro a partir daqui
        float normX = _smoothedX.Value;
        float normY = _smoothedY.Value;

        // Bloqueio de saída: Congela o modelo se a mão estiver na borda
        if (Math.Abs(normX) > SAFE_BOUNDARY || Math.Abs(normY) > SAFE_BOUNDARY)
        {
            ResetTracking();
            return (false, "Zona de Segurança");
        }

        string state = hand.IsOpen ? "Open" : "Closed";
        if (_lastState != state) ResetTracking(); // Reinicia os deltas ao trocar de gesto

        bool updated = false;
        if (_lastX.HasValue && _lastY.HasValue)
        {
            // Agora o delta é calculado com base num sinal liso, sem tremores
            float dx = normX - _lastX.Value;
            float dy = normY - _lastY.Value;

            if (hand.IsOpen)
            {
                // Rotação
                Theta -= dx * ROT_SENSITIVITY;
                Phi = Math.Clamp(Phi - dy * ROT_SENSITIVITY, 0.1f, (float)Math.PI - 0.1f);
                updated = true;
            }
            else
            {
                // Pan
                PanX = Math.Clamp(PanX - dx * PAN_SENSITIVITY, -PAN_LIMIT, PAN_LIMIT);
                PanY = Math.Clamp(PanY + dy * PAN_SENSITIVITY, -PAN_LIMIT, PAN_LIMIT);
                updated = true;
            }
        }

        // Atualiza a memória da iteração anterior
        _lastX = normX; 
        _lastY = normY; 
        _lastState = state;
        
        return (updated, hand.IsOpen ? "Rotação" : "Pan");
    }

    private (bool, string) ProcessBimanual(HandData h1, HandData h2)
    {
        if (!h1.IsOpen || !h2.IsOpen) return (false, "Aguardando Gesto");

        // Sinal Bruto (Distância)
        float rawDist = (float)Math.Sqrt(Math.Pow(h1.CenterX - h2.CenterX, 2) + Math.Pow(h1.CenterY - h2.CenterY, 2));
        
        // APLICAÇÃO DO FILTRO IIR NO ZOOM
        if (!_smoothedDist.HasValue) _smoothedDist = rawDist;
        else _smoothedDist = _smoothedDist.Value + (rawDist - _smoothedDist.Value) * LERP_ALPHA;

        float dist = _smoothedDist.Value;
        bool updated = false;

        if (_lastDist.HasValue)
        {
            float delta = (dist - _lastDist.Value) / 200f;
            Scale = Math.Clamp(Scale + delta, 0.1f, 5.0f);
            updated = true;
        }

        _lastDist = dist;
        return (updated, "Zoom");
    }

    private void ResetTracking()
    {
        _lastX = _lastY = _lastDist = null;
        _smoothedX = _smoothedY = _smoothedDist = null;
        _lastState = "";
    }
}