using Godot;

/// <summary>
/// Painel de controle da interface gráfica (Dupla 2).
/// Exibe os controles de corte 3D, transformação e botão de carregamento.
/// Responsabilidade futura: exibir os 4 vídeos do pipeline PDI e
/// os números de debug dos Momentos de Hu para a banca.
/// </summary>
public partial class PainelAcademico : CanvasLayer
{
	private HSlider _sliderX, _sliderY, _sliderZ;
	private CheckBox _checkX, _checkY, _checkZ;
	private Button  _btnLoadLocal;
	private HSlider _sliderPosX, _sliderPosY, _sliderPosZ;
	private HSlider _sliderScaleX, _sliderScaleY, _sliderScaleZ;
	private float   _maxClipDistance = 50.0f;

	[Signal] public delegate void OnLoadLocalRequestedEventHandler();
	[Signal] public delegate void OnAxisValuesChangedEventHandler(float x, float y, float z);
	[Signal] public delegate void OnPositionValuesChangedEventHandler(float x, float y, float z);
	[Signal] public delegate void OnScaleValuesChangedEventHandler(float x, float y, float z);

	public override void _Ready()
	{
		_sliderX      = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxX/SliderX");
		_checkX       = GetNodeOrNull<CheckBox>("Panel/VBoxContainer/HBoxX/CheckX");
		_sliderY      = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxY/SliderY");
		_checkY       = GetNodeOrNull<CheckBox>("Panel/VBoxContainer/HBoxY/CheckY");
		_sliderZ      = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxZ/SliderZ");
		_checkZ       = GetNodeOrNull<CheckBox>("Panel/VBoxContainer/HBoxZ/CheckZ");
		_btnLoadLocal = GetNodeOrNull<Button>("Panel/BtnLoadLocal");
		_sliderPosX   = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxPosX/SliderPosX");
		_sliderPosY   = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxPosY/SliderPosY");
		_sliderPosZ   = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxPosZ/SliderPosZ");
		_sliderScaleX = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxScaleX/SliderScaleX");
		_sliderScaleY = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxScaleY/SliderScaleY");
		_sliderScaleZ = GetNodeOrNull<HSlider>("Panel/VBoxContainer/HBoxScaleZ/SliderScaleZ");

		if (_sliderX != null)
		{
			_sliderX.ValueChanged += (val) => UpdateClippingPlane("x", (float)val, _checkX?.ButtonPressed ?? false);
			_sliderX.ValueChanged += (_)   => EmitAxisValues();
		}
		if (_checkX  != null) _checkX.Toggled  += (p) => UpdateClippingPlane("x", (float)(_sliderX?.Value ?? 0), p);
		if (_sliderY != null)
		{
			_sliderY.ValueChanged += (val) => UpdateClippingPlane("y", (float)val, _checkY?.ButtonPressed ?? false);
			_sliderY.ValueChanged += (_)   => EmitAxisValues();
		}
		if (_checkY  != null) _checkY.Toggled  += (p) => UpdateClippingPlane("y", (float)(_sliderY?.Value ?? 0), p);
		if (_sliderZ != null)
		{
			_sliderZ.ValueChanged += (val) => UpdateClippingPlane("z", (float)val, _checkZ?.ButtonPressed ?? false);
			_sliderZ.ValueChanged += (_)   => EmitAxisValues();
		}
		if (_checkZ  != null) _checkZ.Toggled  += (p) => UpdateClippingPlane("z", (float)(_sliderZ?.Value ?? 0), p);

		if (_btnLoadLocal != null) _btnLoadLocal.Pressed += () => EmitSignal(SignalName.OnLoadLocalRequested);
		if (_sliderPosX   != null) _sliderPosX.ValueChanged   += (_) => EmitPositionValues();
		if (_sliderPosY   != null) _sliderPosY.ValueChanged   += (_) => EmitPositionValues();
		if (_sliderPosZ   != null) _sliderPosZ.ValueChanged   += (_) => EmitPositionValues();
		if (_sliderScaleX != null) _sliderScaleX.ValueChanged += (_) => EmitScaleValues();
		if (_sliderScaleY != null) _sliderScaleY.ValueChanged += (_) => EmitScaleValues();
		if (_sliderScaleZ != null) _sliderScaleZ.ValueChanged += (_) => EmitScaleValues();

		DisableAllClipping();
	}

	// -------------------------------------------------------------------
	// Clipping helpers
	// -------------------------------------------------------------------
	private static void DisableAllClipping()
	{
		RenderingServer.GlobalShaderParameterSet("plane_distance_x", 10000.0f);
		RenderingServer.GlobalShaderParameterSet("plane_distance_y", 10000.0f);
		RenderingServer.GlobalShaderParameterSet("plane_distance_z", 10000.0f);
	}

	private void EmitAxisValues()
	{
		float x = (float)(_sliderX?.Value ?? 0f);
		float y = (float)(_sliderY?.Value ?? 0f);
		float z = (float)(_sliderZ?.Value ?? 0f);
		EmitSignal(SignalName.OnAxisValuesChanged, x, y, z);
	}

	private void EmitPositionValues()
	{
		float x = (float)(_sliderPosX?.Value ?? 0f);
		float y = (float)(_sliderPosY?.Value ?? 0f);
		float z = (float)(_sliderPosZ?.Value ?? 0f);
		EmitSignal(SignalName.OnPositionValuesChanged, x, y, z);
	}

	private void EmitScaleValues()
	{
		float x = (float)(_sliderScaleX?.Value ?? 1f);
		float y = (float)(_sliderScaleY?.Value ?? 1f);
		float z = (float)(_sliderScaleZ?.Value ?? 1f);
		EmitSignal(SignalName.OnScaleValuesChanged, x, y, z);
	}

	private void UpdateClippingPlane(string axis, float sliderValue, bool isEnabled)
	{
		string paramName = $"plane_distance_{axis}";
		if (!isEnabled) { RenderingServer.GlobalShaderParameterSet(paramName, 10000.0f); return; }
		float normalized    = sliderValue / 100.0f;
		float actualDistance = Mathf.Lerp(-_maxClipDistance, _maxClipDistance, normalized);
		RenderingServer.GlobalShaderParameterSet(paramName, actualDistance);
	}
}
