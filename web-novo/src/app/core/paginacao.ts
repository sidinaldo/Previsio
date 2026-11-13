import { totalDePaginas } from './filtro-falhas';

/** Quantas linhas por página as tabelas mostram ao abrir. */
export const TAMANHO_PAGINA_PADRAO = 10;

/** Opções do "Por página" das tabelas paginadas na própria tela. */
export const TAMANHOS_PAGINA = [10, 25, 50];

/**
 * Página válida para a quantidade de itens: se a lista encolheu (filtro, item desativado),
 * a página pedida pode não existir mais e vale a última.
 */
export function paginaValida(pagina: number, total: number, tamanho: number): number {
  const ultima = totalDePaginas(total, tamanho);
  if (pagina < 1) {
    return 1;
  }
  if (pagina > ultima) {
    return ultima;
  }
  return pagina;
}

/** Itens da página pedida, para tabelas que recebem a lista inteira da API. */
export function paginar<T>(lista: T[], pagina: number, tamanho: number): T[] {
  const atual = paginaValida(pagina, lista.length, tamanho);
  const inicio = (atual - 1) * tamanho;
  return lista.slice(inicio, inicio + tamanho);
}

/** Opções do "Por página" das listas paginadas pela API (contas, notas recebidas, falhas); a API aceita até 100. */
export const TAMANHOS_PAGINA_API = [10, 25, 50, 100];
export const TAMANHO_PAGINA_API_PADRAO = 25;

const CHAVE_TAMANHO_PAGINA = 'previsio.por-pagina.';

/** "Por página" escolhido nesta tela, guardado neste navegador (ou o padrão). */
export function lerTamanhoPagina(tela: string): number {
  try {
    const salvo = Number(localStorage.getItem(CHAVE_TAMANHO_PAGINA + tela));
    if (TAMANHOS_PAGINA_API.includes(salvo)) {
      return salvo;
    }
  } catch {
    // Navegador sem armazenamento: vale o padrão
  }
  return TAMANHO_PAGINA_API_PADRAO;
}

export function salvarTamanhoPagina(tela: string, tamanho: number): void {
  try {
    localStorage.setItem(CHAVE_TAMANHO_PAGINA + tela, String(tamanho));
  } catch {
    // Navegador sem armazenamento: a escolha vale só até sair da tela
  }
}
