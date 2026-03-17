using Godot;
using Aula3D.VisionCore.Interfaces;

/// <summary>
/// Adaptador de testes para a Dupla 2.
/// Finge ser o componente de visão usando a posição e cliques do mouse.
/// Substitua GestorDeVisaoFacade por esta classe no Objeto3D._Ready()
/// para testar a rotação por Quatérnios sem precisar da webcam.
/// </summary>
public class MouseMock : IGestureProvider
{
	public float X { get; private set; }
	public float Y { get; private set; }
	public bool GestoDetectado { get; private set; } // Click esquerdo = Fechada (false), Solto = Aberta (true)
	public bool HandDetected { get; private set; }   // Sempre true se o mouse estiver na tela

	public MouseMock()
	{
		// Simulamos um "loop de visão" atrelando ao _Process real do Godot
		// Mas como esta não é uma classe Node nativa, o uso recomendado é:
		// O Objeto3D chama _AtualizarDados(Input...) no seu _Process.
	}

	/// <summary>
	/// Chama a cada frame dentro do Godot para atualizar o mock.
	/// </summary>
	public void AtualizarMock()
	{
		var viewport = Engine.GetMainLoop() as SceneTree;
		if (viewport?.Root != null)
		{
			Vector2 mousePos = viewport.Root.GetMousePosition();
			X = mousePos.X;
			Y = mousePos.Y;
			
			// Simula "Mão Aberta" = True (rotação livre)
			// Simula "Mão Fechada" = False (quando botão esquerdo está pressionado)
			GestoDetectado = !Input.IsMouseButtonPressed(MouseButton.Left);
			
			HandDetected = true;
		}
	}
}
