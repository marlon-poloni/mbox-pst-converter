# Conversor de e-mails (mbox para PST)

Converte exports mbox do Gmail (Google Takeout) em arquivos PST para importar no Outlook,
transformando cada marcador do Gmail em pasta. Antes de converter, mostra como as pastas vão
ficar no Outlook e deixa ajustar o destino de cada marcador. Roda no computador, sem acesso à
rede, em Linux (`.deb`) e Windows (instalador `.exe`).

- Marcadores padrão do Gmail vão para as pastas equivalentes do Outlook em português
  (Caixa de Entrada, Itens Enviados, Rascunhos, Arquivo Morto, Lixo Eletrônico, Itens Excluídos).
- Marcadores criados pela pessoa viram pastas com o mesmo nome (`Clientes/ACME` vira subpasta).
- "Não lidas" e "Com estrela" viram estado do e-mail (não lido, sinalizado), não pasta.
- Cada marcador pode virar pasta, marcação, ser ignorado ou ficar de fora da conversão.
- Ao terminar, o PST é relido e conferido pasta por pasta contra a prévia; o relatório
  (`<nome>-relatorio.json` e `.txt`) fica ao lado do arquivo.

Como conferir um PST no Outlook: `docs/VALIDACAO-OUTLOOK.md`.

## Estrutura

| Pasta | O que tem |
|---|---|
| `vendor/PSTFileFormat` | motor de PST (LGPL-3.0-or-later), com as modificações em `vendor/PSTFileFormat-MODIFICATIONS.md` |
| `src/Mail2Pst.Core` | leitura de mbox e gravação de PST, vindos do ContinuMail Converter (GPL-3.0-or-later) |
| `src/MboxPst.Core` | marcadores do Gmail, de-para, prévia, conversão, verificação e relatório |
| `src/MboxPst.Sidecar` | executável `mboxpst-core`: protocolo JSON Lines usado pela interface |
| `app/` | interface Tauri 2 (HTML/CSS/TypeScript) |
| `tests/` | testes C#, fixtures sintéticas e o verificador independente (`tests/independent/verify_pst.py`, usa libpff) |
| `tools/MboxPst.Generator` | gerador de mbox sintético grande, para teste de volume |

## Desenvolvimento

Requer .NET SDK 8, Node 22, Rust estável e, no Linux, `libwebkit2gtk-4.1-dev`.

```bash
dotnet test                                   # núcleo
MBOXPST_PYTHON=python3 dotnet test            # inclui o verificador independente (pip install -r tests/independent/requirements.txt)
MBOXPST_VOLUME=1 dotnet test -c Release --filter Category=Volume   # 6 GB sintéticos: tempo e memória

cd app
npm ci
npx vitest run                                # estado, controlador e telas
npx playwright test                           # prints das telas em 1280×800 e 1024×680
../scripts/build-sidecar.sh linux-x64 && npx tauri dev
```

Sem o Tauri (no navegador), `npx vite` abre a interface com um núcleo simulado.

## Pacotes

```bash
scripts/build-sidecar.sh linux-x64
cd app && npx tauri build --bundles deb
```

Instalador do Windows: workflow `release.yml` (tag `v*`, compila num Windows de verdade) ou
compilação cruzada a partir do Linux com `scripts/build-windows-cross.sh` (sem sudo; instruções
no começo do script). Os pacotes gerados localmente ficam em `dist/`.

## Licença

GPL-3.0-or-later. Componentes de terceiros e suas licenças em `NOTICE`.
