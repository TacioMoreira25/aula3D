# Projeto de Visão Computacional - Rastreamento de Mão

Este documento explica a arquitetura e a lógica estrutural adotada no projeto de Visão Computacional para detecção da mão e como ele serve de base para implementações mais complexas.

## 1. Como rodar o projeto

Para compilar e executar o projeto, siga os passos abaixo no seu terminal (dentro da pasta do projeto):

1. Certifique-se de que possui o SDK do .NET instalado.
2. Certifique-se de que possui as bibliotecas do OpenCvSharp referenciadas no arquivo `.csproj` (geralmente `OpenCvSharp4` e a runtime do seu sistema operacional).
3. Abra o terminal na raiz do diretório do projeto.
4. Execute o comando:
   ```bash
   dotnet run
   ```
5. Para encerrar o programa, clique na janela de vídeo gerada pelo OpenCV e pressione a tecla `ESC`.

---

## 2. Arquitetura e Separação de Responsabilidades (Boas Práticas)

O projeto foi refatorado e separado em três camadas distintas para garantir manutenibilidade, redução de acoplamento e facilitar a escalabilidade. 

1. **DTO (Data Transfer Object) - `HandTrackingResult.cs`**  
   Uma classe limpa que atua apenas como um "pacote de dados". Ela representa o *resultado* do processamento sem conter nenhuma lógica de cálculo ou desenho. Retorna flags (`IsHandOpen`, `HandDetected`), textos de estado e coordenadas geométricas prontas para uso.

2. **Core / Engine Layer - `HandProcessor.cs`**  
   Esta classe é blindada e atua apenas realizando cálculos matemáticos e manipulação de matrizes de imagem. Recebe um quadro (frame) "cru", aplica algoritmos de visão computacional e cospe um objeto `HandTrackingResult`.  
   *Nota técnica:* Implementa a interface `IDisposable` para gerenciar a memória e o descarte de objetos não-gerenciados (`Mat`) de forma segura, evitando *Memory Leaks*.

3. **UI / Capture Layer - `Program.cs`**  
   É a ponte com o "mundo externo". Responsável apenas por obter o vídeo da webcam, manter a janela interativa aberta e desenhar elementos visuais (Círculos, Textos e Contornos) na tela consumindo o DTO gerado pelo Core.

---

## 3. Lógica Interna Passo a Passo (Rotina PDI em `HandProcessor`)

O processamento digital do frame da câmera é a essência do projeto. Ele roda 60 vezes por segundo seguindo exatamente este pipeline clássico de PDI:

1. **Recorte de Área de Segurança (ROI - Region of Interest):**  
   Ao invés de processar a tela inteira (o que rastrearia rostos e braços e consumiria muito processamento), cortamos fisicamente a imagem na lateral. Isso atua como um "Mousepad" seguro, garantindo que o que entra nessa zona retangular é estritamente a mão do usuário, blindando contra falsos-positivos e acelerando o algoritmo.

2. **Suavização (Gaussian Blur):** 
   Aplica-se um filtro de desfoque Gaussiano. Isso homogeniza os pixels, tirando impurezas, poros da pele, brilhos duros de suor ou ruídos (granulados) comuns em webcams baratas, preparando a superfície.

3. **Segmentação pelo Espaço de Cores Convencional (HSV):** 
   O frame é convertido do padrão BGR das telas para **HSV** (Matiz, Saturação e Brilho/Valor). O formato HSV não sofre variações drásticas quando você acende ou apaga uma luz (como o RGB sofre). Através do método `Cv2.InRange`, varremos a tela procurando apenas o "Matiz" exato da cor de pele (entre o vermelho e o marrom-claro).

4. **Limiarização (Thresholding / Máscara Binária):** 
   O resultado do corte HSV gera uma **Máscara Binária (Preto e Branco)**. Onde não há pele, o pixel ganha valor estrutural Preto (0), e onde há pele o pixel ganha o nível lógico Branco (255). A partir daqui, as cores deixam de importar e entramos na geometria plana.

5. **Morfologia Matemática (Opening, Closing, Dilate):** 
   Filtros morfológicos estruturais varrem a máscara em preto e branco.
   - `Open` (Erosão seguida de Dilatação): Apaga pingos brancos intrusos no fundo preto.
   - `Close` (Dilatação seguida de Erosão): Tampa buracos e falhas pretas dentro do bloco branco da mão.
   - `Dilate`: Engrosa e solidifica o contorno final da mão detectada.

6. **Busca Topológica (FindContours e Bounding Rect):** 
   O OpenCV mapeia as bordas do objeto branco criando uma malha de coordenadas. Nós filtramos para reter apenas o polígono de maior área (que presume-se ser nossa mão). Em volta dela nós traçamos nosso Retângulo Delimitador (`BoundingRect`).

7. **Trigonometria e Análise de Vetores (Defect Points / Mão Aberta vs Fechada):**
   - **`ConvexHull`**: Cria um casco virtual envolto da mão (como esticar um elástico em torno dos dedos abertos).
   - **`ConvexityDefects`**: Procura pelas "cavernas" e cortes entre as pontas do elástico (ou seja, os vãos entre os dedos).
   - **Lei dos Cossenos:** Para garantir que aquele corte é de fato a fissura de um dedo e não uma falha do pulso, triangulamos o buraco baseados na equação matemática do ângulo $cos(θ) = (b² + c² - a²) / 2bc$. Se o ângulo medir menos de 90° graus, confirmamos a existência de um vão válido.
   - Se houver mais de 3 vales num formato retangular longo, o algoritmo atesta no DTO que a mão está `ABERTA`. Se houver uma falha arredondada sem defeitos, classifica como concha `FECHADA`.

8. **Cálculo de Profundidade Simplificado (Simulação do Eixo Z):** 
   Para gerar noção 3D sem duas câmeras estéreo, utilizamos PDI posicional nativo através do Princípio da Área do Bounding Box. Quando o bloco aproxima da lente (aumenta o Zoom in), a área do retângulo explode exponencialmente em pixels. Aplicamos a Raiz Quadrada  (`Sqrt(Area) / 100`) para estabilizar o valor em uma escala flutuante suave linear de $1.0$ até $5.0$.

9. **Álgebra Especial para Coordenadas 3D (Spatial Moments / Eixos X,Y):**  
   Utilizando o Cálculo Funcional de Momentos (`M00`, `M10`, `M01`), extraímos a exata média vetorial do centro de massa do objeto branco. Esse é o ponto cirúrgico rastreado em X e Y na sua tela como se fosse a ponta de um Joystick.

---

## 4. Integração do PDI e Motores de Física 3D

- **Reutilização de Código:** O `HandProcessor.cs` pode ser exportado para uma DLL e usado numa Engine de Jogos (como Unity ou Godot) sem trazer consigo as bibliotecas de interface da janela.
- **Desempenho Otimizado:** Pré-alocar alocações de memória de Matrizes para as variáveis globais (`_blurred`, `_hsv`, `_mask`) salva ciclos pesados de GC (Garbage Collector) dentro do loop principal da câmera.
- **Fundação Lógica Integrada:** O DTO já repassa estados prontos (como `HandDetected` e `IsHandOpen`). Para aplicar mecânicas de "arrastar objeto" num jogo, basta ler essas flags em conjunto com a variável de posição geométrica mapeada sem refazer matemática no Frontend.