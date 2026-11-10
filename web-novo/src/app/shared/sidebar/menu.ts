// Itens do menu lateral (equivalente ao array ROUTES do template)

export interface ItemSubmenu {
  path: string;
  title: string;
  ab: string;
  // Escondido de quem não é admin (a rota também confere)
  somenteAdmin?: boolean;
}

export interface ItemMenu {
  path: string;
  title: string;
  type: 'link' | 'sub';
  icontype: string;
  collapse?: string;
  children?: ItemSubmenu[];
}

export const MENU: ItemMenu[] = [
  {
    path: '/pages',
    title: 'Previsio',
    type: 'sub',
    icontype: 'payments',
    collapse: 'previsio',
    children: [
      { path: 'painel', title: 'Painel', ab: 'PA' },
      { path: 'notas-pendentes', title: 'Notas pendentes', ab: 'NP' },
      { path: 'falhas', title: 'Falhas', ab: 'FA' },
      { path: 'empresas', title: 'Empresas', ab: 'EM' },
      { path: 'clientes', title: 'Clientes', ab: 'CL', somenteAdmin: true },
      { path: 'usuarios', title: 'Usuários', ab: 'US', somenteAdmin: true }
    ]
  }
];

/**
 * Itens do submenu que o usuário pode ver: quem não é admin não vê os marcados como somenteAdmin.
 */
export function itensVisiveis(filhos: ItemSubmenu[] | undefined, ehAdmin: boolean): ItemSubmenu[] {
  if (!filhos) {
    return [];
  }
  if (ehAdmin) {
    return filhos;
  }
  return filhos.filter(filho => !filho.somenteAdmin);
}

/**
 * Título da tela atual a partir da URL (usado na barra do topo).
 */
export function tituloDaRota(url: string): string {
  const caminho = url.split('?')[0];

  for (const item of MENU) {
    if (item.type === 'link' && item.path === caminho) {
      return item.title;
    }
    if (item.type === 'sub' && item.children) {
      for (const filho of item.children) {
        const completo = item.path + '/' + filho.path;
        if (caminho === completo || caminho.startsWith(completo + '/')) {
          return filho.title;
        }
      }
    }
  }
  return 'Previsio';
}
