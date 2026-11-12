import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { mensagemDoDownload, salvarArquivo } from '../../core/download';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { EmailIgnorado } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';

/** Rede para o filtro (vem da tela Saúde). */
export interface RedeDoFiltro {
  clienteId: number;
  nome: string;
}

/**
 * E-mails com anexo que a leitura examinou e não viraram nota nem pendência, com o motivo.
 * O admin confere o PDF e, se for nota, manda tratar como nota em PDF.
 */
@Component({
  selector: 'app-emails-ignorados',
  imports: [DatePipe, RouterLink, CarregandoDirective],
  template: `
    <div class="card">
      <div class="pv-card__cabecalho">
        <div>
          <h2 class="pv-card__titulo">E-mails ignorados</h2>
          <p class="pv-card__descricao">E-mails com anexo que não viraram nota, com o motivo. Saem sozinhos depois do prazo (30 dias, por padrão).</p>
        </div>
        <div class="pv-ignorados__filtros">
          <span class="pv-campo-grupo bmd-form-group">
            <select class="pv-campo" aria-label="Rede" (change)="mudarRede($event)">
              <option value="" [selected]="clienteId() === null">Todas as redes</option>
              @for (rede of redes(); track rede.clienteId) {
                <option [value]="rede.clienteId" [selected]="clienteId() === rede.clienteId">{{ rede.nome }}</option>
              }
            </select>
          </span>
          <span class="pv-campo-grupo bmd-form-group">
            <select class="pv-campo" aria-label="Período" (change)="mudarDias($event)">
              <option value="1" [selected]="dias() === 1">Último dia</option>
              <option value="7" [selected]="dias() === 7">Últimos 7 dias</option>
              <option value="30" [selected]="dias() === 30">Últimos 30 dias</option>
            </select>
          </span>
        </div>
      </div>

      @if (mensagem()) {
        <div class="card-body pb-0"><div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div></div>
      }
      @if (erro()) {
        <div class="card-body pb-0"><div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div></div>
      }

      @if (carregado() && emails().length === 0) {
        <div class="pv-vazio pv-vazio--compacto">
          <span class="pv-vazio__icone" aria-hidden="true"><i class="material-icons">mark_email_read</i></span>
          <p class="pv-vazio__titulo">Nenhum e-mail ignorado no período.</p>
          <p class="pv-vazio__texto">Todo e-mail com anexo lido pelo Previsio virou nota ou pendência.</p>
        </div>
      } @else if (emails().length > 0) {
        <div class="table-responsive">
          <table class="table">
            <thead>
              <tr>
                <th>Lido em</th>
                <th>Rede e caixa</th>
                <th>E-mail</th>
                <th>Motivo</th>
                <th class="text-right">Ações</th>
              </tr>
            </thead>
            <tbody>
              @for (email of emails(); track email.id) {
                <tr>
                  <td class="pv-numero">{{ email.lidoEm | date:'dd/MM/yyyy HH:mm' }}</td>
                  <td>
                    {{ email.clienteNome }}
                    <span class="pv-tabela__auxiliar">{{ email.caixa }}</span>
                  </td>
                  <td>
                    <strong>{{ email.assunto || '(sem assunto)' }}</strong>
                    <span class="pv-tabela__auxiliar">{{ email.remetente }}</span>
                    <span class="pv-tabela__auxiliar pv-tabela__auxiliar--quebra">Anexos: {{ email.anexos }}</span>
                  </td>
                  <td>
                    @if (email.numeroNota) {
                      <span class="badge badge-info">Nota já existe</span>
                    }
                    {{ email.motivo }}
                    @if (email.tratadoEm) {
                      <span class="pv-tabela__auxiliar pv-tabela__auxiliar--quebra">
                        <span class="badge badge-success">Tratado</span>
                        {{ email.tratadoEm | date:'dd/MM HH:mm' }} por {{ email.tratadoPor }}: {{ email.resultadoTratamento }}
                      </span>
                    }
                  </td>
                  <td class="text-right">
                    @if (email.temPdf || email.numeroNota) {
                      <div class="pv-acoes-linha">
                        @if (email.temPdf) {
                          <button type="button" class="btn btn-sm btn-link" (click)="verPdf(email)">Ver o PDF</button>
                        }
                        @if (email.numeroNota) {
                          <a class="btn btn-sm btn-secundario" [routerLink]="['/pages/notas-recebidas']"
                             [queryParams]="{ busca: email.numeroNota }">Ver a nota</a>
                        } @else if (email.podeTratar) {
                          <button type="button" class="btn btn-sm btn-info" [pvCarregando]="tratandoId() === email.id"
                                  [disabled]="tratandoId() !== null" (click)="tratar(email)">Tratar como nota em PDF</button>
                        }
                      </div>
                    } @else {
                      —
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (emails().length >= 200) {
          <p class="pv-card__rodape">Mostrando os 200 mais recentes. Escolha uma rede ou um período menor.</p>
        }
      } @else if (!carregado()) {
        <div class="card-body"><p class="pv-formulario__ajuda">Carregando...</p></div>
      }
    </div>
  `
})
export class EmailsIgnoradosComponent {
  private readonly api = inject(PrevisioService);

  readonly redes = input<RedeDoFiltro[]>([]);

  readonly clienteId = signal<number | null>(null);
  readonly dias = signal(7);
  readonly emails = signal<EmailIgnorado[]>([]);
  readonly carregado = signal(false);
  readonly tratandoId = signal<number | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  constructor() {
    effect(() => {
      const cliente = this.clienteId();
      const dias = this.dias();
      untracked(() => this.carregar(cliente, dias));
    });
  }

  mudarRede(evento: Event): void {
    const valor = (evento.target as HTMLSelectElement).value;
    this.clienteId.set(valor === '' ? null : Number(valor));
  }

  mudarDias(evento: Event): void {
    this.dias.set(Number((evento.target as HTMLSelectElement).value));
  }

  verPdf(email: EmailIgnorado): void {
    this.erro.set('');
    this.api.baixarPdfEmailIgnorado(email.id).subscribe({
      next: resposta => salvarArquivo(resposta, 'email-' + email.id + '.pdf'),
      error: async erro => this.erro.set(await mensagemDoDownload(erro, 'Não foi possível baixar o PDF.'))
    });
  }

  tratar(email: EmailIgnorado): void {
    this.mensagem.set('');
    this.erro.set('');
    this.tratandoId.set(email.id);
    this.api.tratarEmailIgnoradoComoNota(email.id).subscribe({
      next: resposta => {
        this.tratandoId.set(null);
        this.mensagem.set(resposta.mensagem);
        this.emails.update(lista => lista.map(item => item.id === email.id ? resposta.email : item));
      },
      error: erro => {
        this.tratandoId.set(null);
        this.erro.set(mensagemDaApi(erro, 'Não foi possível tratar o e-mail como nota.'));
      }
    });
  }

  private carregar(clienteId: number | null, dias: number): void {
    this.carregado.set(false);
    this.erro.set('');
    this.api.obterEmailsIgnorados(clienteId, dias).subscribe({
      next: lista => {
        this.emails.set(lista);
        this.carregado.set(true);
      },
      error: erro => {
        this.emails.set([]);
        this.carregado.set(true);
        this.erro.set(mensagemDaApi(erro, 'Não foi possível carregar os e-mails ignorados.'));
      }
    });
  }
}
