import { Routes } from '@angular/router';

import { AdminLayoutComponent } from '../layouts/admin/admin-layout.component';
import { AuthLayoutComponent } from '../layouts/auth/auth-layout.component';
import { exigirLogin, somenteAdmin, somenteSemLogin } from '../core/autenticacao.guards';

/**
 * Todas as telas ficam em /pages/...
 * O layout é escolhido aqui: telas internas usam o admin (menu lateral) e exigem login,
 * a tela de login usa o de tela cheia.
 */
export const PAGES_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    // canActivateChild confere o login também ao trocar de tela dentro do layout
    canActivate: [exigirLogin],
    canActivateChild: [exigirLogin],
    children: [
      {
        path: 'painel',
        loadComponent: () => import('./painel/painel.component').then(m => m.PainelComponent)
      },
      {
        path: 'notas-recebidas',
        loadComponent: () => import('./notas-recebidas/notas-recebidas.component').then(m => m.NotasRecebidasComponent)
      },
      {
        path: 'notas-pendentes',
        loadComponent: () => import('./notas-pendentes/notas-pendentes.component').then(m => m.NotasPendentesComponent)
      },
      {
        path: 'falhas',
        loadComponent: () => import('./falhas/falhas.component').then(m => m.FalhasComponent)
      },
      {
        path: 'empresas',
        loadComponent: () => import('./empresas/empresas.component').then(m => m.EmpresasComponent)
      },
      {
        path: 'empresas/:id',
        loadComponent: () => import('./empresa-detalhe/empresa-detalhe.component').then(m => m.EmpresaDetalheComponent)
      },
      {
        path: 'clientes',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./clientes/clientes.component').then(m => m.ClientesComponent)
      },
      {
        path: 'clientes/:id',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./cliente-detalhe/cliente-detalhe.component').then(m => m.ClienteDetalheComponent)
      },
      {
        path: 'implantacao',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./implantacao/implantacao.component').then(m => m.ImplantacaoComponent)
      },
      {
        path: 'implantacao/:id',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./implantacao/implantacao.component').then(m => m.ImplantacaoComponent)
      },
      {
        path: 'usuarios',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./usuarios/usuarios.component').then(m => m.UsuariosComponent)
      },
      {
        path: 'saude',
        canActivate: [somenteAdmin],
        loadComponent: () => import('./saude/saude.component').then(m => m.SaudeComponent)
      }
    ]
  },
  {
    path: '',
    component: AuthLayoutComponent,
    children: [
      {
        path: 'login',
        canActivate: [somenteSemLogin],
        loadComponent: () => import('./login/login.component').then(m => m.LoginComponent)
      },
      {
        path: 'esqueci-senha',
        canActivate: [somenteSemLogin],
        loadComponent: () => import('./esqueci-senha/esqueci-senha.component').then(m => m.EsqueciSenhaComponent)
      },
      {
        // Sem guarda: o link do e-mail pode ser aberto num navegador que já está logado
        path: 'redefinir-senha',
        loadComponent: () => import('./redefinir-senha/redefinir-senha.component').then(m => m.RedefinirSenhaComponent)
      }
    ]
  }
];
