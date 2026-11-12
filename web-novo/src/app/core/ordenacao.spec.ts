import { alternarOrdenacao, combinaComBusca, ordenarLista, parametroOrdem } from './ordenacao';

interface Linha {
  nome: string;
  valor: number | null;
}

const LINHAS: Linha[] = [
  { nome: 'Ótica Zeta', valor: 50 },
  { nome: 'alfa', valor: null },
  { nome: 'Beta', valor: 900 },
  { nome: 'Auto 10', valor: 120 },
  { nome: 'Auto 9', valor: 7 }
];

const COLUNAS = { nome: (l: Linha) => l.nome, valor: (l: Linha) => l.valor };

describe('ordenacao', () => {
  it('clique na mesma coluna alterna; em outra começa crescente', () => {
    const primeira = alternarOrdenacao(null, 'valor');
    expect(primeira).toEqual({ coluna: 'valor', decrescente: false });
    expect(alternarOrdenacao(primeira, 'valor')).toEqual({ coluna: 'valor', decrescente: true });
    expect(alternarOrdenacao({ coluna: 'valor', decrescente: true }, 'nome')).toEqual({ coluna: 'nome', decrescente: false });
  });

  it('parâmetro da API: "-" na frente quando decrescente', () => {
    expect(parametroOrdem({ coluna: 'valor', decrescente: true })).toBe('-valor');
    expect(parametroOrdem({ coluna: 'numero', decrescente: false })).toBe('numero');
    expect(parametroOrdem(null)).toBe('');
  });

  it('texto em português: sem diferenciar acento/maiúscula e com números na ordem natural', () => {
    const nomes = ordenarLista(LINHAS, { coluna: 'nome', decrescente: false }, COLUNAS).map(l => l.nome);
    expect(nomes).toEqual(['alfa', 'Auto 9', 'Auto 10', 'Beta', 'Ótica Zeta']);
  });

  it('número como número, vazio sempre no fim, nas duas direções', () => {
    expect(ordenarLista(LINHAS, { coluna: 'valor', decrescente: false }, COLUNAS).map(l => l.valor)).toEqual([7, 50, 120, 900, null]);
    expect(ordenarLista(LINHAS, { coluna: 'valor', decrescente: true }, COLUNAS).map(l => l.valor)).toEqual([900, 120, 50, 7, null]);
  });

  it('sem ordenação devolve a lista como veio, sem alterar a original', () => {
    expect(ordenarLista(LINHAS, null, COLUNAS)).toBe(LINHAS);
    ordenarLista(LINHAS, { coluna: 'nome', decrescente: false }, COLUNAS);
    expect(LINHAS[0].nome).toBe('Ótica Zeta');
  });

  it('busca: sem acento, sem maiúscula e CNPJ com ou sem pontuação', () => {
    expect(combinaComBusca('otica', ['Ótica Zeta'])).toBe(true);
    expect(combinaComBusca('11.222.333/0002', ['11222333000262'])).toBe(true);
    expect(combinaComBusca('333000262', ['11.222.333/0002-62'])).toBe(true);
    expect(combinaComBusca('kappa', ['Alfa', null, undefined])).toBe(false);
    expect(combinaComBusca('  ', ['qualquer'])).toBe(true);
  });
});
