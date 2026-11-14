import { Component, effect, inject, input, signal, untracked } from '@angular/core';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { ConferenciaBling } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';

/**
 * Card "Conferência com o Bling" da rede: antes de lançar, procura a conta já lançada à mão
 * (para não duplicar) e fecha sozinhas as notas pendentes encontradas no Bling.
 * Encontro parcial nunca lança: vira aviso para alguém conferir (isso não é configurável).
 */
@Component({
  selector: 'app-conferencia-bling',
  imports: [CarregandoDirective],
  template: `
    <section class="pv-secao">
      <div class="pv-secao__cabecalho">
        <h2 class="pv-card__titulo">Conferência com o Bling</h2>
        <p class="pv-card__descricao">Evita lançar de novo uma nota que alguém já lançou à mão no Bling.</p>
      </div>
      <div class="card pv-secao__card">
      @if (dados(); as d) {
        <div class="card-body pv-resumo">
          @if (mensagem()) {
            <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
          }
          @if (erro()) {
            <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
          }
          <div class="form-check">
            <label class="form-check-label">
              <input class="form-check-input" type="checkbox" [checked]="d.conferirAntesDeLancar" (change)="marcar('conferirAntesDeLancar', $event)">
              Conferir no Bling antes de lançar
              <span class="form-check-sign"><span class="check"></span></span>
            </label>
          </div>
          <p class="pv-formulario__ajuda">
            Antes de lançar, o Previsio procura as parcelas da nota no Bling. Achou todas: marca a nota como
            "Lançada manualmente". Achou só algumas: não lança nada e avisa para você conferir.
          </p>

          <div class="pv-config__dependentes">
          <div class="pv-resumo__linha">
            <div class="pv-resumo__campo">
              <label class="pv-rotulo" for="conferencia-criterio">Considerar a mesma conta quando bater</label>
              <span class="pv-campo-grupo bmd-form-group">
                <select id="conferencia-criterio" class="pv-campo pv-conferencia__criterio" (change)="mudarCriterio($event)">
                  <option value="simples" [selected]="!d.exigeNumeroNota">Fornecedor, vencimento e valor</option>
                  <option value="numero" [selected]="d.exigeNumeroNota">Fornecedor, vencimento, valor e nº da NF</option>
                </select>
              </span>
            </div>
            <div class="pv-resumo__campo">
              <label class="pv-rotulo" for="conferencia-valor">Diferença de valor aceita (R$)</label>
              <input id="conferencia-valor" type="number" min="0" max="5" step="0.01" class="pv-campo pv-campo--texto pv-alerta__numero"
                     [value]="d.toleranciaValor.toFixed(2)" (input)="mudarNumero('toleranciaValor', $event)">
            </div>
            <div class="pv-resumo__campo">
              <label class="pv-rotulo" for="conferencia-dias">Diferença de vencimento aceita (dias)</label>
              <input id="conferencia-dias" type="number" min="0" max="5" step="1" class="pv-campo pv-campo--texto pv-alerta__numero"
                     [value]="d.toleranciaDias" (input)="mudarNumero('toleranciaDias', $event)">
            </div>
          </div>
          </div>

          <div class="form-check">
            <label class="form-check-label">
              <input class="form-check-input" type="checkbox" [checked]="d.fecharPendentes" (change)="marcar('fecharPendentes', $event)">
              Fechar sozinhas as notas pendentes que já estão no Bling
              <span class="form-check-sign"><span class="check"></span></span>
            </label>
          </div>
          <p class="pv-formulario__ajuda">Confere a cada sincronização com o Bling. Desligado, a nota fica pendente até alguém clicar em "Já lancei no Bling".</p>
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
export class ConferenciaBlingComponent {
  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly dados = signal<ConferenciaBling | null>(null);
  readonly salvando = signal(false);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    effect(() => {
      const id = this.clienteId();
      untracked(() => this.carregar(id));
    });
  }

  marcar(campo: 'conferirAntesDeLancar' | 'fecharPendentes', evento: Event): void {
    const marcado = (evento.target as HTMLInputElement).checked;
    this.alterar(dados => ({ ...dados, [campo]: marcado }));
  }

  mudarCriterio(evento: Event): void {
    const exige = (evento.target as HTMLSelectElement).value === 'numero';
    this.alterar(dados => ({ ...dados, exigeNumeroNota: exige }));
  }

  mudarNumero(campo: 'toleranciaValor' | 'toleranciaDias', evento: Event): void {
    const valor = Number((evento.target as HTMLInputElement).value);
    this.alterar(dados => ({ ...dados, [campo]: valor }));
  }

  salvar(): void {
    const dados = this.dados();
    if (!dados) {
      return;
    }
    if (!(dados.toleranciaValor >= 0 && dados.toleranciaValor <= 5)) {
      this.erro.set('A diferença de valor vai de R$ 0,00 a R$ 5,00.');
      return;
    }
    if (!Number.isInteger(dados.toleranciaDias) || dados.toleranciaDias < 0 || dados.toleranciaDias > 5) {
      this.erro.set('A diferença de vencimento vai de 0 a 5 dias.');
      return;
    }
    this.mensagem.set('');
    this.erro.set('');
    this.salvando.set(true);
    this.api.alterarConferenciaBling(this.clienteId(), dados).subscribe({
      next: salvo => {
        this.dados.set(salvo);
        this.salvando.set(false);
        this.mensagem.set('Conferência com o Bling salva.');
      },
      error: erro => {
        this.salvando.set(false);
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar a conferência com o Bling.'));
      }
    });
  }

  private alterar(mudanca: (dados: ConferenciaBling) => ConferenciaBling): void {
    const atual = this.dados();
    if (atual) {
      this.dados.set(mudanca(atual));
    }
  }

  private carregar(clienteId: number): void {
    this.dados.set(null);
    this.mensagem.set('');
    this.erro.set('');
    this.api.obterConferenciaBling(clienteId).subscribe({
      next: dados => this.dados.set(dados),
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar a conferência com o Bling.'))
    });
  }
}
