import { Component, computed, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { Cliente, seloContrato } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';

/**
 * Teste grátis e contratação da rede (admin): situação, "Marcar como contratado" e "Prorrogar 7 dias".
 */
@Component({
  selector: 'app-contrato-cliente',
  imports: [DatePipe, CarregandoDirective],
  template: `
    <div class="card">
      <div class="pv-card__cabecalho">
        <div>
          <h2 class="pv-card__titulo">Contrato</h2>
          <p class="pv-card__descricao">
            @if (cliente().contrato === 'EmTeste') {
              Teste grátis até {{ cliente().testeUltimoDia | date:'dd/MM/yyyy' }}. Depois disso o Previsio para de ler as notas e lançar no Bling.
            } @else if (cliente().contrato === 'TesteEncerrado') {
              O teste grátis acabou em {{ cliente().testeUltimoDia | date:'dd/MM/yyyy' }}: a rede não está sendo processada.
            } @else {
              Rede contratada: processada sem prazo.
            }
          </p>
        </div>
        <span [class]="selo().classe">{{ selo().texto }}</span>
      </div>
      @if (mensagem() || erro()) {
        <div class="card-body pb-0">
          @if (mensagem()) {
            <div class="alert alert-success" role="status"><span>{{ mensagem() }}</span></div>
          }
          @if (erro()) {
            <div class="alert alert-danger" role="alert"><span>{{ erro() }}</span></div>
          }
        </div>
      }
      <div class="pv-formulario__acoes">
        @if (cliente().contrato !== 'Contratado') {
          <button type="button" class="btn btn-info" [pvCarregando]="acao() === 'contratar'" [disabled]="acao() !== null"
                  (click)="contratar()">
            <i class="material-icons" aria-hidden="true">handshake</i> Marcar como contratado
          </button>
        }
        <button type="button" class="btn btn-secundario" [pvCarregando]="acao() === 'prorrogar'" [disabled]="acao() !== null"
                (click)="prorrogar()">
          @if (cliente().contrato === 'Contratado') {
            Colocar em teste por 7 dias
          } @else {
            Prorrogar o teste em 7 dias
          }
        </button>
      </div>
    </div>
  `
})
export class ContratoClienteComponent {
  private readonly api = inject(PrevisioService);

  readonly cliente = input.required<Cliente>();
  readonly alterado = output<Cliente>();

  readonly selo = computed(() => seloContrato(this.cliente().contrato, this.cliente().diasRestantesTeste));
  readonly acao = signal<'contratar' | 'prorrogar' | null>(null);
  readonly mensagem = signal('');
  readonly erro = signal('');

  contratar(): void {
    this.iniciar('contratar');
    this.api.contratarCliente(this.cliente().id).subscribe({
      next: cliente => this.concluir(cliente, 'Rede marcada como contratada. O processamento segue sem prazo.'),
      error: erro => this.falhar(erro, 'Não consegui marcar como contratado.')
    });
  }

  prorrogar(): void {
    this.iniciar('prorrogar');
    this.api.prorrogarTeste(this.cliente().id, 7).subscribe({
      next: cliente => this.concluir(cliente, 'Teste grátis prorrogado: vai até ' + formatarData(cliente.testeUltimoDia) + '.'),
      error: erro => this.falhar(erro, 'Não consegui prorrogar o teste.')
    });
  }

  private iniciar(acao: 'contratar' | 'prorrogar'): void {
    this.acao.set(acao);
    this.mensagem.set('');
    this.erro.set('');
  }

  private concluir(cliente: Cliente, texto: string): void {
    this.acao.set(null);
    this.mensagem.set(texto);
    this.alterado.emit(cliente);
  }

  private falhar(erro: unknown, padrao: string): void {
    this.acao.set(null);
    this.erro.set(mensagemDaApi(erro, padrao));
  }
}

// "2026-11-25T00:00:00" → "25/11/2026"
function formatarData(data: string | null | undefined): string {
  if (!data) {
    return '';
  }
  const partes = data.substring(0, 10).split('-');
  return partes[2] + '/' + partes[1] + '/' + partes[0];
}
