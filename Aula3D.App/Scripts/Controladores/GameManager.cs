using Godot;

/// <summary>
/// Orquestra a cena principal: conecta os sinais da UI (PainelAcademico)
/// às transformações do modelo 3D (Objeto3D) e abre o FileDialog.
/// </summary>
public partial class GameManager : Node
{
	private PainelAcademico _painel;
	private Objeto3D        _objeto3D;
	private FileDialog      _fileDialog;

	public override void _Ready()
	{
		_painel   = GetNodeOrNull<PainelAcademico>("UIManager");
		_objeto3D = GetNodeOrNull<Objeto3D>("WorldLayer/ModelManager");

		if (_painel != null)
		{
			_painel.OnLoadLocalRequested   += HandleLoadLocalRequest;
			_painel.OnAxisValuesChanged    += HandleAxisValuesChanged;
			_painel.OnPositionValuesChanged += HandlePositionValuesChanged;
			_painel.OnScaleValuesChanged   += HandleScaleValuesChanged;
		}

		_fileDialog = new FileDialog
		{
			FileMode       = FileDialog.FileModeEnum.OpenFile,
			Access         = FileDialog.AccessEnum.Filesystem,
			Title          = "Carregar Modelo 3D",
			Filters        = new string[] { "*.glb, *.gltf ; Modelos GLTF" },
			UseNativeDialog = false
		};
		_fileDialog.FileSelected += OnFileSelected;
		AddChild(_fileDialog);
	}

	private void HandleLoadLocalRequest()                       => _fileDialog.PopupCenteredRatio(0.5f);
	private void HandleAxisValuesChanged(float x, float y, float z)    => _objeto3D?.SetModelRotation(x, y, z);
	private void HandlePositionValuesChanged(float x, float y, float z) => _objeto3D?.SetModelPosition(x, y, z);
	private void HandleScaleValuesChanged(float x, float y, float z)   => _objeto3D?.SetModelScale(x, y, z);

	private async void OnFileSelected(string path)
	{
		GD.Print($"Tentando carregar modelo local: {path}");
		if (_objeto3D != null) await _objeto3D.LoadModelAsync(path);
		else GD.PrintErr("Objeto3D nulo — não é possível carregar a malha.");
	}
}
