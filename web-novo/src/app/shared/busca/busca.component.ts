import { Component, input, model } from '@angular/core';

let proximoId = 1;

/**
 * Caixa de busca das listas, com a lupa e o botão de limpar:
 *
 *   <app-busca [(valor)]="busca" placeholder="Buscar por nome ou CNPJ" />
 *
 * Só guarda o texto; quem usa decide como filtrar (na hora, na própria tela,
 * ou ao enviar o formulário de filtros, nas listas paginadas pela API).
 */
@Component({
  selector: 'app-busca',
  // bmd-form-group: impede o Bootstrap Material Design de embrulhar o input numa div (que limita a largura)
  host: { class: 'pv-busca bmd-form-group' },
  template: `
    <label class="sr-only" [for]="id">{{ placeholder() }}</label>
    <i class="material-icons pv-busca__lupa" aria-hidden="true">search</i>
    <input [id]="id" type="search" class="pv-busca__campo" autocomplete="off" [placeholder]="placeholder()"
           [value]="valor()" (input)="valor.set($any($event.target).value)">
    @if (valor()) {
      <button type="button" class="pv-busca__limpar" (click)="valor.set('')" aria-label="Limpar busca" title="Limpar busca">
        <i class="material-icons" aria-hidden="true">close</i>
      </button>
    }
  `
})
export class BuscaComponent {
  readonly valor = model('');
  readonly placeholder = input('Buscar');

  readonly id = 'pv-busca-' + proximoId++;
}
