# Press kit do Bisect TD — handoff para o Claude Code

**Página publicada:** https://claude.ai/artifact/2U8UJDsJKGpudx74ebMkeY
**Documento da atividade:** https://claude.ai/artifact/LBvLQsL8gWJ7ZzKP6YGjmZ
**Apresentação:** https://claude.ai/artifact/JgKEVmdq7Kndec8kBzGDet (12 slides, mesmas cores e capturas da página)
**Fonte da página:** `PressKit/bisect-td-presskit.html` (HTML único; sprites e capturas embutidos em data URI)

## Estado (2026-11-04)
- As telas montadas foram trocadas por capturas reais do jogo, em `PressKit/screenshots/` (1080×1920):
  `board`, `swipe`, `deck`, `victory`, `action`. No HTML ficam em `SHOT` como JPEG 540×960 q82;
  `screen(kind)` devolve a captura no viewBox 180×320 e `keyArt()` usa a `action` recortada como cover.
- Removidos a pedido: o download do teaser de 30 s (e a simulação em canvas que o desenhava), a seção interativa
  "Cartas e teto de mana", a seção "Trailer" e, em Características, os cartões "Partida na hora",
  "Empilhe para evoluir" e "Golpe final em câmera lenta". Os downloads dos trailers de 60 s continuam.
- Downloads reais (capacidade `downloads` do artifact): one page PT/EN em PDF, Feature Graphic 1024×500, e os zips
  de logo e de screenshots, montados no navegador a partir dos arquivos em `PressKit/downloads/` (artifact não
  serve .zip). Manual do logo e trailers continuam ilustrativos.
- Artes do jogo (`downloads/artes/`, 98 arquivos): torres nos níveis 1-3, as 30 cartas, tropas, inimigos das waves e
  feitiços, PNG transparente com a cor que o jogo aplica. Geradas a partir de um manifesto lido do Unity (sprite e
  cor de cada prefab e carta); para refazer depois de mudar arte ou cor, exporte o manifesto de novo e recomponha.
- Logo para redes em `PressKit/logo/` (perfil 1080², logo com nome 1080², transparente 1024²), gerado da mesma
  marca do press kit. O ícone do app no Unity ainda é provisório (sprite da torre Circle).
- Nada da Flying Cakes na página (logo, textos, links e redes saíram). O desenvolvedor é João Kecek e o contato
  é só `joaokecek.business@gmail.com`. A única rede é o Instagram @bisect.td (real, https://www.instagram.com/bisect.td/).
- Progressão descrita como no build: cartas sobem de nível com ouro e cópias ganhos nas partidas; nada é vendido.

## Visual
- Layout de press kit (barra no topo, títulos centrais em caixa alta, lista de downloads),
  fonte Rubik, nas cores do jogo (JuiceTokens): fundo `#14171F`, superfície `#1B1F2A`, linha `#2E3444`,
  destaque no verde do jogador `#97F2DD` com texto escuro por cima, rival `#FF7273`, tinta `#F5F5F5`.
- O token de destaque é `--accent` / `--on-accent`.

## Como recapturar
1. `unity status` deve listar TCC-PUC como `ready` (pacote `com.unity.pipeline`). Game view em 1080×1920.
2. Play → MainMenu → trocar o deck em runtime com `BasePlayerSaveManager.SetDeckCards` (sem mexer no prefab) →
   Battle; o Bot entra em ~30 s. Copiar o `player_save.json` antes e restaurar depois: a vitória grava recompensa.
3. Capturar com `ScreenCapture.CaptureScreenshot("<caminho absoluto>")` via `unity command eval`.
4. Sem overlays de debug, FPS ou placeholders: esconder o Quantum Console e, na Victory, os rótulos `*Username`
   (o jogo ainda não preenche os nomes).
