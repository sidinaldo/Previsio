// Itens do menu lateral, separados em grupos, na ordem em que aparecem

export interface ItemMenu {
  path: string;
  title: string;
  // Nome do ícone do Material Icons
  icone: string;
  // Escondido de quem não é admin
  somenteAdmin?: boolean;
  // Só para o admin e o gestor da rede
  somenteGestao?: boolean;
  // Mostra ao lado o número de notas pendentes
  contaPendentes?: boolean;
}

export interface GrupoMenu {
  titulo: string;
  itens: ItemMenu[];
}

export const MENU: GrupoMenu[] = [
  {
    titulo: 'Financeiro',
    itens: [
      { path: '/pages/painel', title: 'Painel', icone: 'dashboard' },
      { path: '/pages/contas-pagar', title: 'Contas a pagar', icone: 'payments' },
      { path: '/pages/contas-receber', title: 'Contas a receber', icone: 'savings' }
    ]
  },
  {
    titulo: 'Notas',
    itens: [
      { path: '/pages/notas-recebidas', title: 'Notas recebidas', icone: 'receipt_long' },
      { path: '/pages/notas-pendentes', title: 'Notas pendentes', icone: 'pending_actions', contaPendentes: true }
    ]
  },
  {
    titulo: 'Configuração',
    itens: [
      { path: '/pages/empresas', title: 'Empresas', icone: 'store' },
      { path: '/pages/usuarios', title: 'Usuários', icone: 'manage_accounts', somenteGestao: true }
    ]
  },
  {
    // Telas da equipe Soft.IO
    titulo: 'Administração',
    itens: [
      { path: '/pages/clientes', title: 'Clientes', icone: 'groups', somenteAdmin: true },
      { path: '/pages/saude', title: 'Saúde', icone: 'monitor_heart', somenteAdmin: true },
      // Fora do menu do cliente, mas a tela continua aberta: o histórico da nota e o aviso de caixa com erro levam até ela
      { path: '/pages/falhas', title: 'Falhas', icone: 'error_outline', somenteAdmin: true }
    ]
  }
];

/**
 * Grupos que o usuário pode ver: os itens somenteAdmin só para o admin, os somenteGestao
 * para o admin e o gestor da rede; um grupo sem nenhum item visível não aparece.
 */
export function gruposVisiveis(grupos: GrupoMenu[] | undefined, ehAdmin: boolean, gereUsuarios: boolean): GrupoMenu[] {
  if (!grupos) {
    return [];
  }
  const visiveis: GrupoMenu[] = [];
  for (const grupo of grupos) {
    let itens = grupo.itens;
    if (!ehAdmin) {
      itens = itens.filter(item => !item.somenteAdmin);
    }
    if (!gereUsuarios) {
      itens = itens.filter(item => !item.somenteGestao);
    }
    if (itens.length > 0) {
      visiveis.push({ titulo: grupo.titulo, itens: itens });
    }
  }
  return visiveis;
}

/** Texto do número ao lado do item: nada quando é zero, "99+" acima de 99. */
export function textoContador(total: number | null): string {
  if (total === null || total <= 0) {
    return '';
  }
  if (total > 99) {
    return '99+';
  }
  return String(total);
}
