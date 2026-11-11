import { Component, input } from '@angular/core';

/**
 * Símbolo do Previsio: o P cujo pé continua numa linha tracejada até um ponto
 * (do que já chegou até o vencimento). Decorativo: quem usa dá o nome acessível ao redor.
 *
 * - "principal": P azul e tracejado verde-azulado, para fundos claros.
 * - "negativo": P branco e tracejado claro, para o menu lateral e o painel da marca no login.
 */
@Component({
  selector: 'app-simbolo',
  host: { class: 'pv-simbolo', '[class.pv-simbolo--negativo]': "versao() === 'negativo'" },
  template: `
    <svg viewBox="0 0 48 48" width="100%" height="100%" aria-hidden="true" focusable="false">
      <rect class="pv-simbolo__letra" x="6" y="4" width="7" height="40" rx="3.5"/>
      <path class="pv-simbolo__curva" d="M9.5 7.5H23a8.5 8.5 0 0 1 0 17H9.5" fill="none" stroke-width="7" stroke-linejoin="round"/>
      <path class="pv-simbolo__previsto" d="M18.5 40.5H35" fill="none" stroke-width="3.5" stroke-linecap="round" stroke-dasharray="3.5 5"/>
      <circle class="pv-simbolo__vencimento" cx="41.5" cy="40.5" r="3.5"/>
    </svg>
  `
})
export class SimboloComponent {
  readonly versao = input<'principal' | 'negativo'>('principal');
}
