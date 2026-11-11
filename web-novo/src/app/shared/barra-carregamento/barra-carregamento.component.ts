import { Component, inject } from '@angular/core';

import { CarregamentoService } from '../../core/carregamento.service';

/**
 * Barra fina animada no topo da tela enquanto alguma chamada à API está em andamento.
 * Só aparece depois de um instante, para respostas rápidas não piscarem a tela.
 */
@Component({
  selector: 'app-barra-carregamento',
  template: `
    @if (carregamento.carregando()) {
      <div class="pv-barra-carregamento" role="progressbar" aria-label="Carregando">
        <span class="pv-barra-carregamento__faixa"></span>
      </div>
    }
  `
})
export class BarraCarregamentoComponent {
  readonly carregamento = inject(CarregamentoService);
}
