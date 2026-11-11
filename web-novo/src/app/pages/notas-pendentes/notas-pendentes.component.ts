import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe, SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { aguardarFimDoJob } from '../../core/aguardar-job';
import { PrevisioService } from '../../core/previsio.service';
import { DESCRICAO_STATUS, Nota, SITUACOES_COM_PROBLEMA } from '../../core/previsio.models';
import { SessaoService } from '../../core/sessao.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { MenuAcoesComponent } from '../../shared/menu-acoes/menu-acoes.component';
import { ResolucaoManualComponent } from '../../shared/resolucao-manual/resolucao-manual.component';
import { classeDaSituacao } from '../../core/filtro-notas';

@Component({
  selector: 'app-notas-pendentes',
  imports: [CurrencyPipe, DatePipe, SlicePipe, RouterLink, MenuAcoesComponent, ResolucaoManualComponent, CarregandoDirective],
  templateUrl: './notas-pendentes.component.html'
})
export class NotasPendentesComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly destroyRef = inject(DestroyRef);

  // Quando usado dentro da tela da empresa, filtra por ela
  readonly empresaId = input<number | undefined>(undefined);
  readonly embutido = input(false);

  // Rodar a leitura de e-mails na hora é só do admin (o job passa por todos os clientes)
  readonly ehAdmin = inject(SessaoService).ehAdmin;

  readonly notas = signal<Nota[]>([]);
  readonly carregando = signal(false);
  readonly erro = signal('');
  readonly mensagem = signal('');
  // Leitura dos e-mails rodando: o botão fica desabilitado até ela terminar
  readonly buscandoEmails = signal(false);

  // Nota com a confirmação de "lançada manualmente no Bling" aberta
  readonly resolvendoId = signal<number | null>(null);

  // Nota sendo enviada para um novo lançamento
  readonly reprocessandoId = signal<number | null>(null);

  // Colunas da tabela (a de empresa some quando a tela já está filtrada por uma)
  readonly colunas = computed(() => (this.empresaId() ? 7 : 8));

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

  // Cor do badge da situação (a mesma da tela de notas recebidas)
  classeStatus(status: string): string {
    return classeDaSituacao(status);
  }

  // Notas com erro ou desconectadas têm tentativas registradas na tela de falhas
  temFalhas(nota: Nota): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado';
  }

  /** Notas com problema podem ser marcadas como lançadas manualmente no Bling. */
  podeMarcarManual(nota: Nota): boolean {
    return SITUACOES_COM_PROBLEMA.includes(nota.status);
  }

  abrirResolucao(nota: Nota): void {
    this.resolvendoId.set(nota.id);
  }

  fecharResolucao(): void {
    this.resolvendoId.set(null);
  }

  aoResolver(nota: Nota): void {
    this.resolvendoId.set(null);
    this.erro.set('');
    this.mensagem.set(`NF ${nota.numero} marcada como lançada manualmente no Bling.`);
    this.carregar();
  }

  podeReprocessar(nota: Nota): boolean {
    return nota.status === 'Erro' || nota.status === 'BlingDesconectado' || nota.status === 'Recebida';
  }

  reprocessar(nota: Nota): void {
    this.erro.set('');
    this.reprocessandoId.set(nota.id);
    this.previsio.reprocessarNota(nota.id).subscribe({
      next: () => {
        this.reprocessandoId.set(null);
        this.mensagem.set(`NF ${nota.numero} enviada para lançamento.`);
        setTimeout(() => this.carregar(), 3000);
      },
      error: () => {
        this.reprocessandoId.set(null);
        this.erro.set(`Não foi possível reprocessar a NF ${nota.numero}.`);
      }
    });
  }


  /** Roda a leitura dos e-mails e atualiza a lista sozinha quando ela termina. */
  buscarEmailsAgora(): void {
    this.erro.set('');
    this.buscandoEmails.set(true);
    this.mensagem.set('Buscando os e-mails... a lista atualiza sozinha quando terminar.');
    this.previsio.executarJob('leitura-email').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.esperarLeitura(),
      error: () => {
        this.buscandoEmails.set(false);
        this.mensagem.set('');
        this.erro.set('Não foi possível iniciar a leitura dos e-mails.');
      }
    });
  }

  private esperarLeitura(): void {
    aguardarFimDoJob(() => this.previsio.situacaoJob('leitura-email'))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.buscandoEmails.set(false);
          this.mensagem.set('Leitura dos e-mails concluída e lista atualizada. As notas novas são lançadas no Bling em até 10 minutos.');
          this.carregar();
        },
        error: () => {
          // Demorou demais (ou a API não respondeu): mostra o que já chegou
          this.buscandoEmails.set(false);
          this.mensagem.set('A leitura dos e-mails ainda não terminou. Clique em Atualizar em alguns instantes.');
          this.carregar();
        }
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
