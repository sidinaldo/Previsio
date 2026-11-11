import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { ResumoSemanal } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../carregando.directive';

export const DIAS_DA_SEMANA = ['Domingo', 'Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira', 'Sábado'];

/**
 * Card do resumo semanal por e-mail de uma rede: liga/desliga, dia e hora, e "Enviar para mim agora".
 * Usado na tela Empresas (pelo financeiro da rede e pelo admin) e no detalhe do cliente (admin).
 */
@Component({
  selector: 'app-resumo-semanal',
  imports: [FormsModule, DatePipe, CarregandoDirective],
  template: `
    <div class="card">
      <div class="pv-card__cabecalho">
        <div>
          <h2 class="pv-card__titulo">Resumo semanal por e-mail</h2>
          <p class="pv-card__descricao">
            Vencimentos dos próximos 7 dias, total dos 30, notas da semana e problemas em aberto.
          </p>
        </div>
      </div>
      @if (resumo(); as atual) {
        <form (ngSubmit)="salvar()">
          <div class="card-body pv-resumo">
            @if (mensagem()) {
              <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
            }
            @if (erro()) {
              <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
            }
            <div class="pv-resumo__linha">
              <div class="form-check">
                <label class="form-check-label">
                  <input class="form-check-input" type="checkbox" name="ativo" [(ngModel)]="ativo">
                  Enviar toda semana
                  <span class="form-check-sign"><span class="check"></span></span>
                </label>
              </div>
              <div class="pv-resumo__campo">
                <label class="pv-rotulo" for="resumo-dia">Dia</label>
                <span class="pv-campo-grupo bmd-form-group">
                  <select id="resumo-dia" class="pv-campo" name="dia" [(ngModel)]="dia" [disabled]="!ativo()">
                    @for (nome of dias; track $index) {
                      <option [ngValue]="$index">{{ nome }}</option>
                    }
                  </select>
                </span>
              </div>
              <div class="pv-resumo__campo">
                <label class="pv-rotulo" for="resumo-hora">Hora</label>
                <span class="pv-campo-grupo bmd-form-group">
                  <select id="resumo-hora" class="pv-campo" name="hora" [(ngModel)]="hora" [disabled]="!ativo()">
                    @for (h of horas; track h) {
                      <option [ngValue]="h">{{ rotuloHora(h) }}</option>
                    }
                  </select>
                </span>
              </div>
            </div>
            <p class="pv-formulario__ajuda">
              @if (atual.destinatarios === 0) {
                Ainda não há usuários ativos nesta rede: cadastre um em Usuários para alguém receber.
              } @else if (atual.destinatarios === 1) {
                Vai para 1 usuário da rede, no horário de Brasília.
              } @else {
                Vai para os {{ atual.destinatarios }} usuários da rede, no horário de Brasília.
              }
              @if (atual.ultimoEnvioEm) {
                Último envio em {{ atual.ultimoEnvioEm | date:'dd/MM/yyyy HH:mm' }}.
              }
            </p>
          </div>
          <div class="pv-formulario__acoes">
            <button type="submit" class="btn btn-info" [pvCarregando]="acao() === 'salvar'" [disabled]="acao() !== null">Salvar</button>
            <button type="button" class="btn btn-secundario" [pvCarregando]="acao() === 'teste'" [disabled]="acao() !== null"
                    (click)="enviarTeste()">
              <i class="material-icons" aria-hidden="true">send</i> Enviar para mim agora
            </button>
          </div>
        </form>
      } @else if (erro()) {
        <div class="card-body"><div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div></div>
      } @else {
        <div class="card-body"><p class="pv-formulario__ajuda">Carregando...</p></div>
      }
    </div>
  `
})
export class ResumoSemanalComponent {
  private readonly api = inject(PrevisioService);

  readonly clienteId = input.required<number>();

  readonly dias = DIAS_DA_SEMANA;
  readonly horas = Array.from({ length: 24 }, (_, i) => i);

  readonly resumo = signal<ResumoSemanal | null>(null);
  readonly ativo = signal(true);
  readonly dia = signal(1);
  readonly hora = signal(7);
  readonly acao = signal<'salvar' | 'teste' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    // Recarrega quando o admin troca de cliente
    effect(() => {
      const clienteId = this.clienteId();
      untracked(() => this.carregar(clienteId));
    });
  }

  rotuloHora(hora: number): string {
    return String(hora).padStart(2, '0') + 'h';
  }

  salvar(): void {
    this.limparMensagens();
    this.acao.set('salvar');
    this.api.alterarResumoSemanal(this.clienteId(), { ativo: this.ativo(), dia: this.dia(), hora: this.hora() }).subscribe({
      next: resumo => {
        this.preencher(resumo);
        this.acao.set(null);
        if (resumo.ativo) {
          this.mensagem.set('Resumo semanal salvo: ' + this.dias[resumo.dia].toLowerCase() + ', às ' + this.rotuloHora(resumo.hora) + '.');
        } else {
          this.mensagem.set('Resumo semanal desligado.');
        }
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não consegui salvar o resumo semanal. Tente de novo.'));
      }
    });
  }

  enviarTeste(): void {
    this.limparMensagens();
    this.acao.set('teste');
    this.api.enviarResumoSemanalTeste(this.clienteId()).subscribe({
      next: () => {
        this.acao.set(null);
        this.mensagem.set('Resumo enviado para o seu e-mail. Confira a caixa de entrada.');
      },
      error: erro => {
        this.acao.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não consegui enviar o resumo. Tente de novo.'));
      }
    });
  }

  private carregar(clienteId: number): void {
    this.limparMensagens();
    this.resumo.set(null);
    this.api.obterResumoSemanal(clienteId).subscribe({
      next: resumo => this.preencher(resumo),
      error: erro => this.erro.set(mensagemDaApi(erro, 'Não consegui carregar o resumo semanal.'))
    });
  }

  private preencher(resumo: ResumoSemanal): void {
    this.resumo.set(resumo);
    this.ativo.set(resumo.ativo);
    this.dia.set(resumo.dia);
    this.hora.set(resumo.hora);
  }

  private limparMensagens(): void {
    this.mensagem.set('');
    this.erro.set('');
  }
}
