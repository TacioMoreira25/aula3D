import os
import cv2
import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import Dataset, DataLoader
import segmentation_models_pytorch as smp

# ==============================================================================
# 1. CONFIGURAÇÃO DE CAMINHOS
# ==============================================================================
DIRETORIO_DATASET = "training"
PASTA_IMAGENS = os.path.join(DIRETORIO_DATASET, "rgb/")
PASTA_MASCARAS = os.path.join(DIRETORIO_DATASET, "mask/")

# ==============================================================================
# 2. DEFINIÇÃO DO DATASET
# ==============================================================================
class FreiHANDDataset(Dataset):
    def __init__(self, pasta_imagens, pasta_mascaras):
        self.pasta_imagens = pasta_imagens
        self.pasta_mascaras = pasta_mascaras
        self.nomes_ficheiros = []

        # Obtém todos os arquivos JPG da pasta de imagens
        todos_ficheiros = [f for f in os.listdir(pasta_imagens) if f.endswith('.jpg')]
        print(f"Analisando a integridade de {len(todos_ficheiros)} imagens...")

        # Valida a existência da máscara correspondente (JPG ou PNG)
        for ficheiro in todos_ficheiros:
            caminho_mask_jpg = os.path.join(pasta_mascaras, ficheiro)
            caminho_mask_png = os.path.join(pasta_mascaras, ficheiro.replace('.jpg', '.png'))

            if os.path.exists(caminho_mask_jpg) or os.path.exists(caminho_mask_png):
                self.nomes_ficheiros.append(ficheiro)

        print(f"Validação concluída. Dataset pronto com {len(self.nomes_ficheiros)} pares de imagem e máscara.")

    def __len__(self):
        return len(self.nomes_ficheiros)

    def __getitem__(self, idx):
        nome_ficheiro = self.nomes_ficheiros[idx]

        caminho_img = os.path.join(self.pasta_imagens, nome_ficheiro)
        caminho_mask = os.path.join(self.pasta_mascaras, nome_ficheiro)

        # Carregamento e conversão da imagem RGB
        imagem = cv2.imread(caminho_img)
        if imagem is None:
            raise FileNotFoundError(f"Falha ao carregar a imagem em: {caminho_img}")
        
        imagem = cv2.cvtColor(imagem, cv2.COLOR_BGR2RGB)

        # Carregamento da máscara (suporte a arquivos .jpg e .png)
        mascara = cv2.imread(caminho_mask, cv2.IMREAD_GRAYSCALE)
        if mascara is None:
            caminho_mask_png = os.path.join(self.pasta_mascaras, nome_ficheiro.replace('.jpg', '.png'))
            mascara = cv2.imread(caminho_mask_png, cv2.IMREAD_GRAYSCALE)

        if mascara is None:
            raise FileNotFoundError(f"Máscara não encontrada para a imagem '{nome_ficheiro}'.")

        # Normalização e ajuste de dimensões da imagem (CHW)
        imagem = imagem.astype(np.float32) / 255.0
        imagem = np.transpose(imagem, (2, 0, 1))

        # Binarização da máscara e ajuste de dimensões (BCHW)
        mascara = (mascara > 127).astype(np.float32)
        mascara = np.expand_dims(mascara, axis=0)

        return torch.tensor(imagem), torch.tensor(mascara)

def treinar_e_exportar():
    dispositivo = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Dispositivo de treino: {dispositivo}")

    # Inicialização do Dataset e DataLoader
    dataset = FreiHANDDataset(PASTA_IMAGENS, PASTA_MASCARAS)
    dataloader = DataLoader(dataset, batch_size=16, shuffle=True)

    # Configuração da arquitetura U-Net (backbone MobileNetV2)
    modelo = smp.Unet(
        encoder_name="mobilenet_v2",
        encoder_weights="imagenet",
        in_channels=3,
        classes=1
    ).to(dispositivo)

    # Definição da função de perda e do otimizador
    criterio = nn.BCEWithLogitsLoss()
    otimizador = torch.optim.Adam(modelo.parameters(), lr=0.001)

    epocas = 5
    print("Iniciando o loop de treino...")

    for epoca in range(epocas):
        modelo.train()
        erro_total = 0.0

        for batch_idx, (imagens, mascaras) in enumerate(dataloader):
            imagens = imagens.to(dispositivo)
            mascaras = mascaras.to(dispositivo)

            otimizador.zero_grad()
            previsoes = modelo(imagens)

            erro = criterio(previsoes, mascaras)
            erro.backward()
            otimizador.step()

            erro_total += erro.item()

            if batch_idx % 50 == 0:
                print(f"Época [{epoca+1}/{epocas}] | Lote [{batch_idx}/{len(dataloader)}] | Loss: {erro.item():.4f}")

        print(f"--- Fim da Época {epoca+1} | Loss Médio: {erro_total/len(dataloader):.4f} ---")

    # Exportação do modelo para o formato ONNX
    print("\nExportando modelo para formato ONNX...")
    modelo.eval()
    modelo.to('cpu')

    tensor_exemplo = torch.randn(1, 3, 224, 224)
    caminho_onnx = "modelo_mao.onnx"

    torch.onnx.export(
        modelo,
        tensor_exemplo,
        caminho_onnx,
        export_params=True,
        opset_version=11,          # Compatível com Microsoft.ML.OnnxRuntime
        do_constant_folding=True,
        input_names=['input'],
        output_names=['output']
    )

    print(f"Exportação concluída. Arquivo salvo em: '{caminho_onnx}'.")

def main():
    treinar_e_exportar()

if __name__ == "__main__":
    main()
