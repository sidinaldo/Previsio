import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ClientesService } from '../../core/clientes.service';
import { Cliente } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';

/**
 * Faixa no topo durante o teste grátis: quantos dias faltam e, quando acaba, que o Previsio parou.
 * O admin vê a do cliente escolhido (com link para contratar); o usuário do cliente, a da própria rede.
 */
@Component({
  selector: 'app-faixa-teste',
  imports: [DatePipe, RouterLink],
  template: `
    @if (cliente(); as c) {
      @if (c.contrato === 'EmTeste') {
        <div class="pv-faixa-teste" [class.pv-faixa-teste--fim]="(c.diasRestantesTeste ?? 0) <= 3" role="status">
          <i class="material-icons" aria-hidden="true">hourglass_top</i>
          <span>
            @if (c.diasRestantesTeste === 1) {
              <strong>Último dia do teste grátis</strong>
            } @else {
              <strong>Teste grátis: faltam {{ c.diasRestantesTeste }} dias</strong>
            }
            (até {{ c.testeUltimoDia | date:'dd/MM/yyyy' }}).
            @if (ehAdmin()) {
              {{ c.nome }} ainda não contratou.
            } @else {
              Depois disso o Previsio para de ler as notas e lançar no Bling. Para continuar, fale com a Soft.IO.
            }
          </span>
          @if (ehAdmin()) {
            <a class="pv-faixa-teste__link" [routerLink]="['/pages/clientes', c.id]">Contratar ou prorrogar</a>
          }
        </div>
      } @else if (c.contrato === 'TesteEncerrado') {
        <div class="pv-faixa-teste pv-faixa-teste--encerrado" role="alert">
          <i class="material-icons" aria-hidden="true">pause_circle</i>
          <span>
            <strong>O teste grátis acabou em {{ c.testeUltimoDia | date:'dd/MM/yyyy' }}.</strong>
            O Previsio parou de ler as notas do e-mail e de lançar no Bling; os dados continuam aqui.
            @if (!ehAdmin()) {
              Para continuar, fale com a Soft.IO.
            }
          </span>
          @if (ehAdmin()) {
            <a class="pv-faixa-teste__link" [routerLink]="['/pages/clientes', c.id]">Contratar ou prorrogar</a>
          }
        </div>
      }
    }
  `
})
export class FaixaTesteComponent {
  private readonly api = inject(PrevisioService);
  private readonly sessao = inject(SessaoService);
  private readonly clientes = inject(ClientesService);

  readonly ehAdmin = this.sessao.ehAdmin;
  private readonly daRede = signal<Cliente | null>(null);

  // Admin: o cliente escolhido na lista que o layout já carregou. Usuário do cliente: busca a própria rede.
  readonly cliente = computed(() => {
    if (this.ehAdmin()) {
      const id = this.sessao.clienteId();
      return this.clientes.lista().find(cliente => cliente.id === id) ?? null;
    }
    return this.daRede();
  });

  constructor() {
    effect(() => {
      const id = this.sessao.clienteId();
      const admin = this.ehAdmin();
      untracked(() => {
        if (admin || id === null) {
          return;
        }
        this.api.obterCliente(id).subscribe({
          next: cliente => this.daRede.set(cliente),
          error: () => this.daRede.set(null)
        });
      });
    });
  }
}
