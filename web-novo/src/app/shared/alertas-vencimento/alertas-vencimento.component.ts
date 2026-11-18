import { Component, effect, inject, input, signal, untracked } from '@angular/core';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { AlertasVencimento } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';
import { ErpService } from '../../core/erp.service';

/**
 * Card "Avisos de vencimento" da rede: depois de quantos dias de atraso a conta vencida vira aviso
 * (sininho e e-mails) e o e-mail diário com as contas que vencem hoje e amanhã.
 * "Enviar para mim agora" manda o aviso de hoje só para quem clicou (o admin não recebe o diário).
 */
@Component({
  selector: 'app-alertas-vencimento',
  imports: [CarregandoDirective],
  template: `
    <section class="pv-secao">
      <div class="pv-secao__cabecalho">
        <h2 class="pv-card__titulo">Avisos de vencimento</h2>
        <p class="pv-card__descricao">Contas em aberto no {{ erp() }}: as vencidas aparecem no sininho; as de hoje e amanhã vão por e-mail.</p>
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
          <div class="pv-config__duas-colunas">
          <div class="pv-config__grupo">
          <div class="pv-resumo__linha">
            <div class="pv-resumo__campo">
              <label class="pv-rotulo" for="alerta-dias">Avisar conta vencida depois de</label>
              <span class="pv-alerta__dias">
                <input id="alerta-dias" type="number" min="1" max="60" class="pv-campo pv-campo--texto pv-alerta__numero"
                       [value]="dias()" (input)="mudarDias($event)">
                <span>{{ dias() === 1 ? 'dia de atraso' : 'dias de atraso' }}</span>
              </span>
            </div>
          </div>
          <p class="pv-formulario__ajuda">
            Com 1, o aviso aparece no dia seguinte ao vencimento e some quando a conta é paga ou baixada no {{ erp() }}.
          </p>
          </div>
          <div class="pv-config__grupo">
          <div class="pv-resumo__linha">
            <div class="form-check">
              <label class="form-check-label">
                <input class="form-check-input" type="checkbox" [checked]="diarioAtivo()" (change)="mudarDiario($event)">
                E-mail diário com o que vence hoje e amanhã
                <span class="form-check-sign"><span class="check"></span></span>
              </label>
            </div>
            <div class="pv-resumo__campo">
              <label class="pv-rotulo" for="alerta-hora">Hora</label>
              <span class="pv-campo-grupo bmd-form-group">
                <select id="alerta-hora" class="pv-campo" [disabled]="!diarioAtivo()" (change)="mudarHora($event)">
                  @for (h of horas; track h) {
                    <option [value]="h" [selected]="h === hora()">{{ rotuloHora(h) }}</option>
                  }
                </select>
              </span>
            </div>
          </div>
          <p class="pv-formulario__ajuda">Vai para os usuários da rede, no horário de Brasília. Se não houver nada vencido nem vencendo, o e-mail não sai.
            "Enviar para mim agora" manda o aviso de hoje só para você, sem contar como o envio do dia.</p>
          </div>
          </div>
        </div>
        <div class="pv-formulario__acoes">
          <button type="button" class="btn btn-secundario" [pvCarregando]="acao() === 'teste'" [disabled]="acao() !== null"
                  (click)="enviarTeste()">
            <i class="material-icons" aria-hidden="true">send</i> Enviar para mim agora
          </button>
          <button type="button" class="btn btn-info" [pvCarregando]="acao() === 'salvar'" [disabled]="acao() !== null"
                  (click)="salvar()">Salvar</button>
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
export class AlertasVencimentoComponent {
  /** Nome do ERP da rede nos textos (vem da API). */
  protected readonly erp = inject(ErpService).nome;

  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly horas = Array.from({ length: 24 }, (_, i) => i);
  readonly carregado = signal(false);
  readonly dias = signal(1);
  readonly diarioAtivo = signal(true);
  readonly hora = signal(7);
  readonly acao = signal<'salvar' | 'teste' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    effect(() => {
      const id = this.clienteId();
      untracked(() => this.carregar(id));
    });
  }

  rotuloHora(hora: number): string {
    return String(hora).padStart(2, '0') + 'h';
  }

  mudarDias(evento: Event): void {
    this.dias.set(Number((evento.target as HTMLInputElement).value));
  }

  mudarDiario(evento: Event): void {
    this.diarioAtivo.set((evento.target as HTMLInputElement).checked);
  }

  mudarHora(evento: Event): void {
    this.hora.set(Number((evento.target as HTMLSelectElement).value));
  }

  salvar(): void {
    const dias = this.dias();
    if (!Number.isInteger(dias) || dias < 1 || dias > 60) {
      this.erro.set('Os dias de atraso vão de 1 a 60.');
      return;
    }
    this.mensagem.set('');
    this.erro.set('');
    this.acao.set('salvar');
    const dados: AlertasVencimento = { avisoVencidasDias: dias, alertaDiarioAtivo: this.diarioAtivo(), alertaDiarioHora: this.hora() };
    this.api.alterarAlertasVencimento(this.clienteId(), dados).subscribe({
      next: salvo => {
        this.preencher(salvo);
        this.acao.set(null);
        this.mensagem.set('Avisos de vencimento salvos.');
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar os avisos de vencimento.'));
      }
    });
  }

  /** O aviso de hoje (o que vence hoje, amanhã e as vencidas), só para quem clicou; não conta como o envio do dia. */
  enviarTeste(): void {
    this.mensagem.set('');
    this.erro.set('');
    this.acao.set('teste');
    this.api.enviarAlertaVencimentosTeste(this.clienteId()).subscribe({
      next: () => {
        this.acao.set(null);
        this.mensagem.set('Aviso de vencimentos enviado para o seu e-mail. Confira a caixa de entrada.');
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não consegui enviar o aviso. Tente de novo.'));
      }
    });
  }

  private carregar(clienteId: number): void {
    this.carregado.set(false);
    this.mensagem.set('');
    this.erro.set('');
    this.api.obterAlertasVencimento(clienteId).subscribe({
      next: dados => {
        this.preencher(dados);
        this.carregado.set(true);
      },
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar os avisos de vencimento.'))
    });
  }

  private preencher(dados: AlertasVencimento): void {
    this.dias.set(dados.avisoVencidasDias);
    this.diarioAtivo.set(dados.alertaDiarioAtivo);
    this.hora.set(dados.alertaDiarioHora);
  }
}
