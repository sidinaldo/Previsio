import { Component, effect, inject, input, signal, untracked } from '@angular/core';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';

/**
 * Card "Fluxo de caixa" da rede: liga ou desliga as contas a receber do Bling no painel
 * (entradas × saídas). Usado em Empresas (gestor e admin) e no detalhe do cliente (admin).
 */
@Component({
  selector: 'app-fluxo-caixa',
  imports: [CarregandoDirective],
  template: `
    <section class="pv-secao">
      <div class="pv-secao__cabecalho">
        <h2 class="pv-card__titulo">Fluxo de caixa</h2>
        <p class="pv-card__descricao">Traz do Bling as contas a receber e mostra no painel o que entra, o que sai e o saldo de cada semana.</p>
      </div>
      <div class="card pv-secao__card">
      @if (carregado()) {
        <div class="card-body pv-resumo">
          @if (mensagem()) {
            <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
          }
          @if (erro()) {
            <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
          }
          <div class="form-check">
            <label class="form-check-label">
              <input id="fluxo-caixa-ativo" class="form-check-input" type="checkbox" [checked]="ativo()" (change)="mudar($event)">
              Mostrar entradas × saídas no painel
              <span class="form-check-sign"><span class="check"></span></span>
            </label>
          </div>
          <p class="pv-formulario__ajuda">
            Deixe desligado se as vendas da rede não são lançadas no Bling: as entradas apareceriam zeradas.
            As contas a receber vencidas e não recebidas ficam fora do saldo, num card à parte.
          </p>
        </div>
        <div class="pv-formulario__acoes">
          <button type="button" class="btn btn-info" [pvCarregando]="salvando()" [disabled]="salvando()" (click)="salvar()">Salvar</button>
        </div>
      } @else if (erro()) {
        <div class="card-body"><div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div></div>
      } @else {
        <div class="card-body"><p class="pv-formulario__ajuda">Carregando...</p></div>
      }
    </div>
    </section>
  `
})
export class FluxoCaixaComponent {
  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly carregado = signal(false);
  readonly ativo = signal(true);
  readonly salvando = signal(false);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    effect(() => {
      const id = this.clienteId();
      untracked(() => this.carregar(id));
    });
  }

  mudar(evento: Event): void {
    this.ativo.set((evento.target as HTMLInputElement).checked);
  }

  salvar(): void {
    this.mensagem.set('');
    this.erro.set('');
    this.salvando.set(true);
    this.api.alterarFluxoCaixa(this.clienteId(), { ativo: this.ativo() }).subscribe({
      next: salvo => {
        this.ativo.set(salvo.ativo);
        this.salvando.set(false);
        if (salvo.ativo) {
          this.mensagem.set('Fluxo de caixa ligado. As contas a receber chegam na próxima sincronização com o Bling.');
        } else {
          this.mensagem.set('Fluxo de caixa desligado. O painel mostra só as saídas.');
        }
      },
      error: erro => {
        this.salvando.set(false);
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar o fluxo de caixa.'));
      }
    });
  }

  private carregar(clienteId: number): void {
    this.carregado.set(false);
    this.mensagem.set('');
    this.erro.set('');
    this.api.obterFluxoCaixa(clienteId).subscribe({
      next: dados => {
        this.ativo.set(dados.ativo);
        this.carregado.set(true);
      },
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar o fluxo de caixa.'))
    });
  }
}
