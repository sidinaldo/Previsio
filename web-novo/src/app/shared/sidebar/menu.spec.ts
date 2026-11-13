import { GrupoMenu, MENU, gruposVisiveis, textoContador } from './menu';

describe('menu lateral', () => {
  const grupos: GrupoMenu[] = [
    {
      titulo: 'Financeiro',
      itens: [{ path: '/pages/painel', title: 'Painel', icone: 'dashboard' }]
    },
    {
      titulo: 'Administração',
      itens: [
        { path: '/pages/clientes', title: 'Clientes', icone: 'groups', somenteAdmin: true },
        { path: '/pages/usuarios', title: 'Usuários', icone: 'manage_accounts', somenteGestao: true }
      ]
    }
  ];

  function caminhos(lista: GrupoMenu[]): string[] {
    return lista.flatMap(g => g.itens.map(i => i.path));
  }

  it('admin vê todos os grupos e itens', () => {
    expect(gruposVisiveis(grupos, true, true).map(g => g.titulo)).toEqual(['Financeiro', 'Administração']);
    expect(caminhos(gruposVisiveis(grupos, true, true))).toEqual(['/pages/painel', '/pages/clientes', '/pages/usuarios']);
  });

  it('gestor vê os itens somenteGestao, mas não os somenteAdmin', () => {
    expect(caminhos(gruposVisiveis(grupos, false, true))).toEqual(['/pages/painel', '/pages/usuarios']);
  });

  it('usuário comum não vê os itens somenteAdmin nem somenteGestao, nem o grupo que ficou vazio', () => {
    const visiveis = gruposVisiveis(grupos, false, false);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro']);
    expect(caminhos(visiveis)).toEqual(['/pages/painel']);
  });

  it('lista vazia não quebra', () => {
    expect(gruposVisiveis(undefined, false, false)).toEqual([]);
  });

  it('menu real do gestor: Usuários em Configuração, sem o grupo Administração', () => {
    const visiveis = gruposVisiveis(MENU, false, true);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro', 'Notas', 'Configuração']);
    expect(visiveis[2].itens.map(i => i.path)).toEqual(['/pages/empresas', '/pages/usuarios']);
  });

  it('menu real do usuário comum: Financeiro, Notas e Configuração, sem Usuários nem Falhas', () => {
    const visiveis = gruposVisiveis(MENU, false, false);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro', 'Notas', 'Configuração']);
    expect(caminhos(visiveis)).toEqual([
      '/pages/painel',
      '/pages/contas-pagar',
      '/pages/contas-receber',
      '/pages/notas-recebidas',
      '/pages/notas-pendentes',
      '/pages/empresas'
    ]);
  });

  it('menu real do admin termina com o grupo Administração', () => {
    const visiveis = gruposVisiveis(MENU, true, true);
    expect(visiveis.map(g => g.titulo)).toEqual(['Financeiro', 'Notas', 'Configuração', 'Administração']);
    expect(visiveis[3].itens.map(i => i.path)).toEqual(['/pages/clientes', '/pages/saude', '/pages/falhas']);
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
