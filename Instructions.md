# Documentação Técnica e Arquitetural - Aula3D

## Arquitetura Híbrida
O projeto Aula3D adota uma arquitetura distribuída desenhada para maximizar a performance e isolar responsabilidades. O sistema é composto por duas camadas principais que comunicam em tempo real via protocolo UDP local:

* **TrackerService (Microserviço Python):** Responsável pela interface com o hardware de vídeo e pela inferência do modelo de Machine Learning.
* **Aplicação Desktop (C# .NET):** Construída utilizando a arquitetura Blazor Hybrid e o container Photino, atuando como o motor de renderização 3D e a interface com o utilizador.

## O Papel do Processamento Digital de Imagens (PDI)
Nesta nova iteração arquitetural, o uso de Processamento Digital de Imagens clássico (através da biblioteca OpenCV) foi estritamente isolado no microserviço Python (`TrackerService`). As operações de PDI são aplicadas apenas nas seguintes etapas de pré e pós-processamento:

* **Espelhamento Geométrico:** Inversão horizontal da matriz de imagem (flip) para garantir um feedback visual natural (efeito espelho).
* **Conversão de Espaços de Cores:** Transformação das frames do formato BGR nativo do OpenCV para RGB, requisito essencial para a rede neuronal MediaPipe.
* **Rasterização:** Desenho de overlays e anotações gráficas diretamente na frame para fornecer feedback visual da captura ao utilizador.

## Processamento no C# (VisionCore)
A camada C# foi completamente desacoplada de operações intensivas em matrizes de pixels. A responsabilidade do módulo C# (`VisionCore`) reside exclusivamente no processamento lógico e matemático dos dados.

A classificação de gestos e o cálculo das transformações do modelo baseiam-se puramente em Geometria Analítica e lógica espacial. O cliente C# consome apenas as coordenadas tridimensionais (Landmarks) extraídas em tempo real pelo modelo de Deep Learning em execução no Python. Esta abordagem elimina a sobrecarga computacional de processamento de imagem no cliente, otimizando drasticamente a performance, a responsividade e a fluidez da manipulação 3D no ambiente Desktop.

## Instruções de Execução

A inicialização da aplicação Desktop (C#) gere automaticamente o ciclo de vida do microserviço em Python (`TrackerService`).

Para iniciar todo o ambiente de desenvolvimento, execute o projeto na raiz:

```bash
dotnet run
```
