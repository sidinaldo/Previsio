// Configuração do painel no build de produção (troca o previsio.config.ts, ver angular.json).
// O nginx entrega o painel e repassa /api para a API no mesmo endereço: sem domínio fixo e sem CORS.
export const PREVISIO_CONFIG = {
  apiUrl: '/api'
};
