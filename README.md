# Aula3D

## Visão Geral
O Aula3D é uma ferramenta interativa inovadora desenvolvida para a manipulação de modelos 3D de forma fluida e sem contacto físico (touch-free). Utilizando apenas uma webcam padrão, o sistema captura e interpreta gestos corporais em tempo real, permitindo aos utilizadores uma experiência imersiva e natural na exploração de objetos tridimensionais.

## Usabilidade e Controlos
A interação com o sistema foi desenhada para ser intuitiva, traduzindo movimentos naturais em comandos espaciais. A câmara rastreia as mãos do utilizador e aplica as transformações correspondentes ao modelo 3D.

Os gestos de controlo definidos são:

* **Uma Mão Aberta:** Rotação do modelo. Permite rodar o objeto 3D de forma contínua em 360 graus em qualquer eixo.
* **Uma Mão Fechada:** Translação do modelo (Pan). Permite agarrar o objeto e movê-lo livremente pela área de visualização.
* **Duas Mãos Abertas:** Escala do modelo (Zoom). Afastar as mãos aproxima o modelo (zoom in), enquanto aproximar as mãos reduz o modelo (zoom out).

## Zona de Segurança (Safe Margin)
Para evitar comportamentos erráticos durante a interação, o sistema implementa o conceito de Zona de Segurança (Safe Margin) nas bordas do enquadramento da câmara.

Quando o utilizador move a mão para além desta margem central e atinge as bordas da área de captura, o modelo fica ancorado na sua última posição registada. Este mecanismo assegura que a manipulação espacial não sofra arrastos acidentais ou movimentos indesejados ao finalizar uma interação ou ao retirar a mão do campo de visão.
