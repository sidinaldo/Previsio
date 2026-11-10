import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { DESCRICAO_STATUS, Nota } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';

@Component({
  selector: 'app-notas-pendentes',
  imports: [CurrencyPipe, DatePipe, SlicePipe, RouterLink],
  templateUrl: './notas-pendentes.component.html'
})
export class NotasPendentesComponent {
  private readonly previsio = inject(PrevisioService);

  // Quando usado dentro da tela da empresa, filtra por ela
  readonly empresaId = input<number | undefined>(undefined);
  readonly embutido = input(false);

  // Rodar a leitura de e-mails na hora é só do admin (o job passa por todos os clientes)
  readonly ehAdmin = inject(SessaoService).ehAdmin;

  readonly notas = signal<Nota[]>([]);
  readonly carregando = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');

  readonly valorTotal = computed(() => {
    let total = 0;
    for (const nota of this.notas()) {
      total = total + nota.valorTotal;
    }
    return total;
  });

  constructor() {
    // Carrega ao abrir a tela e sempre que a empresa filtrada mudar
    effect(() => {
      const empresa = this.empresaId();
      this.buscar(empresa);
    });
  }

  carregar(): void {
    this.buscar(this.empresaId());
  }

  descricao(status: string): string {
    const texto = DESCRICAO_STATUS[status];
    if (texto) {
      return texto;
    }
    return status;
  }

  // Notas com erro ou desconectadas têm tentativas registradas na tela de falhas
  temFalhas(nota: Nota): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado';
  }

  podeReprocessar(nota: Nota): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado' || nota.status === 'Recebida';
  }

  reprocessar(nota: Nota): void {
    this.previsio.reprocessarNota(nota.id).subscribe({
      next: () => {
        this.mensagem.set(`NF ${nota.numero} enviada para lançamento.`);
        setTimeout(() => this.carregar(), 3000);
      },
      error: () => this.erro.set(`Não foi possível reprocessar a NF ${nota.numero}.`)
    });
  }

  buscarEmailsAgora(): void {
    this.previsio.executarJob('leitura-email').subscribe({
      next: () => this.mensagem.set('Leitura dos e-mails iniciada. Atualize em alguns instantes.'),
      error: () => this.erro.set('Não foi possível iniciar a leitura dos e-mails.')
    });
  }

  private buscar(empresaId: number | undefined): void {
    this.carregando.set(true);
    this.erro.set('');
    this.previsio.obterNotasPendentes(empresaId).subscribe({
      next: notas => {
        this.notas.set(notas);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as notas pendentes.');
        this.carregando.set(false);
      }
    });
  }
}
