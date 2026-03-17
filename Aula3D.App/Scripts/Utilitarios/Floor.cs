using Godot;

/// <summary>
/// Chão decorativo para a cena 3D.
/// </summary>
public partial class Floor : CsgBox3D
{
	public override void _Ready()
	{
		var material = new StandardMaterial3D();
		var texture  = GD.Load<Texture2D>("res://Assets/Texturas/smooth-plaster-wall.jpg");

		if (texture != null)
		{
			material.AlbedoTexture = texture;
			material.Uv1Scale      = new Vector3(5, 5, 5);
		}
		else
		{
			GD.PrintErr("Falha ao carregar a textura do chão. Verifique o caminho res://Assets/Texturas/smooth-plaster-wall.jpg");
		}

		this.Material = material;
	}

	public override void _Process(double delta) { }
}
