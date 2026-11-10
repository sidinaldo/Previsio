import { Component, DestroyRef, DOCUMENT, ElementRef, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import PerfectScrollbar from 'perfect-scrollbar';

import { SidebarComponent } from '../../shared/sidebar/sidebar.component';
import { NavbarComponent } from '../../shared/navbar/navbar.component';
import { FooterComponent } from '../../shared/footer/footer.component';
import { ClientesService } from '../../core/clientes.service';
import { SessaoService } from '../../core/sessao.service';

/**
 * Layout das telas internas: menu lateral, barra do topo e rodapé.
 * O admin não tem cliente próprio: escolhe na barra do topo qual cliente quer ver.
 * Sem nenhum cliente, só a tela Clientes abre (para cadastrar o primeiro).
 */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, SidebarComponent, NavbarComponent, FooterComponent],
  templateUrl: './admin-layout.component.html'
})
export class AdminLayoutComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly elemento = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sessao = inject(SessaoService);

  private readonly cadastroClientes = inject(ClientesService);

  private barras: PerfectScrollbar[] = [];
  private readonly urlAtual = signal(this.router.url);

  readonly clientes = this.cadastroClientes.lista;
  readonly erroClientes = this.cadastroClientes.erro;

  // As telas buscam os dados ao abrir; trocar a chave recria a tela com o novo cliente
  readonly chaveCliente = computed(() => [this.sessao.clienteId()]);

  readonly podeMostrarTelas = computed(() => {
    if (!this.sessao.ehAdmin()) {
      return true;
    }
    // Clientes e Usuários não dependem do cliente exibido: abrem mesmo sem nenhum escolhido
    // (é na tela Clientes que se cadastra o primeiro)
    const url = this.urlAtual();
    if (url.startsWith('/pages/clientes') || url.startsWith('/pages/usuarios')) {
      return true;
    }
    const clienteId = this.sessao.clienteId();
    return this.clientes().some(cliente => cliente.id === clienteId);
  });

  readonly semClientes = computed(() => {
    return this.cadastroClientes.carregada() && this.erroClientes() === '' && this.clientes().length === 0;
  });

  // Há clientes, mas nenhum escolhido (a escolha automática do primeiro não aconteceu)
  readonly precisaEscolherCliente = computed(() => {
    return this.cadastroClientes.carregada() && this.erroClientes() === '' && this.clientes().length > 0;
  });

  constructor() {
    if (this.sessao.ehAdmin()) {
      this.cadastroClientes.carregar();
    }

    afterNextRender(() => this.iniciarRolagem());

    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => {
        this.urlAtual.set(this.router.url);
        this.voltarAoTopo();
      });

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
