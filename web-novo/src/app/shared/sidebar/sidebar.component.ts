import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { MENU, itensVisiveis } from './menu';
import { PERFIL_ADMIN } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';

/**
 * Menu lateral: logotipo, telas (direto no menu, cada uma com ícone) e,
 * no rodapé, o usuário logado com o botão Sair.
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html'
})
export class SidebarComponent {
  private readonly sessao = inject(SessaoService);

  readonly usuario = this.sessao.usuario;
  readonly itens = computed(() => itensVisiveis(MENU, this.sessao.ehAdmin()));

  descricaoPerfil(perfil: string): string {
    if (perfil === PERFIL_ADMIN) {
      return 'Administrador';
    }
    return 'Cliente';
  }

  sair(): void {
    this.sessao.sair();
  }
}
