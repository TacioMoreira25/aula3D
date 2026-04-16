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
# Substitua pelo caminho exato de onde extraiu o FreiHAND no seu disco secundário
DIRETORIO_DATASET = "FreiHAND"
PASTA_IMAGENS = os.path.join(DIRETORIO_DATASET, "training/rgb/")
PASTA_MASCARAS = os.path.join(DIRETORIO_DATASET, "training/mask/")

# ==============================================================================
# 2. DEFINIÇÃO DO DATASET (Como o PyTorch lê os seus ficheiros)
# ==============================================================================
class FreiHANDDataset(Dataset):
    def __init__(self, pasta_imagens, pasta_mascaras):
        self.pasta_imagens = pasta_imagens
        self.pasta_mascaras = pasta_mascaras
        self.nomes_ficheiros = []

        # 1. Pega em todos os ficheiros da pasta de imagens
        todos_ficheiros = [f for f in os.listdir(pasta_imagens) if f.endswith('.jpg')]

        print(f"A analisar a integridade de {len(todos_ficheiros)} imagens. Por favor, aguarde...")

        # 2. Mantém na lista apenas as imagens que têm uma máscara real no disco
        for ficheiro in todos_ficheiros:
            caminho_mask_jpg = os.path.join(pasta_mascaras, ficheiro)
            caminho_mask_png = os.path.join(pasta_mascaras, ficheiro.replace('.jpg', '.png'))

            # Se a máscara existir em .jpg ou .png, é um ficheiro válido!
            if os.path.exists(caminho_mask_jpg) or os.path.exists(caminho_mask_png):
                self.nomes_ficheiros.append(ficheiro)

        print(f"Limpeza concluída! Vamos treinar com {len(self.nomes_ficheiros)} pares perfeitos.")

    def __len__(self):
        return len(self.nomes_ficheiros)

    def __getitem__(self, idx):
        nome_ficheiro = self.nomes_ficheiros[idx]

        # Caminho da imagem real
        caminho_img = os.path.join(self.pasta_imagens, nome_ficheiro)
        # Caminho base para tentar a máscara
        caminho_mask = os.path.join(self.pasta_mascaras, nome_ficheiro)

        # 1. Carregar imagem real
        imagem = cv2.imread(caminho_img)
        if imagem is None:
            raise FileNotFoundError(f"Erro: O OpenCV não conseguiu ler a imagem em {caminho_img}")

        imagem = cv2.cvtColor(imagem, cv2.COLOR_BGR2RGB)

        # 2. Carregar a máscara de forma inteligente
        mascara = cv2.imread(caminho_mask, cv2.IMREAD_GRAYSCALE)

        # SE A MÁSCARA NÃO FOR .JPG, TENTA .PNG (Muito comum no FreiHAND)
        if mascara is None:
            caminho_mask_png = os.path.join(self.pasta_mascaras, nome_ficheiro.replace('.jpg', '.png'))
            mascara = cv2.imread(caminho_mask_png, cv2.IMREAD_GRAYSCALE)

        # Proteção final: Se falhar em ambos os formatos, pára de forma limpa
        if mascara is None:
            raise FileNotFoundError(f"Erro crítico: A máscara para a imagem '{nome_ficheiro}' não foi encontrada na pasta {self.pasta_mascaras}. Verifique as extensões.")

        # 3. Pré-processamento (Agora seguro, porque sabemos que 'mascara' tem dados)
        imagem = imagem.astype(np.float32) / 255.0
        imagem = np.transpose(imagem, (2, 0, 1))

        mascara = (mascara > 127).astype(np.float32)
        mascara = np.expand_dims(mascara, axis=0)

        return torch.tensor(imagem), torch.tensor(mascara)

def treinar_e_exportar():
    # Verifica se tem placa gráfica (GPU), caso contrário usa o processador (CPU)
    dispositivo = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"A treinar no dispositivo: {dispositivo}")

    # 1. Preparar os Dados
    dataset = FreiHANDDataset(PASTA_IMAGENS, PASTA_MASCARAS)
    # batch_size=16 significa que processa 16 imagens de cada vez. Se faltar RAM, reduza para 8.
    dataloader = DataLoader(dataset, batch_size=16, shuffle=True)

    # 2. Construir o Modelo U-Net (MobileNetV2)
    modelo = smp.Unet(
        encoder_name="mobilenet_v2",
        encoder_weights="imagenet", # Usa conhecimento prévio para treinar mais rápido
        in_channels=3,
        classes=1
    )
    modelo = modelo.to(dispositivo)

    # 3. Configurar Otimizador e Função de Erro
    # BCEWithLogitsLoss é ideal para máscaras binárias (preto e branco)
    criterio = nn.BCEWithLogitsLoss()
    otimizador = torch.optim.Adam(modelo.parameters(), lr=0.001)

    # 4. Iniciar Treino (Vamos fazer 5 épocas apenas para obter um modelo inicial viável)
    epocas = 5
    print("Iniciando o treino...")

    for epoca in range(epocas):
        modelo.train()
        erro_total = 0.0

        for batch_idx, (imagens, mascaras) in enumerate(dataloader):
            imagens = imagens.to(dispositivo)
            mascaras = mascaras.to(dispositivo)

            # Passagem pela rede
            otimizador.zero_grad()
            previsoes = modelo(imagens)

            # Calcular erro e corrigir pesos (Backpropagation)
            erro = criterio(previsoes, mascaras)
            erro.backward()
            otimizador.step()

            erro_total += erro.item()

            if batch_idx % 50 == 0:
                print(f"Época [{epoca+1}/{epocas}] | Lote [{batch_idx}/{len(dataloader)}] | Erro: {erro.item():.4f}")

        print(f"--- Fim da Época {epoca+1} | Erro Médio: {erro_total/len(dataloader):.4f} ---")

    # 5. Exportar para ONNX
    print("\nTreino concluído! A preparar exportação para ONNX...")
    modelo.eval()
    modelo.to('cpu') # Movemos para a CPU para garantir que a exportação é neutra

    # Criar um "tensor falso" com as dimensões exatas que a sua Webcam enviará em C#
    tensor_exemplo = torch.randn(1, 3, 224, 224)

    caminho_onnx = "modelo_mao.onnx"
    torch.onnx.export(
        modelo,
        tensor_exemplo,
        caminho_onnx,
        export_params=True,
        opset_version=11,          # Versão suportada pelo Microsoft.ML.OnnxRuntime
        do_constant_folding=True,
        input_names=['input'],     # Nome da variável que usaremos no C#
        output_names=['output']    # Nome do retorno que usaremos no C#
    )

    print(f"Sucesso! Ficheiro '{caminho_onnx}' gerado na pasta atual.")

def main():
    treinar_e_exportar()

if __name__ == "__main__":
    main()
