using Microsoft.JSInterop;

namespace Aula3D.Desktop.Core.Services;

/// <summary>
/// Serviço responsável pela comunicação com as funções JavaScript do model-viewer.
/// </summary>
public class ModelViewerService
{
    private readonly IJSRuntime _js;

    public ModelViewerService(IJSRuntime js) => _js = js;

    public async Task LoadModelAsync(string url) 
        => await _js.InvokeVoidAsync("loadModel", url);

    public async Task UpdateRotationAsync(float theta, float phi) 
        => await _js.InvokeVoidAsync("updateModelRotation", theta, phi, 0);

    public async Task UpdatePanAsync(float x, float y) 
        => await _js.InvokeVoidAsync("updateModelPan", x, y, 0);

    public async Task UpdateScaleAsync(float scale) 
        => await _js.InvokeVoidAsync("updateModelScale", scale);
}