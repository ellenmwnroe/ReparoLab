# Reparo Lab

Experiência em **Realidade Virtual** para o **SEBRAELab**, onde o jogador ajuda a **Lia**, zeladora do espaço, a encontrar e consertar defeitos no salão antes de um evento.

Projeto da disciplina **Tecnologias Emergentes** — UNDB, Escola de Tecnologia, semestre 2026.2 (Prof. Marcos Vinicius Januário da Silva).

> **Equipe:** Ellen Monroe, Davi de Jesus, Rodrigo Gois e Telma Regina

---

## Desafio escolhido

**4. Conservação Patrimonial** — um simulador voltado para a identificação de falhas e a manutenção do patrimônio usando mecânicas interativas. A experiência também usa ideias do **Modo Visita Guiada**: uma NPC que guia o jogador e pontos de interesse marcados no trajeto.

## A solução

O jogador entra no salão do SEBRAELab, que vai receber um evento. A Lia conta que o espaço tem **3 defeitos** e guia o jogador passo a passo:

| Defeito | Onde fica | Ferramenta certa | Conserto |
|---|---|---|---|
| Fissura na parede | parede do salão | Espátula | encostar a espátula na rachadura |
| Mancha de infiltração | parede da entrada | Rolo de tinta | encostar o rolo na mancha |
| Lâmpada queimada | teto | Lâmpada nova | encaixar a lâmpada no soquete |

Quando o jogador registra um defeito, a Lia explica a **causa** e a **prevenção**. Esse é o conteúdo educativo sobre conservação do patrimônio. No fim, o jogador **libera o espaço** e vê um relatório com o tempo e os erros.

**Público-alvo:** visitantes do SEBRAELab, empreendedores e estudantes interessados em gestão e conservação de espaços.

## Loop principal

**Início** (a Lia apresenta a missão) → **Interação** (teleportar, registrar, pegar a ferramenta certa e consertar, 3 vezes) → **Resultado** (botão *Liberar espaço*, relatório com tempo e erros e opção *Jogar de novo*).

Duração estimada: **3 a 5 minutos**.

## Jornada do usuário

1. **Entrada:** o jogador aparece no salão, de frente para o espaço. O painel da Lia surge à sua frente.
2. **Primeiro passo:** a Lia se apresenta. O jogador avança com o botão **Continuar**.
3. **Teleporte:** uma seta marca o chão perto da primeira parede e o jogador se teleporta até lá.
4. **Fissura (com mais dicas):**
   - registrar olhando 1 s para a rachadura;
   - pegar a espátula, que está brilhando;
   - levar a espátula até a rachadura. O brilho fica mais forte conforme ela se aproxima.
5. **Mancha e lâmpada:** as mesmas etapas, com menos dicas.
6. **Final:** a seta aponta o botão **LIBERAR ESPAÇO**. A Lia comenta o desempenho e aparece o relatório final.

## Interações implementadas

1. Teleporte pelo salão
2. Registrar defeito **olhando por 1 segundo** (ou apontando o raio + grip)
3. Pegar ferramenta (XR Grab Interactable)
4. Consertar encostando a ferramenta certa no defeito, com contagem de erro se a ferramenta for errada
5. Encaixar a lâmpada nova no soquete (XR Socket Interactor)
6. Botão **Continuar** no diálogo
7. Botão **Liberar espaço**
8. Botão **Jogar de novo**

## Sistemas lógicos

- **Estados dos defeitos:** Intacto → Identificado → Reparado (`Defeito.cs`).
- **Checklist, cronômetro e erros**, com relatório final (`GerenciadorJogo.cs`).
- **Roteiro guiado / conversa** que avança sozinho conforme o jogador age (`GuiaTutorial.cs` + `DialogoVN.cs`).
- **Registro pelo olhar**, com tempo de permanência e progresso mostrado na seta.
- **Ferramentas** que não caem: ficam paradas no ar ao soltar e voltam para a mesa (`Ferramenta.cs`).

## Escopo técnico

- **Cena:** `Assets/Scenes/Scene1.unity`, com o modelo 3D do SEBRAELab fornecido pelo professor.
- **Engine:** Unity **6000.4.10f1** + **XR Interaction Toolkit 3.4.1** (Starter Assets).
- **Locomoção:** teleporte (Teleportation Area sobre o piso) + snap turn do XR Origin.
- **Interface:** UI World Space — painel de diálogo da Lia que acompanha o jogador, painel de relatório e rótulos 3D.
- **Guias visuais:** seta flutuante no alvo atual, defeito pulsando, brilho na ferramenta certa.
- **Áudio espacial:**
  - os sons de **registro, conserto e ferramenta errada** saem em 3D da posição de cada defeito;
  - a **lâmpada queimada zumbe** em loop, em 3D, e o zumbido some quando ela é trocada;
  - som de **vitória** ao liberar o espaço;
  - **trilha de fundo** em loop, 2D e com volume baixo.
- **Hardware alvo:** Meta Quest 2 (testado no XR Device Simulator).
- **Performance (Quest 2):**
  - só uma luz pontual nova, que acende quando a lâmpada é consertada;
  - guias com materiais **Unlit**;
  - nenhum Mesh Collider novo, só Box Colliders;
  - fontes com atlas estático de 1024 px;
  - música em streaming com compressão Vorbis.

## Controles

**XR Device Simulator (PC):**
- segure **Espaço** + mouse para mover a mão direita;
- **G** (grip) para pegar, registrar e apertar botões;
- olhar para um defeito por 1 s também registra.

**Meta Quest 2:**
- apontar o raio do controle;
- **grip** (botão lateral) para pegar e apertar;
- analógico para teleportar e girar.

## Como abrir

1. Abra a pasta do projeto no **Unity Hub** com a versão **6000.4.10f1**.
2. Abra `Assets/Scenes/Scene1.unity`, que já está em primeiro lugar na Scene List.
3. Aperte **Play**. O XR Device Simulator já está ativo na cena.

### Ferramentas de montagem (menu *Reparo Lab*)

| Menu | O que faz |
|---|---|
| Preparar cena completa | confere XR Origin, Device Simulator e teleporte, monta o jogo, posiciona os defeitos nas paredes e no teto, salva e configura a Scene List |
| Montar tutorial da Lia | cria as fontes TMP, o painel de diálogo, a seta, os brilhos e o roteiro guiado |
| Adicionar trilha sonora | música de fundo em loop, com volume baixo |
| Adicionar sons 3D | liga os efeitos sonoros espaciais aos defeitos, o zumbido da lâmpada e a vitória |
| Criar fontes TMP | gera só os Font Assets (Fredoka + Nunito, Latin completo) |

## Estrutura

```
Assets/
  Scripts/        Defeito, Ferramenta, GerenciadorJogo, DialogoVN, GuiaTutorial
  Editor/         MontarReparoLab, PrepararReparoLab, MontarTutorialLia, TrilhaReparoLab
  ReparoLab/      Fontes, Materiais, Malhas e Audio do projeto
  Scenes/Scene1   cena principal
  Sebraelab3D/    modelo 3D do SEBRAELab (material base da disciplina)
```

## Créditos

- **Modelo 3D do SEBRAELab e projeto base:** fornecidos pelo professor da disciplina.
- **Fontes:** [Fredoka](https://fonts.google.com/specimen/Fredoka) e [Nunito](https://fonts.google.com/specimen/Nunito), Google Fonts, licença SIL Open Font License (os textos das licenças estão em `Assets/ReparoLab/Fontes`).
- **Trilha sonora:** _[autor / fonte / licença da música]_
- **Efeitos sonoros:** sintetizados especialmente para o projeto (`Assets/ReparoLab/Audio/Sfx`).
- **XR Interaction Toolkit:** Unity Technologies.
