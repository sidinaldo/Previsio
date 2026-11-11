// Itens do menu lateral, na ordem em que aparecem

export interface ItemMenu {
  path: string;
  title: string;
  // Nome do ícone do Material Icons
  icone: string;
  // Escondido de quem não é admin (a rota também confere)
  somenteAdmin?: boolean;
}

export const MENU: ItemMenu[] = [
  { path: '/pages/painel', title: 'Painel', icone: 'dashboard' },
  { path: '/pages/notas-recebidas', title: 'Notas recebidas', icone: 'receipt_long' },
  { path: '/pages/notas-pendentes', title: 'Notas pendentes', icone: 'pending_actions' },
  { path: '/pages/falhas', title: 'Falhas', icone: 'error_outline' },
  { path: '/pages/empresas', title: 'Empresas', icone: 'store' },
  { path: '/pages/clientes', title: 'Clientes', icone: 'groups', somenteAdmin: true },
  { path: '/pages/usuarios', title: 'Usuários', icone: 'manage_accounts', somenteAdmin: true }
];

/**
 * Itens que o usuário pode ver: quem não é admin não vê os marcados como somenteAdmin.
 */
export function itensVisiveis(itens: ItemMenu[] | undefined, ehAdmin: boolean): ItemMenu[] {
  if (!itens) {
    return [];
  }
  if (ehAdmin) {
    return itens;
  }
  return itens.filter(item => !item.somenteAdmin);
}
