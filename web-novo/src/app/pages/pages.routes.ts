import { Routes } from '@angular/router';

import { AdminLayoutComponent } from '../layouts/admin/admin-layout.component';
import { AuthLayoutComponent } from '../layouts/auth/auth-layout.component';

/**
 * Todas as telas ficam em /pages/...
 * O layout é escolhido aqui: telas internas usam o admin (menu lateral),
 * a tela de login usa o de tela cheia.
 */
export const PAGES_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    children: [
      {
        path: 'painel',
        loadComponent: () => import('./painel/painel.component').then(m => m.PainelComponent)
      },
      {
        path: 'notas-pendentes',
        loadComponent: () => import('./notas-pendentes/notas-pendentes.component').then(m => m.NotasPendentesComponent)
      },
      {
        path: 'empresas',
        loadComponent: () => import('./empresas/empresas.component').then(m => m.EmpresasComponent)
      },
      {
        path: 'empresas/:id',
        loadComponent: () => import('./empresa-detalhe/empresa-detalhe.component').then(m => m.EmpresaDetalheComponent)
      }
    ]
  },
  {
    path: '',
    component: AuthLayoutComponent,
    children: [
      {
        path: 'login',
        loadComponent: () => import('./login/login.component').then(m => m.LoginComponent)
      }
    ]
  }
];
