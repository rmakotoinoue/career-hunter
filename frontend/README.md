# Frontend

Dashboard Angular do Career Hunter: **as vagas do dia**, com a descrição armazenada de cada
vaga e o link para a inscrição no site original.

App em [`career-hunter-web/`](career-hunter-web/) (Angular 22, standalone, SCSS, vitest).

## Como rodar

```bash
# API na porta 5080 (a mesma do compose e da documentação)
$env:ASPNETCORE_URLS='http://localhost:5080'; $env:Api__ApiKey='<chave>'
dotnet run --project src/CareerHunter.Api --no-launch-profile

# frontend
cd career-hunter-web
cp proxy.conf.example.json proxy.conf.json   # primeira vez: cole a chave em X-Api-Key
npm install                                   # primeira vez
npm start                                     # http://localhost:4200
npm test                                      # 6 testes (vitest)
```

## Como a chave de API é tratada

O app chama `/api/*` por URL relativa. O `ng serve` usa `proxy.conf.json`, que aponta para a
API **e injeta o header `X-Api-Key`** — a chave fica só nesse arquivo (ignorado pelo git, com
modelo em `proxy.conf.example.json`) e nunca entra no JavaScript gerado. Sem a chave, o proxy
devolve `401` e a tela mostra a mensagem de erro acionável.

## O que a tela faz

- Lista os itens do digest mais recente (seletor para trocar de dia);
- Detalhe da vaga em uma única chamada: `GET /api/digests/{id}/items` — descrição, local,
  modalidade, contrato, senioridade, faixa salarial, requisitos detectados e fonte;
- Filtro por título/empresa/local/descrição;
- Botão **Abrir vaga original ↗** (abre a URL do anúncio em outra aba) e **Prévia do e-mail**;
- Campos que a fonte não informou aparecem como ausentes — a interface nunca inventa valor.

## O que a tela NÃO faz

Seguindo a política da API: **nenhuma candidatura é enviada automaticamente**. Não há botão de
inscrição, envio de formulário nem mudança de status para `Applied`; o fluxo termina no link
original para você se candidatar manualmente.

## Contrato com a API

- `GET /api/digests?take=20` — lista de digests (data, status, contagem);
- `GET /api/digests/{id}/items` — itens do digest com o detalhe da vaga;
- `GET /api/digests/{id}/preview` — prévia do e-mail (abre em nova aba).

Tipos espelhados em `src/app/core/models.ts`. Demais telas previstas no contrato original
(perfil do candidato, catálogo completo, avaliação detalhada, candidatura) ainda não foram
implementadas.
