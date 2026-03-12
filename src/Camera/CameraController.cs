using Godot;
using System;

public partial class CameraController : Node3D
{
	public enum CameraMode
	{
		Orbital,
		Free
	}

	[Export] public float RotationSpeed = 0.005f;
	[Export] public float ZoomSpeed = 0.5f;
	[Export] public float PanSpeed = 0.01f;
	[Export] public float MinZoom = 1.0f;
	[Export] public float MaxZoom = 150.0f;
	[Export] public float FreeMoveSpeed = 10.0f;

	public CameraMode CurrentMode = CameraMode.Orbital;

	private Node3D _innerGimbal;
	private Camera3D _camera;
	private bool _isDragging = false;
	private bool _isPanning = false;
	private Vector2 _lastMousePosition;
	private float _currentZoom = 10.0f;

	public override void _Ready()
	{
		// Configuramos a hierarquia no instanciamento principal, mas tentamos pegar os filhos para garantir.
		_innerGimbal = GetNodeOrNull<Node3D>("InnerGimbal");
		_camera = _innerGimbal?.GetNodeOrNull<Camera3D>("MainCamera");

		// Se eles não existirem (ex: script adicionado sem a arvore pronta), vamos criar em tempo de execução
		if (_innerGimbal == null)
		{
			_innerGimbal = new Node3D { Name = "InnerGimbal" };
			AddChild(_innerGimbal);

			_camera = new Camera3D { Name = "MainCamera" };
			// Movemos a câmera um pouco para tras no eixo Z localmente (posicao inicial)
			_camera.Position = new Vector3(0, 0, 10);
			_innerGimbal.AddChild(_camera);
		}

		if (_camera != null)
		{
			_currentZoom = _camera.Position.Z;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Alt)
			{
				ToggleCameraMode();
			}
		}

		// 1. Zoom (Scroll do Mouse)
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
			{
				ApplyZoom(-ZoomSpeed);
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
			{
				ApplyZoom(ZoomSpeed);
			}

			// Inicio de Arrastar/Pan (Clique do meio ou botao direito)
			if (mouseButton.ButtonIndex == MouseButton.Middle || mouseButton.ButtonIndex == MouseButton.Right)
			{
				if (mouseButton.Pressed)
				{
					_isDragging = true;
					_lastMousePosition = mouseButton.Position;
					// Se estiver segurando shift, ativamos o panning (Translacao em X/Y da tela) inves da Orbit
					_isPanning = Input.IsKeyPressed(Key.Shift);
				}
				else
				{
					_isDragging = false;
					_isPanning = false;
				}
			}
		}

		// 2. Rotação/Orbit e Pan (Arrastar o Mouse)
		if (@event is InputEventMouseMotion mouseMotion && _isDragging)
		{
			Vector2 delta = mouseMotion.Position - _lastMousePosition;
			_lastMousePosition = mouseMotion.Position;

			if (_isPanning)
			{
				ApplyPan(delta);
			}
			else
			{
				ApplyRotation(delta);
			}
		}
	}

	private void ApplyZoom(float amount)
	{
		if (CurrentMode == CameraMode.Orbital)
		{
			// Movemos a camera ao longo de seu proprio eixo Z local para dar zoom in/out
			Vector3 newPos = _camera.Position;
			newPos.Z = Mathf.Clamp(newPos.Z + amount, MinZoom, MaxZoom);
			_camera.Position = newPos;
			_currentZoom = newPos.Z;
		}
		else if (CurrentMode == CameraMode.Free)
		{
			GlobalPosition += _camera.GlobalTransform.Basis.Z * amount * 2.0f;
		}
	}

	private void ApplyRotation(Vector2 delta)
	{
		// Rotaciona o Outer Gimbal no eixo Y (Esquerda-Direita)
		RotateY(-delta.X * RotationSpeed);

		// Rotaciona o Inner Gimbal no eixo X (Cima-Baixo)
		_innerGimbal.RotateX(-delta.Y * RotationSpeed);

		// Limita a rotação no eixo X para evitar virar de cabeça pra baixo ("Gimbal Lock")
		Vector3 innerRotation = _innerGimbal.Rotation;
		innerRotation.X = Mathf.Clamp(innerRotation.X, -Mathf.Pi / 2.1f, Mathf.Pi / 2.1f);
		_innerGimbal.Rotation = innerRotation;
	}

	private void ApplyPan(Vector2 delta)
	{
		// Move o Outer Gimbal inteiramente pra cima/baixo, esquerda/direita baseado na visao da camera
		Vector3 right = GlobalTransform.Basis.X;
		Vector3 up = GlobalTransform.Basis.Y;

		GlobalPosition -= (right * delta.X * PanSpeed) + (up * -delta.Y * PanSpeed); // Y da tela inverte
	}

	private void ToggleCameraMode()
	{
		if (CurrentMode == CameraMode.Orbital)
		{
			CurrentMode = CameraMode.Free;
			Vector3 camGlobalPos = _camera.GlobalPosition;
			GlobalPosition = camGlobalPos;
			_camera.Position = Vector3.Zero;
		}
		else
		{
			CurrentMode = CameraMode.Orbital;
			Vector3 camGlobalPos = _camera.GlobalPosition;
			Vector3 backward = _camera.GlobalTransform.Basis.Z;
			GlobalPosition = camGlobalPos - backward * _currentZoom;
			_camera.Position = new Vector3(0, 0, _currentZoom);
		}
	}

	public override void _Process(double delta)
	{
		if (CurrentMode == CameraMode.Free)
		{
			Vector3 velocity = Vector3.Zero;

			if (Input.IsKeyPressed(Key.W)) velocity -= _camera.GlobalTransform.Basis.Z;
			if (Input.IsKeyPressed(Key.S)) velocity += _camera.GlobalTransform.Basis.Z;
			if (Input.IsKeyPressed(Key.A)) velocity -= _camera.GlobalTransform.Basis.X;
			if (Input.IsKeyPressed(Key.D)) velocity += _camera.GlobalTransform.Basis.X;
			if (Input.IsKeyPressed(Key.E)) velocity += _camera.GlobalTransform.Basis.Y;
			if (Input.IsKeyPressed(Key.Q)) velocity -= _camera.GlobalTransform.Basis.Y;

			if (velocity.LengthSquared() > 0)
			{
				velocity = velocity.Normalized();
				GlobalPosition += velocity * FreeMoveSpeed * (float)delta;
			}
		}
	}
}
