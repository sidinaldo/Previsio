import { Component, DestroyRef, DOCUMENT, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

import { Cliente } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { SininhoComponent } from '../sininho/sininho.component';

/**
 * Barra do topo do layout admin.
 * Reproduz o comportamento do template sem jQuery, num botão só (à esquerda):
 * - no computador, recolhe o menu lateral (classe "sidebar-mini" no body)
 * - no celular, abre o menu por cima da tela (classe "nav-open" + camada escura)
 * Para o admin, mostra também a escolha do cliente exibido. À direita fica o sininho de avisos.
 */
@Component({
  selector: 'app-navbar',
  imports: [FormsModule, SininhoComponent],
  templateUrl: './navbar.component.html'
})
export class NavbarComponent {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly sessao = inject(SessaoService);

  readonly clientes = input<Cliente[]>([]);
  readonly ehAdmin = this.sessao.ehAdmin;
  readonly usuario = this.sessao.usuario;
  readonly clienteId = this.sessao.clienteId;

  readonly menuCelularAberto = signal(false);

  private camada: HTMLElement | null = null;

  constructor() {
    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.fecharMenuCelular());
  }

  trocarCliente(clienteId: number): void {
    this.sessao.escolherCliente(Number(clienteId));
  }

  alternarMenu(): void {
    // Mesmo ponto de corte do template para o menu virar gaveta (991px)
    if (window.matchMedia('(max-width: 991px)').matches) {
      this.alternarMenuCelular();
    } else {
      this.minimizarMenu();
    }
  }

  minimizarMenu(): void {
    const body = this.document.body;
    if (body.classList.contains('sidebar-mini')) {
      body.classList.remove('sidebar-mini');
    } else {
      body.classList.add('sidebar-mini');
    }
    // O template simula um resize para os gráficos se ajustarem à nova largura
    this.simularRedimensionamento();
  }

  alternarMenuCelular(): void {
    if (this.menuCelularAberto()) {
      this.fecharMenuCelular();
    } else {
      this.abrirMenuCelular();
    }
  }

  private abrirMenuCelular(): void {
    this.document.body.classList.add('nav-open');
    this.menuCelularAberto.set(true);

    const camada = this.document.createElement('div');
    camada.className = 'close-layer';
    camada.addEventListener('click', () => this.fecharMenuCelular());

    const painel = this.document.querySelector('.main-panel');
    if (painel) {
      painel.appendChild(camada);
    }
    setTimeout(() => camada.classList.add('visible'), 100);
    this.camada = camada;
  }

  private fecharMenuCelular(): void {
    this.document.body.classList.remove('nav-open');
    this.menuCelularAberto.set(false);

    if (this.camada) {
      const camada = this.camada;
      camada.classList.remove('visible');
      setTimeout(() => camada.remove(), 400);
      this.camada = null;
    }
  }

  private simularRedimensionamento(): void {
    const intervalo = setInterval(() => window.dispatchEvent(new Event('resize')), 180);
    setTimeout(() => clearInterval(intervalo), 1000);
  }
}
