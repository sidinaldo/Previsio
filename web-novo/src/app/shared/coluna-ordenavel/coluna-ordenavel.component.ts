import { Component, computed, input, output } from '@angular/core';

import { Ordenacao } from '../../core/ordenacao';

/**
 * Cabeçalho de coluna que ordena a tabela ao clicar (1º clique crescente, 2º decrescente):
 *
 *   <th pvColuna="valor" [ordenacao]="ordenacao()" (ordenar)="ordenarPor($event)">Valor</th>
 *
 * A seta mostra a coluna e a direção atuais; leitores de tela recebem o aria-sort.
 */
@Component({
  selector: 'th[pvColuna]',
  host: {
    class: 'pv-th-ordenavel',
    '[attr.aria-sort]': 'ariaSort()',
    '[class.pv-coluna--ativa]': 'ativa()'
  },
  template: `
    <button type="button" class="pv-coluna" (click)="ordenar.emit(pvColuna())" [title]="titulo()">
      <ng-content />
      <i class="material-icons pv-coluna__seta" aria-hidden="true">{{ seta() }}</i>
    </button>
  `
})
export class ColunaOrdenavelComponent {
  readonly pvColuna = input.required<string>();
  readonly ordenacao = input<Ordenacao | null>(null);
  readonly ordenar = output<string>();

  readonly ativa = computed(() => {
    const atual = this.ordenacao();
    return atual !== null && atual.coluna === this.pvColuna();
  });

  readonly seta = computed(() => {
    const atual = this.ordenacao();
    if (!this.ativa() || atual === null) {
      return 'unfold_more';
    }
    if (atual.decrescente) {
      return 'arrow_downward';
    }
    return 'arrow_upward';
  });

  readonly ariaSort = computed(() => {
    const atual = this.ordenacao();
    if (!this.ativa() || atual === null) {
      return 'none';
    }
    if (atual.decrescente) {
      return 'descending';
    }
    return 'ascending';
  });

  readonly titulo = computed(() => {
    const atual = this.ordenacao();
    if (this.ativa() && atual !== null && !atual.decrescente) {
      return 'Ordenar do maior para o menor';
    }
    return 'Ordenar do menor para o maior';
  });
}
