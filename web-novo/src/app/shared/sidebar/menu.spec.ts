import { ItemMenu, MENU, itensVisiveis } from './menu';

describe('menu lateral', () => {
  const itens: ItemMenu[] = [
    { path: '/pages/painel', title: 'Painel', icone: 'dashboard' },
    { path: '/pages/clientes', title: 'Clientes', icone: 'groups', somenteAdmin: true },
    { path: '/pages/empresas', title: 'Empresas', icone: 'store' },
    { path: '/pages/usuarios', title: 'Usuários', icone: 'manage_accounts', somenteAdmin: true }
  ];

  it('admin vê todos os itens, inclusive os somenteAdmin', () => {
    expect(itensVisiveis(itens, true).map(i => i.path))
      .toEqual(['/pages/painel', '/pages/clientes', '/pages/empresas', '/pages/usuarios']);
  });

  it('usuário de cliente não vê os itens somenteAdmin', () => {
    expect(itensVisiveis(itens, false).map(i => i.path)).toEqual(['/pages/painel', '/pages/empresas']);
  });

  it('lista vazia não quebra', () => {
    expect(itensVisiveis(undefined, false)).toEqual([]);
  });

  it('os itens ficam direto no menu, cada um com ícone, na ordem das telas', () => {
    expect(MENU.map(i => i.path)).toEqual([
      '/pages/painel',
      '/pages/notas-recebidas',
      '/pages/notas-pendentes',
      '/pages/falhas',
      '/pages/empresas',
      '/pages/clientes',
      '/pages/usuarios',
      '/pages/saude'
    ]);
    expect(MENU.map(i => i.icone)).toEqual([
      'dashboard', 'receipt_long', 'pending_actions', 'error_outline', 'store', 'groups', 'manage_accounts', 'monitor_heart'
    ]);
  });

  it('Clientes, Usuários e Saúde do menu real são somenteAdmin', () => {
    const doMenu = itensVisiveis(MENU, false).map(i => i.path);
    expect(doMenu).not.toContain('/pages/saude');
    expect(doMenu).not.toContain('/pages/clientes');
    expect(doMenu).not.toContain('/pages/usuarios');
    expect(doMenu).toContain('/pages/empresas');
  });
});
