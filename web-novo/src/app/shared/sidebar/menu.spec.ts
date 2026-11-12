import { GrupoMenu, MENU, gruposVisiveis, textoContador } from './menu';

describe('menu lateral', () => {
  const grupos: GrupoMenu[] = [
    {
      titulo: 'Financeiro',
      itens: [{ path: '/pages/painel', title: 'Painel', icone: 'dashboard' }]
    },
    {
      titulo: 'Soft.IO',
      itens: [
        { path: '/pages/clientes', title: 'Clientes', icone: 'groups', somenteAdmin: true },
        { path: '/pages/usuarios', title: 'Usuários', icone: 'manage_accounts', somenteAdmin: true }
      ]
    }
  ];

  function caminhos(lista: GrupoMenu[]): string[] {
    return lista.flatMap(g => g.itens.map(i => i.path));
  }

  it('admin vê todos os grupos e itens', () => {
    expect(gruposVisiveis(grupos, true).map(g => g.titulo)).toEqual(['Financeiro', 'Soft.IO']);
    expect(caminhos(gruposVisiveis(grupos, true))).toEqual(['/pages/painel', '/pages/clientes', '/pages/usuarios']);
  });

  it('usuário de cliente não vê os itens somenteAdmin nem o grupo que ficou vazio', () => {
    const visiveis = gruposVisiveis(grupos, false);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro']);
    expect(caminhos(visiveis)).toEqual(['/pages/painel']);
  });

  it('lista vazia não quebra', () => {
    expect(gruposVisiveis(undefined, false)).toEqual([]);
  });

  it('menu real do cliente: Financeiro, Notas e Configuração, sem Falhas', () => {
    const visiveis = gruposVisiveis(MENU, false);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro', 'Notas', 'Configuração']);
    expect(caminhos(visiveis)).toEqual([
      '/pages/painel',
      '/pages/contas-pagar',
      '/pages/notas-recebidas',
      '/pages/notas-pendentes',
      '/pages/empresas'
    ]);
  });

  it('menu real do admin termina com o grupo Soft.IO', () => {
    const visiveis = gruposVisiveis(MENU, true);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro', 'Notas', 'Configuração', 'Soft.IO']);
    expect(visiveis[3].itens.map(i => i.path)).toEqual(['/pages/clientes', '/pages/usuarios', '/pages/saude', '/pages/falhas']);
  });

  it('só Notas pendentes mostra o número', () => {
    const comNumero = MENU.flatMap(g => g.itens).filter(i => i.contaPendentes).map(i => i.path);
    expect(comNumero).toEqual(['/pages/notas-pendentes']);
  });

  it('número do menu: vazio em zero, 99+ acima de 99', () => {
    expect(textoContador(null)).toBe('');
    expect(textoContador(0)).toBe('');
    expect(textoContador(3)).toBe('3');
    expect(textoContador(150)).toBe('99+');
  });
});
