# Frontend

Não implementado nesta entrega: o Angular CLI não está instalado no ambiente de build e a
geração do app foi deliberadamente adiada para não entregar um esqueleto não executável.

## Como criar quando o CLI estiver disponível

```bash
npm install -g @angular/cli
ng new career-hunter-web --directory frontend/career-hunter-web --routing --style=scss --standalone --skip-git
```

## Contrato esperado com a API

- Toda chamada a `/api/*` envia o header `X-Api-Key` (quando `API_KEY` está configurado).
- Telas previstas:
  - perfil do candidato (dados declarados; nunca preenchidos automaticamente);
  - catálogo de vagas com pontuação, requisitos atendidos/faltantes e evidências;
  - detalhe da avaliação (motor, versão do prompt, modelo, data);
  - candidatura: materiais preparados, mensagem ao recrutador, expectativa salarial e link
    original da vaga;
  - mudança de status — `Applied` exige confirmação explícita do usuário na interface.
- A interface **não** deve oferecer envio automático de candidatura: o fluxo termina em
  "abrir link original" para submissão manual.
