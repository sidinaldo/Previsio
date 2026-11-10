import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'pages/painel',
    pathMatch: 'full'
  },
  {
    // Todas as telas; cada uma define seu layout em pages.routes.ts
    path: 'pages',
    loadChildren: () => import('./pages/pages.routes').then(m => m.PAGES_ROUTES)
  },
  {
    path: '**',
    redirectTo: 'pages/painel'
  }
];
