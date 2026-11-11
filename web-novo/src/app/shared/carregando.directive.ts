import { Directive, input } from '@angular/core';

/**
 * Mostra um spinner no botão enquanto a ação dele está em andamento e impede clicar de novo:
 *
 *   <button type="submit" class="btn btn-info" [pvCarregando]="salvando()" [disabled]="salvando()">Salvar</button>
 *
 * O [disabled] continua no próprio botão (o teclado também fica bloqueado); a diretiva
 * cuida do spinner, do clique e do aviso para leitores de tela.
 */
@Directive({
  selector: '[pvCarregando]',
  host: {
    '[class.pv-botao--carregando]': 'pvCarregando()',
    '[attr.aria-busy]': 'pvCarregando() ? "true" : null'
  }
})
export class CarregandoDirective {
  readonly pvCarregando = input(false);
}
