import { Component, DestroyRef, DOCUMENT, ElementRef, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import PerfectScrollbar from 'perfect-scrollbar';

import { SidebarComponent } from '../../shared/sidebar/sidebar.component';
import { NavbarComponent } from '../../shared/navbar/navbar.component';
import { FooterComponent } from '../../shared/footer/footer.component';

/**
 * Layout das telas internas: menu lateral, barra do topo e rodapé.
 */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, SidebarComponent, NavbarComponent, FooterComponent],
  templateUrl: './admin-layout.component.html'
})
export class AdminLayoutComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly elemento = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);

  private barras: PerfectScrollbar[] = [];

  constructor() {
    afterNextRender(() => this.iniciarRolagem());

    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.voltarAoTopo());

    this.destroyRef.onDestroy(() => {
      for (const barra of this.barras) {
        barra.destroy();
      }
      this.document.documentElement.classList.remove('perfect-scrollbar-on', 'perfect-scrollbar-off');
    });
  }

  private iniciarRolagem(): void {
    const html = this.document.documentElement;
    const telaGrande = window.matchMedia('(min-width: 960px)').matches;

    // Mesmo critério do template: barra de rolagem customizada só em telas grandes e fora do Mac
    if (telaGrande && !this.ehMac()) {
      const painel = this.elemento.nativeElement.querySelector('.main-panel') as HTMLElement | null;
      const menu = this.elemento.nativeElement.querySelector('.sidebar .sidebar-wrapper') as HTMLElement | null;
      if (painel) {
        this.barras.push(new PerfectScrollbar(painel));
      }
      if (menu) {
        this.barras.push(new PerfectScrollbar(menu));
      }
      html.classList.add('perfect-scrollbar-on');
    } else {
      html.classList.add('perfect-scrollbar-off');
    }
  }

  private voltarAoTopo(): void {
    const painel = this.elemento.nativeElement.querySelector('.main-panel') as HTMLElement | null;
    if (painel) {
      painel.scrollTop = 0;
    }
    window.scrollTo(0, 0);
    for (const barra of this.barras) {
      barra.update();
    }
  }

  private ehMac(): boolean {
    const plataforma = navigator.platform.toUpperCase();
    return plataforma.indexOf('MAC') >= 0 || plataforma.indexOf('IPAD') >= 0;
  }
}
