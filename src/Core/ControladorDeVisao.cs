using Godot;
using VisaoComputacionalTeste;

public partial class ControladorDeVisao : Node
{
	private GestorDeVisaoFacade _visao;
	private ModelManager _modelManager;

	private float _larguraCameraOpenCV = 300.0f;
	private float _alturaCameraOpenCV = 300.0f;
	private float _fatorDeSensibilidade = 15.0f;

	public override void _Ready()
	{
		// Encontra o gerenciador de modelos 3D que você já programou
		_modelManager = GetNodeOrNull<ModelManager>("../WorldLayer/ModelManager");

		// Instancia a classe do OpenCV e liga a webcam em background
		_visao = new GestorDeVisaoFacade();
		_visao.Iniciar();
		
		GD.Print("OpenCV iniciado em background via integração nativa.");
	}

	public override void _Process(double delta)
	{
		// Só move algo se o modelo foi carregado e a mão está visível
		if (_modelManager == null || !_modelManager.ModelIsLoaded()) return;

		if (_visao.HandDetected)
		{
			// Normaliza os pixels para o mundo 3D
			float mapX = (_visao.X - (_larguraCameraOpenCV / 2)) / _fatorDeSensibilidade; 
			float mapY = -(_visao.Y - (_alturaCameraOpenCV / 2)) / _fatorDeSensibilidade; 

			// Máquina de estados
			if (!_visao.IsHandOpen)
			{
				// Mão fechada (Agarrar): Atualiza posição
				_modelManager.SetModelPosition(mapX, mapY, -_visao.Z);
				_modelManager.SetModelScale(1.1f, 1.1f, 1.1f);
			}
			else
			{
				// Mão aberta: Retorna à escala original
				_modelManager.SetModelScale(1.0f, 1.0f, 1.0f);
			}
		}
	}

	// Evento super importante: Quando o Godot for fechado, 
	// manda o OpenCV desligar a câmera corretamente.
	public override void _ExitTree()
	{
		if (_visao != null)
		{
			_visao.Parar();
			_visao.Dispose();
			GD.Print("OpenCV finalizado com segurança.");
		}
	}
}
