using Godot;
using System.Threading.Tasks;
using Aula3D.VisionCore;
using Aula3D.VisionCore.Interfaces;

/// <summary>
/// Controlador principal do objeto 3D.
/// Responsabilidades (Dupla 2):
///   1. Carregar e gerenciar o modelo GLTF em cena.
///   2. Injetar o shader de corte em todas as superfícies do modelo.
///   3. Receber coordenadas do <see cref="IGestureProvider"/> (VisionCore ou MouseMock)
///      e aplicar rotação via Quatérnios.
/// </summary>
public partial class Objeto3D : Node3D
{
	// -------------------------------------------------------------------
	// Campos de model management (ex-ModelManager)
	// -------------------------------------------------------------------
	private Shader _clippingShader;
	private Node3D _currentModel;

	[Signal]
	public delegate void ModelLoadedEventHandler();

	// -------------------------------------------------------------------
	// Campos de controle por visão / mouse (ex-ControladorDeVisao)
	// -------------------------------------------------------------------
	/// <summary>
	/// Provedor de gestos injetado durante _Ready.
	/// Por padrão usa GestorDeVisaoFacade (câmera real).
	/// Para testes da Dupla 2, substituir por MouseMock.
	/// </summary>
	private IGestureProvider _gestureProvider;

	private float _larguraCameraOpenCV = 300.0f;
	private float _alturaCameraOpenCV  = 300.0f;
	private float _fatorDeSensibilidade = 15.0f;

	private TextureRect _cameraPreview;

	// -------------------------------------------------------------------
	// Godot lifecycle
	// -------------------------------------------------------------------
	public override void _Ready()
	{
		// Procura o preview de câmera que você posicionar na UI pelo Godot Editor
		_cameraPreview = GetNodeOrNull<TextureRect>("/root/Main/CameraPreview");
		if (_cameraPreview == null) 
		{
			_cameraPreview = GetNodeOrNull<TextureRect>("/root/Main/UIManager/CameraPreview");
		}

		if (_cameraPreview == null)
		{
			// Varredura profunda caso você tenha colocado dentro de algum painel
			Node found = GetTree().Root.FindChild("CameraPreview", true, false);
			if (found is TextureRect tr) {
				_cameraPreview = tr;
			}
		}

		if (_cameraPreview == null)
		{
			GD.PrintErr("Objeto3D: Aviso! Nó 'CameraPreview' (TextureRect) não foi encontrado na cena. A câmera funcionará no fundo, mas não aparecerá na tela.");
		}

		_clippingShader = GD.Load<Shader>("res://Shaders/ClippingShader.gdshader");

		// Escolha do provedor: troque GestorDeVisaoFacade por MouseMock para testes sem câmera.
		var facade = new GestorDeVisaoFacade();
		facade.Iniciar();
		_gestureProvider = facade;

		GD.Print("Objeto3D: provedor de gestos iniciado.");
	}

	public override void _Process(double delta)
	{
		// 1. Atualiza o preview da câmera se for GestorDeVisaoFacade
		if (_cameraPreview != null && _gestureProvider is GestorDeVisaoFacade visao)
		{
			if (visao.FrameBuffer != null && visao.FrameBuffer.Length > 0)
			{
				try
				{
					Image img = new Image();
					Error erro = img.LoadJpgFromBuffer(visao.FrameBuffer);
					
					if (erro == Error.Ok)
					{
						_cameraPreview.Texture = ImageTexture.CreateFromImage(img);
					}
				}
				catch
				{
				}
			}
		}

		if (_currentModel == null || _gestureProvider == null) return;

		if (_gestureProvider.HandDetected)
		{
			float mapX = (_gestureProvider.X - (_larguraCameraOpenCV / 2)) / _fatorDeSensibilidade;
			float mapY = -(_gestureProvider.Y - (_alturaCameraOpenCV / 2)) / _fatorDeSensibilidade;

			if (!_gestureProvider.GestoDetectado)
			{
				// Mão FECHADA → translação + zoom
				SetModelPosition(mapX, mapY, -((_gestureProvider is GestorDeVisaoFacade f) ? f.Z : 0f));
				SetModelScale(1.1f, 1.1f, 1.1f);
			}
			else
			{
				// Mão ABERTA → rotação livre usando Quatérnios
				AplicarRotacaoQuaternio(mapX, mapY);
				SetModelScale(1.0f, 1.0f, 1.0f);
			}
		}
	}

	public override void _ExitTree()
	{
		if (_gestureProvider is GestorDeVisaoFacade facade)
		{
			facade.Parar();
			facade.Dispose();
			GD.Print("Objeto3D: provedor de gestos finalizado com segurança.");
		}
	}

	// -------------------------------------------------------------------
	// Carregamento de modelo
	// -------------------------------------------------------------------
	public async Task LoadModelAsync(string path)
	{
		_currentModel?.QueueFree();
		_currentModel = null;

		GltfDocument gltf  = new();
		GltfState    state  = new();
		Error        err    = gltf.AppendFromFile(path, state, 0, "res://");

		if (err == Error.Ok)
		{
			_currentModel = (Node3D)gltf.GenerateScene(state);
			InjectClippingShaders(_currentModel);
			AddChild(_currentModel);
			EmitSignal(SignalName.ModelLoaded);
		}
		else GD.PrintErr($"Falha ao carregar o modelo GLTF. Erro: {err}");
	}

	private void InjectClippingShaders(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			for (int i = 0; i < meshInstance.Mesh.GetSurfaceCount(); i++)
			{
				Material originalMat = meshInstance.GetActiveMaterial(i);
				if (originalMat is StandardMaterial3D stdMat)
				{
					ShaderMaterial newMat = new ShaderMaterial { Shader = _clippingShader };
					newMat.SetShaderParameter("albedo_color",   stdMat.AlbedoColor);
					if (stdMat.AlbedoTexture != null)
						newMat.SetShaderParameter("texture_albedo", stdMat.AlbedoTexture);
					newMat.SetShaderParameter("roughness",      stdMat.Roughness);
					newMat.SetShaderParameter("metallic",       stdMat.Metallic);
					newMat.SetShaderParameter("emission_color", stdMat.Emission);
					meshInstance.SetSurfaceOverrideMaterial(i, newMat);
				}
			}
		}
		foreach (Node child in node.GetChildren()) InjectClippingShaders(child);
	}

	// -------------------------------------------------------------------
	// Transformações expostas para GameManager (UI sliders)
	// -------------------------------------------------------------------
	public void SetModelRotation(float x, float y, float z)
	{ if (_currentModel != null) _currentModel.RotationDegrees = new Vector3(x, y, z); }

	public void SetModelPosition(float x, float y, float z)
	{ if (_currentModel != null) _currentModel.Position = new Vector3(x, y, z); }

	public void SetModelScale(float x, float y, float z)
	{ if (_currentModel != null) _currentModel.Scale = new Vector3(x, y, z); }

	/// <summary>Indica se um modelo está carregado em cena.</summary>
	public bool ModelIsLoaded() => _currentModel != null;

	// -------------------------------------------------------------------
	// Rotação por Quatérnios
	// -------------------------------------------------------------------
	/// <summary>
	/// Aplica rotação incremental ao modelo usando Quatérnios, evitando
	/// Gimbal Lock (problema clássico com ângulos de Euler).
	///
	/// mapX → rotação em torno do eixo Y (yaw)
	/// mapY → rotação em torno do eixo X (pitch)
	/// </summary>
	private void AplicarRotacaoQuaternio(float mapX, float mapY)
	{
		if (_currentModel == null) return;

		// Converte os valores de deslocamento em ângulos (radianos)
		float anguloPitch = mapY * 0.01f;   // inclinação vertical
		float anguloYaw   = mapX * 0.01f;   // rotação horizontal

		// Cria Quatérnios para cada eixo
		Quaternion rotPitch = new Quaternion(Vector3.Right,   anguloPitch);
		Quaternion rotYaw   = new Quaternion(Vector3.Up,      anguloYaw);

		// Combina a rotação atual com as novas (ordem: yaw * pitch * atual)
		Quaternion rotacaoAtual = _currentModel.Quaternion;
		_currentModel.Quaternion = (rotYaw * rotPitch * rotacaoAtual).Normalized();
	}
}
