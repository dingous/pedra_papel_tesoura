PERSONAGENS CARTOON — PEDRA, PAPEL E TESOURA

Arquivos principais
  Male/RpsMale.fbx
  Female/RpsFemale.fbx

Os dois FBX incluem malha, materiais/textura e nove animações a 30 fps.
Altura aproximada: 1,70 m. Rig Generic, com 30 articulações dos dedos
(três por dedo, cinco dedos em cada mão). Não exige Blender para importar.

Clips
  Idle: repouso, ciclo de 2 segundos.
  CountIn: três movimentos de preparação com a mão direita, 1,5 segundo.
  Rock, Paper, Scissors: gesto da mão direita, 1,367 segundo.
  LeftCountIn, LeftRock, LeftPaper, LeftScissors: versões da mão esquerda.

As jogadas partem da mão fechada e revelam o gesto entre os frames 7 e 15.
A pose final é mantida até o frame 42 e não tem loop. Os clips não deslocam
o personagem pelo cenário. O movimento dos dedos está gravado no FBX.

Uso no Unity
  1. Arraste Male/RpsMale.prefab ou Female/RpsFemale.prefab para a cena.
  2. Use o componente RpsCharacterAnimator:
       SetHiddenHand()
       PlayCountIn()
       ShowChoice(RpsChoice.Rock / Paper / Scissors)
  3. Ative useLeftHand para jogar com a outra mão.

Abra RpsCharactersPreview.unity e pressione Play para ver a contagem e
as três jogadas dos dois personagens. Os prefabs têm Animator Controllers
com os clips prontos. RPS > Rebuild Character Assets reconstrói os assets.

A interface existente do jogo continua usando HandGestureView. Estes
assets e o componente RpsCharacterAnimator estão preparados para conectá-la
aos personagens 3D; a tela e o fluxo de partidas não foram substituídos.

Origem e edição
Modelos e texturas base criados no Meshy a partir das referências cartoon.
As mãos foram reconstruídas para deformação estável; rig dos dedos e
animações de pedra, papel e tesoura foram feitos no Blender.
Os arquivos de origem, scripts e imagens de verificação estão em
ArtSource/Characters na raiz do repositório.

Validação
Importação e compilação no Unity 6000.4.5f1; conferência dos nove clips por
personagem, das 30 articulações dos dedos, e das poses abertas/fechadas
nas duas mãos. Render de verificação produzido com os FBX importados.
