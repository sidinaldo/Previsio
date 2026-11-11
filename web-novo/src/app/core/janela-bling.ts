/**
 * Conexão com o Bling numa janela separada: o Previsio fica aberto atrás e é avisado
 * quando a autorização termina.
 *
 * A volta do Bling cai em /pages/empresas?bling=... dentro da janela. Essa página avisa
 * as outras abas do Previsio por BroadcastChannel (mesma origem, funciona mesmo que o
 * Bling corte a ligação com a janela que abriu) e se fecha.
 */
export const NOME_JANELA_BLING = 'previsio-bling';

const CANAL = 'previsio-bling';

// Tamanho da janela de autorização do Bling
const RECURSOS_JANELA = 'popup,width=640,height=780';

/**
 * Abre a janela vazia já no clique (depois da chamada à API o navegador bloquearia o popup).
 * Null quando o navegador bloqueou.
 */
export function abrirJanelaBling(): Window | null {
  return window.open('', NOME_JANELA_BLING, RECURSOS_JANELA);
}

/** Esta aba é a janela de autorização aberta pelo Previsio? */
export function ehJanelaBling(): boolean {
  return window.opener != null || window.name === NOME_JANELA_BLING;
}

/** Avisa as outras abas do Previsio como terminou a autorização. */
export function avisarRetornoBling(retorno: string): void {
  if (typeof BroadcastChannel === 'undefined') {
    return;
  }
  const canal = new BroadcastChannel(CANAL);
  canal.postMessage(retorno);
  canal.close();
}

/** Escuta o fim da autorização vindo da janela do Bling. Devolve a função que para de escutar. */
export function ouvirRetornoBling(aoReceber: (retorno: string) => void): () => void {
  if (typeof BroadcastChannel === 'undefined') {
    return () => undefined;
  }
  const canal = new BroadcastChannel(CANAL);
  canal.onmessage = evento => aoReceber(String(evento.data));
  return () => canal.close();
}
