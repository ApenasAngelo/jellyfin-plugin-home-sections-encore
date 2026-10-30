# Home Screen Sections Encore

Fork do [Home Screen Sections](https://github.com/IAmParadox27/jellyfin-plugin-home-sections) (HSS, IAmParadox27,
GPL-3) para o Jellyfin 12.4 da VPS do Angelo. Responda em português do Brasil. O Angelo é iniciante: explique o porquê
das mudanças em uma ou duas frases.

Este repositório é **público** (fork de repositório público). Nada de segredos, IPs, nomes de usuários ou detalhes da
VPS aqui. A operação (Jellyfin, Caddy, backups, PreProbe) está no `~/ops/CLAUDE.md`, que é a referência; as decisões e o
histórico estão em `~/ops/plano-jellyfin-personalizacao.md` (seção "Sessão 6") e nas notas de sessão do `~/ops`. O
desenvolvimento é feito pela sessão **ops** (não há sessão de Remote Control própria).

## Branches e sincronia com o upstream

- `origin` = `ApenasAngelo/jellyfin-plugin-home-sections-encore`; `upstream` = `IAmParadox27/jellyfin-plugin-home-sections`.
- `main` **espelha** a `main` do upstream: nunca commitar nela.
- `encore` é a branch de trabalho e a padrão do GitHub. Recebe a `main` **por merge** (nunca rebase do que já foi
  publicado):
  ```bash
  git fetch upstream
  git switch main && git merge --ff-only upstream/main && git push origin main
  git switch encore && git merge main      # resolver conflitos só nos ganchos // Encore:
  ```
- `README.md` usa `merge=ours` (`.gitattributes`): o nosso sempre vence. Num clone novo, rodar uma vez
  `git config merge.ours.driver true`.
- Sem PRs para o upstream (decisão do Angelo).

## Regras do fork

- Tudo o que for novo vai em **arquivos novos** (seções em `HomeScreen/Sections/Encore/`, JS e CSS próprios em pastas
  `Encore`). Nos arquivos do autor, só **ganchos curtos marcados com `// Encore:`**. Antes de mexer num arquivo dele,
  ver se dá para fazer num arquivo nosso.
- Estilo do autor nos arquivos dele: chaves em linha nova, sem `var`, prefixos `m_`, nenhuma mudança só de espaço.
- GUID, nome e assembly próprios (o catálogo do autor não pode oferecer a versão dele por cima). O nome do assembly
  continua contendo `.HomeScreenSections`: outros plugins acham o HSS por esse texto para registrar seções.
- Commits em **inglês** (repositório público, pode receber contribuições), Conventional Commits, atômicos (ex.: `fix(web): find cardBuilder hooks in the 12.4 bundle`).
  O `CLAUDE.md` e a conversa continuam em português.

## Como o HSS funciona (lido em 2026-10-29)

- **Servidor** (`src/Jellyfin.Plugin.HomeScreenSections/`): cada fileira é uma classe `IHomeScreenSection`
  (`Library/IHomeScreenManager.cs`) em `HomeScreen/Sections/`; `CreateInstances` permite várias instâncias (ex.: vários
  "Porque você viu"). Endpoints em `Controllers/HomeScreenController.cs`: `/HomeScreen/Sections` (lista) e
  `/HomeScreen/Section/<nome>` (itens de uma fileira). Cache por usuário em `Data/` e `Services/PerUserComputedStatsCache.cs`.
  Outros plugins registram seções por `PluginInterface.RegisterSection` (README).
- **Cliente:** `Inject/HomeScreenSections.js|css` entram no `index.html` e `Controllers/loadSections.js` **substitui a
  função `loadSections`** do código minificado do jellyfin-web, pelo File Transformation (`Helpers/TransformationPatches.cs`,
  `Services/StartupService.cs`). Os 4 nomes minificados (`{{cardbuilder_hook}}`, `{{appRouterParent_hook}}`,
  `{{shapebuilder_hook}}`, `{{layoutmanager_hook}}`) são fixos por versão; no 12.x o do layoutManager está errado e a
  home não desenha (issue #312 do upstream, com diagnóstico). O `{{this_hook}}` já é achado por regex: fazer o mesmo
  para os 4.
- **Plugin Pages:** dependência opcional (sem ele, só um aviso no log). O fork **mantém** o suporte (não remover nem
  citar no README); só a nossa VPS não o instala e usa `AllowUserOverride=false` geral e por seção, para o admin
  decidir tudo na página de configuração do plugin.
- **Versões:** `JellyfinVersion` no `.csproj` (hoje `12.1.0`) escolhe o NuGet e o `net10.0`; código por versão em
  `JellyfinVersionSpecific/`. Alvo do fork: 12.4.
- **Lentidão conhecida:** "Recently Added Shows" (agrupa episódios: segundos a minutos) e "Because You Watched". Com o
  Gelato, "recentes" é a ordem de importação: fica de fora. A paginação ao rolar (`feat/home-screen-paging`) já está na
  `main`. Medir cada `/HomeScreen/Section/<nome>` antes de ligar uma fileira.
- O submódulo `jellyfin-web` (fork do autor) não é usado no build; não inicializar.

## Fileiras Encore (fase 2, 2026-10-30)

- `Encore/EncoreCatalog.cs`: títulos em memória (sem versões alternativas), 1,6 s para 1174, renovado a cada dia; gêneros
  em dois idiomas viram uma chave só. `EncoreHistory`: parte do que o usuário viu, invalidado por `PlaybackFinished`/`TogglePlayed`.
  `EncoreRecommendations`: lista semanal por usuário em JSON (`encore-recommendations.json`), tarefa domingo 07:20 UTC. `EncoreDraw`: sorteio estável por dia em `America/Sao_Paulo`.
- Seções em `HomeScreen/Sections/Encore/`: `EncoreGenre`, `EncoreSeriesGenre`, `EncoreAnimeGenre`, `EncoreKeyword`, `EncoreDailyPicks`,
  `EncoreNational`, `EncoreSeasonal`, `EncoreMyList` (sem cache), `EncoreAlmostDone`, `EncoreWatchAgain`, `EncoreForYou`.
- A chave da fileira vira classe CSS: só `a-z`, `0-9` e hífen.
- `CreateInstances` segura a lista inteira de seções: nada pesado ali.
- Paginação: marcador no fim das fileiras (`IntersectionObserver`), o gatilho de rolagem do autor continua lá. `PageHash` vem do cliente.
- Nas capturas e testes, o navegador precisa de `locale`: sem ele o Jellyfin lança `Invalid language tag` e as fileiras ficam vazias.

## Páginas no formato da home (fase 3, 2026-10-30)

- Filmes, Séries, Animes, Coleções e a página de uma coleção **são a própria home**: `#/home?topParentId=<id>` (coleção: mais
  `&encoreCollection=<id>`; aba Coleções: mais `&encoreKind=boxsets`). O menu marca a aba sozinho pelo `topParentId`.
  Por que não dentro das páginas nativas: em `/movies`, `/tv`, `/mixed` e `/boxsets` o chunk do `loadSections` nem é carregado.
- `Inject/EncoreViews.js`: reescreve links nativos (clique em captura no `window`, mais verificação a cada 300 ms, porque o app
  navega sem `hashchange`), redesenha a home com o evento `settingschange` no `.sections` (a mesma instância da página continua aberta) e
  põe o botão "Ver tudo". A grade nativa fica por `sessionStorage` (`encore-grid-library`), limpo no próximo clique do menu.
- Servidor: `Controllers/EncoreViewsController.cs` (`HomeScreen/Encore/Views` e `/View`), sem cache de página: a lista é calculada e a
  ordem vem do `PageHash`. Fileiras leem o parâmetro `View` (`EncoreCatalog.ScopeOf`). Com o HSS desligado, os links ficam como estão.
- `elem.dataset.encoreView` marca a home desenhada e também denuncia chunk antigo (`EncoreChunkRefresh.js`).

## Cuidados com o servidor

- **PreProbe do Gelato:** abrir um item por `GET /Items/{id}` ou `/Users/{u}/Items/{id}`, ou pedir `MediaSources`, faz o
  Gelato analisar a fonte e pôr o torrent no TorBox. As seções montam itens dentro do servidor (`DtoOptions`); contar
  `Probing stream` no log do Jellyfin com a home aberta antes de liberar qualquer versão. Detalhes no `~/ops/CLAUDE.md`.
- Instalar, trocar ou reiniciar no Jellyfin só com o OK do Angelo, e com backup antes (`/data/backups/jellyfin/`).
- Capturas: `~/ops/scripts/jellyfin-captura.sh` (conta `claude-teste`).

## Build e publicação

- Build na VPS (ARM64) só em container do .NET 10 SDK (nada de SDK instalado no sistema).
- Publicação: release por tag `vX.Y.Z.W` (GitHub Actions) e entrada no manifesto do repositório de plugins
  `ApenasAngelo/jellyfin-plugins`. A montar na sessão 7 (`~/ops/prompts/jellyfin-sessao-7.md`).
