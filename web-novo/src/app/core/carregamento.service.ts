import { Injectable, computed, signal } from '@angular/core';

/**
 * Quantas chamadas à API estão em andamento. A barra do topo aparece enquanto houver alguma.
 */
@Injectable({ providedIn: 'root' })
export class CarregamentoService {
  private readonly pendentes = signal(0);

  readonly carregando = computed(() => this.pendentes() > 0);

  iniciar(): void {
    this.pendentes.update(total => total + 1);
  }

  terminar(): void {
    this.pendentes.update(total => Math.max(0, total - 1));
  }
}
