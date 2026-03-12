using Godot;
using System;

public partial class GameManager : Node
{
	private UIManager _uiManager;
	private ModelManager _modelManager;
	private FileDialog _fileDialog;

	public override void _Ready()
	{
		_uiManager = GetNodeOrNull<UIManager>("UIManager");
		_modelManager = GetNodeOrNull<ModelManager>("WorldLayer/ModelManager");

		if (_uiManager != null)
		{
			_uiManager.OnLoadLocalRequested += HandleLoadLocalRequest;
			_uiManager.OnAxisValuesChanged += HandleAxisValuesChanged;
			_uiManager.OnPositionValuesChanged += HandlePositionValuesChanged;
			_uiManager.OnScaleValuesChanged += HandleScaleValuesChanged;
		}

		// Setup FileDialog em runtime para que funcione se nao estiver configurado na cena
		_fileDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			Title = "Load 3D Model",
			Filters = new string[] { "*.glb, *.gltf ; GLTF Models" },
			UseNativeDialog = false // Mantém o dialog embutido do Godot, mais seguro no Linux de não falhar
		};

		_fileDialog.FileSelected += OnFileSelected;
		AddChild(_fileDialog);
	}

	private void HandleLoadLocalRequest()
	{
		_fileDialog.PopupCenteredRatio(0.5f);
	}

	private void HandleAxisValuesChanged(float x, float y, float z)
	{
		// Exemplo: Usando a função SetModelRotation que foi criada no ModelManager.
		// Dependendo do range do seu Slider na interface (ex: 0 a 100 ou 0 a 360),
		// você pode multiplicar aqui se necessário. Estou passando diretamente.
		_modelManager?.SetModelRotation(x, y, z);
	}

	private void HandlePositionValuesChanged(float x, float y, float z)
	{
		_modelManager?.SetModelPosition(x, y, z);
	}

	private void HandleScaleValuesChanged(float x, float y, float z)
	{
		_modelManager?.SetModelScale(x, y, z);
	}

	private async void OnFileSelected(string path)
	{
		GD.Print($"Tentando carregar modelo local: {path}");

		if (_modelManager != null)
		{
			await _modelManager.LoadModelAsync(path);
		}
		else
		{
			GD.PrintErr("ModelManager nulo, não é possivel carregar a malha.");
		}
	}
}
