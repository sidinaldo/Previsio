import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

import { ItemMenu, ItemSubmenu, MENU, itensVisiveis } from './menu';
import { SessaoService } from '../../core/sessao.service';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html'
})
export class SidebarComponent {
  private readonly router = inject(Router);
  private readonly sessao = inject(SessaoService);

  readonly itens = MENU;
  readonly usuario = this.sessao.usuario;
  readonly menuUsuarioAberto = signal(false);

  filhosVisiveis(item: ItemMenu): ItemSubmenu[] {
    return itensVisiveis(item.children, this.sessao.ehAdmin());
  }

  alternarMenuUsuario(evento: Event): void {
    evento.preventDefault();
    this.menuUsuarioAberto.set(!this.menuUsuarioAberto());
  }

  sair(evento: Event): void {
    evento.preventDefault();
    this.sessao.sair();
  }

  // Submenus abertos. Começa com o submenu da tela atual aberto.
  readonly abertos = signal<string[]>(this.submenusDaRotaAtual());

  estaAberto(item: ItemMenu): boolean {
    if (!item.collapse) {
      return false;
    }
    return this.abertos().includes(item.collapse);
  }

  alternar(item: ItemMenu, evento: Event): void {
    evento.preventDefault();
    if (!item.collapse) {
      return;
    }
    const nome = item.collapse;
    if (this.abertos().includes(nome)) {
      this.abertos.set(this.abertos().filter(n => n !== nome));
    } else {
      this.abertos.set([...this.abertos(), nome]);
    }
  }

  private submenusDaRotaAtual(): string[] {
    const url = this.router.url;
    const lista: string[] = [];
    for (const item of MENU) {
      if (item.type === 'sub' && item.collapse && url.startsWith(item.path)) {
        lista.push(item.collapse);
      }
    }
    // Na primeira carga a URL ainda pode estar vazia: deixa o Previsio aberto
    if (lista.length === 0 && MENU.length > 0 && MENU[0].collapse) {
      lista.push(MENU[0].collapse);
    }
    return lista;
  }
}
