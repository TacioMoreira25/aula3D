using Godot;
using System;
using System.Threading.Tasks;

public partial class ModelManager : Node3D
{
	private Shader _clippingShader;
	private Node3D _currentModel;

	[Signal]
	public delegate void ModelLoadedEventHandler();

	public override void _Ready()
	{
		// Pré-carrega o nosso shader espacial da pasta
		_clippingShader = GD.Load<Shader>("res://src/Shaders/ClippingShader.gdshader");
	}

	/// <summary>
	/// Método público para carregar o modelo dado um caminho (local ou URI).
	/// </summary>
	public async Task LoadModelAsync(string path)
	{
		_currentModel?.QueueFree();
		_currentModel = null;

		GltfDocument gltf = new();
		GltfState state = new();

		// O append from file do GLTF bloqueia a thread parcialmente, idealmente rodar via Task
		// porem algumas operacoes de node da Godot devem acontecer na MainThread
		Error err = gltf.AppendFromFile(path, state, 0, "res://"); // res:// só base para local, pra remoto HTTP é outro fluxo

		if (err == Error.Ok)
		{
			_currentModel = (Node3D)gltf.GenerateScene(state);

			// Injeção da lógica de Shader Materials no lugar do Standard
			InjectClippingShaders(_currentModel);

			AddChild(_currentModel);

			// Emite sinal quando tudo tiver carregado e instanciado
			EmitSignal(SignalName.ModelLoaded);
		}
		else
		{
			GD.PrintErr($"Falha ao carregar o modelo GLTF. Erro: {err}");
		}
	}

	/// <summary>
	/// Percorre a arvore nativa do modelo para substituir os StandardMaterials por ShaderMaterials com PBR properties
	/// </summary>
	private void InjectClippingShaders(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			for (int i = 0; i < meshInstance.Mesh.GetSurfaceCount(); i++)
			{
				Material originalMat = meshInstance.GetActiveMaterial(i);
				if (originalMat is StandardMaterial3D stdMat)
				{
					ShaderMaterial newMat = new ShaderMaterial
					{
						Shader = _clippingShader
					};

					// Portar propriedades base
					newMat.SetShaderParameter("albedo_color", stdMat.AlbedoColor);

					if (stdMat.AlbedoTexture != null)
						newMat.SetShaderParameter("texture_albedo", stdMat.AlbedoTexture);

					newMat.SetShaderParameter("roughness", stdMat.Roughness);
					newMat.SetShaderParameter("metallic", stdMat.Metallic);
					newMat.SetShaderParameter("emission_color", stdMat.Emission);

					meshInstance.SetSurfaceOverrideMaterial(i, newMat);
				}
			}
		}

		// Recursividade pros filhos
		foreach (Node child in node.GetChildren())
		{
			InjectClippingShaders(child);
		}
	}

	/// <summary>
	/// Aplica rotação ao modelo recebendo os valores de X, Y e Z em graus.
	/// Função simples e exposta para fácil integração.
	/// </summary>
	public void SetModelRotation(float x, float y, float z)
	{
		if (_currentModel != null)
		{
			_currentModel.RotationDegrees = new Vector3(x, y, z);
		}
	}

	/// <summary>
	/// Aplica translação (posição) ao modelo recebendo os valores de X, Y e Z.
	/// Função simples e exposta para fácil integração.
	/// </summary>
	public void SetModelPosition(float x, float y, float z)
	{
		if (_currentModel != null)
		{
			_currentModel.Position = new Vector3(x, y, z);
		}
	}

	/// <summary>
	/// Aplica escala ao modelo recebendo os valores de X, Y e Z.
	/// Função simples e exposta para fácil integração.
	/// </summary>
	public void SetModelScale(float x, float y, float z)
	{
		if (_currentModel != null)
		{
			_currentModel.Scale = new Vector3(x, y, z);
		}
	}

	internal bool ModelIsLoaded()
	{
		return _currentModel != null;
	}
}
