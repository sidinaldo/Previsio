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
