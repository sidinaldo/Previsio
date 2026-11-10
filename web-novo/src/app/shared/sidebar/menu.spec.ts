import { ItemSubmenu, MENU, itensVisiveis } from './menu';

describe('menu lateral', () => {
  const filhos: ItemSubmenu[] = [
    { path: 'painel', title: 'Painel', ab: 'PA' },
    { path: 'clientes', title: 'Clientes', ab: 'CL', somenteAdmin: true },
    { path: 'empresas', title: 'Empresas', ab: 'EM' },
    { path: 'usuarios', title: 'Usuários', ab: 'US', somenteAdmin: true }
  ];

  it('admin vê todos os itens, inclusive os somenteAdmin', () => {
    expect(itensVisiveis(filhos, true).map(f => f.path)).toEqual(['painel', 'clientes', 'empresas', 'usuarios']);
  });

  it('usuário de cliente não vê os itens somenteAdmin', () => {
    expect(itensVisiveis(filhos, false).map(f => f.path)).toEqual(['painel', 'empresas']);
  });

  it('item sem submenu não quebra', () => {
    expect(itensVisiveis(undefined, false)).toEqual([]);
  });

  it('Clientes e Usuários do menu real são somenteAdmin', () => {
    const doMenu = itensVisiveis(MENU[0].children, false).map(f => f.path);
    expect(doMenu).not.toContain('clientes');
    expect(doMenu).not.toContain('usuarios');
    expect(doMenu).toContain('empresas');
  });
});
