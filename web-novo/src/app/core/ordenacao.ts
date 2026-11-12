/** Coluna e direção em que uma tabela está ordenada. */
export interface Ordenacao {
  coluna: string;
  decrescente: boolean;
}

/**
 * Clique no cabeçalho: outra coluna começa crescente; a mesma coluna alterna a direção.
 */
export function alternarOrdenacao(atual: Ordenacao | null, coluna: string): Ordenacao {
  if (atual !== null && atual.coluna === coluna) {
    return { coluna: coluna, decrescente: !atual.decrescente };
  }
  return { coluna: coluna, decrescente: false };
}

/** Parâmetro "ordem" da API: a coluna, com "-" na frente quando decrescente. */
export function parametroOrdem(ordenacao: Ordenacao | null): string {
  if (ordenacao === null) {
    return '';
  }
  if (ordenacao.decrescente) {
    return '-' + ordenacao.coluna;
  }
  return ordenacao.coluna;
}

/** Valor usado para comparar uma linha numa coluna. Nulo vai para o fim. */
export type ValorOrdenacao = string | number | null | undefined;

/**
 * Lista ordenada pela coluna, para tabelas que recebem a lista inteira da API.
 * Texto compara em português (acentos e maiúsculas não atrapalham); número compara como número.
 * Sem ordenação (ou coluna desconhecida), devolve a lista como veio.
 */
export function ordenarLista<T>(
  lista: T[],
  ordenacao: Ordenacao | null,
  colunas: Record<string, (item: T) => ValorOrdenacao>
): T[] {
  if (ordenacao === null) {
    return lista;
  }
  const valor = colunas[ordenacao.coluna];
  if (!valor) {
    return lista;
  }
  const sinal = ordenacao.decrescente ? -1 : 1;
  return [...lista].sort((a, b) => {
    const va = valor(a);
    const vb = valor(b);
    const aVazio = va === null || va === undefined || va === '';
    const bVazio = vb === null || vb === undefined || vb === '';
    if (aVazio && bVazio) {
      return 0;
    }
    if (aVazio) {
      return 1;
    }
    if (bVazio) {
      return -1;
    }
    if (typeof va === 'number' && typeof vb === 'number') {
      return (va - vb) * sinal;
    }
    return String(va).localeCompare(String(vb), 'pt-BR', { sensitivity: 'base', numeric: true }) * sinal;
  });
}

/** Texto sem acentos e em minúsculas, para comparar a busca. */
export function normalizar(texto: string | null | undefined): string {
  if (!texto) {
    return '';
  }
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/**
 * A linha combina com a busca? Procura o texto em todos os campos; se a busca tem 3 dígitos ou mais,
 * procura também só os dígitos (CNPJ digitado com ou sem pontuação).
 */
export function combinaComBusca(busca: string, campos: (string | null | undefined)[]): boolean {
  const termo = normalizar(busca.trim());
  if (termo === '') {
    return true;
  }
  const digitos = termo.replace(/\D/g, '');
  for (const campo of campos) {
    const texto = normalizar(campo);
    if (texto.includes(termo)) {
      return true;
    }
    if (digitos.length >= 3 && texto.replace(/\D/g, '').includes(digitos)) {
      return true;
    }
  }
  return false;
}
