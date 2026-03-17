using Godot;
// using OpenCvSharp; // Descomentar quando implementar

/// <summary>
/// Converte a matriz de memória do OpenCV (Mat)
/// para um formato de textura que o Godot possa desenhar na UI.
/// </summary>
public static class ConversorDeImagem
{
	// -------------------------------------------------------------------
	// TODO - Dupla 2: Implementar conversão de Imagem
	// -------------------------------------------------------------------
	// Passos necessários para a integração UI (Painel Acadêmico):
	// 1. Receber um objeto 'Mat' do OpenCvSharp.
	// 2. Extrair o array de bytes (mat.ToBytes() ou Marshal.Copy(mat.DataPointer...)).
	// 3. Criar uma Godot.Image usando Image.CreateFromData(...).
	//    Atenção ao formato: OpenCV usa BGR, Godot usa RGB ou RGBA.
	//    Talvez seja necessário Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB).
	// 4. Retornar ImageTexture.CreateFromImage(godotImage).
	// -------------------------------------------------------------------

	/// <summary>
	/// [TODO - Dupla 2] Converte um Mat do OpenCV para ImageTexture do Godot.
	/// Retorna null enquanto não implementado.
	/// </summary>
	public static ImageTexture MatParaTextura(object matOpenCv)
	{
		// var mat = matOpenCv as Mat;
		// ... (lógica de conversão BGR -> RGB e extração de bytes)
		// var image = Image.CreateFromData(mat.Width, mat.Height, false, Image.Format.Rgb8, bytes);
		// return ImageTexture.CreateFromImage(image);

		GD.PrintErr("ConversorDeImagem: Método não implementado (Tarefa Dupla 2).");
		return null;
	}
}
