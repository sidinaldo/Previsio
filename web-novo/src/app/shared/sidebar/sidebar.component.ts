import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Subscription, filter, timer } from 'rxjs';

import { MENU, gruposVisiveis, textoContador } from './menu';
import { PERFIL_ADMIN } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { SimboloComponent } from '../simbolo/simbolo.component';

// De quanto em quanto tempo o número de notas pendentes é atualizado
const INTERVALO_CONTADOR_MS = 2 * 60 * 1000;

/**
 * Menu lateral: logotipo, telas separadas em grupos (cada uma com ícone; Notas pendentes
 * com o número de notas para resolver) e, no rodapé, o usuário logado com o botão Sair.
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, SimboloComponent],
  templateUrl: './sidebar.component.html'
})
export class SidebarComponent implements OnInit {
  private readonly sessao = inject(SessaoService);
  private readonly api = inject(PrevisioService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private consulta: Subscription | null = null;
  private readonly totalPendentes = signal<number | null>(null);

  readonly usuario = this.sessao.usuario;
  readonly grupos = computed(() => gruposVisiveis(MENU, this.sessao.ehAdmin()));
  readonly contador = computed(() => textoContador(this.totalPendentes()));

  constructor() {
    // O admin pode trocar o cliente exibido: o número passa a ser o do novo cliente
    effect(() => {
      this.sessao.clienteId();
      this.atualizarContador();
    });
  }

  ngOnInit(): void {
    timer(INTERVALO_CONTADOR_MS, INTERVALO_CONTADOR_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.atualizarContador());

    // Ao trocar de tela (por exemplo, depois de resolver uma nota)
    this.router.events
      .pipe(filter(evento => evento instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.atualizarContador());

    this.destroyRef.onDestroy(() => this.cancelarConsulta());
  }

  descricaoPerfil(perfil: string): string {
    if (perfil === PERFIL_ADMIN) {
      return 'Administrador';
    }
    return 'Cliente';
  }

  sair(): void {
    this.sessao.sair();
  }

  private atualizarContador(): void {
    if (this.sessao.clienteId() === null) {
      this.totalPendentes.set(null);
      return;
    }
    this.cancelarConsulta();
    this.consulta = this.api.obterTotalPendentes().subscribe({
      next: total => this.totalPendentes.set(total),
      // Sem rede ou API fora: mantém o número que tinha e tenta de novo depois
      error: () => undefined
    });
  }

  private cancelarConsulta(): void {
    if (this.consulta) {
      this.consulta.unsubscribe();
      this.consulta = null;
    }
  }
}
