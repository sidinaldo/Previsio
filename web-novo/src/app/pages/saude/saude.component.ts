import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { formatarCnpj } from '../../core/implantacao';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { combinaComBusca } from '../../core/ordenacao';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { Saude, SaudeJob, SaudeRede, SituacaoJobAgendado, seloContrato } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { BuscaComponent } from '../../shared/busca/busca.component';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';
import { EmailsIgnoradosComponent, RedeDoFiltro } from './emails-ignorados.component';

const TEXTO_JOB: Record<SituacaoJobAgendado, string> = {
  Ok: 'Em dia',
  Falhou: 'Falhou',
  TentandoDeNovo: 'Tentando de novo',
  Rodando: 'Rodando',
  NuncaRodou: 'Ainda não rodou'
};

const CLASSE_JOB: Record<SituacaoJobAgendado, string> = {
  Ok: 'badge-success',
  Falhou: 'badge-danger',
  TentandoDeNovo: 'badge-warning',
  Rodando: 'badge-info',
  NuncaRodou: ''
};

/**
 * Saúde das integrações (admin): os jobs agendados e, por rede, a caixa de e-mail,
 * o Bling de cada empresa e a fila de notas. Rede com aviso aberto pede atenção e vem primeiro.
 */
@Component({
  selector: 'app-saude',
  imports: [DatePipe, FormsModule, RouterLink, BuscaComponent, CarregandoDirective, PaginacaoComponent, EmailsIgnoradosComponent],
  templateUrl: './saude.component.html'
})
export class SaudeComponent implements OnInit {
  private readonly api = inject(PrevisioService);

  readonly formatarCnpj = formatarCnpj;

  readonly saude = signal<Saude | null>(null);
  readonly carregando = signal(false);
  readonly erro = signal('');

  readonly busca = signal('');
  readonly soAtencao = signal(false);
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);

  readonly redesFiltradas = computed(() => {
    const saude = this.saude();
    if (!saude) {
      return [];
    }
    const termo = this.busca();
    const soAtencao = this.soAtencao();
    return saude.redes.filter(rede => {
      if (soAtencao && !rede.atencao) {
        return false;
      }
      const campos: string[] = [rede.nome];
      for (const empresa of rede.empresas) {
        campos.push(empresa.nome, empresa.cnpj);
      }
      return combinaComBusca(termo, campos);
    });
  });
  // Redes para o filtro dos e-mails ignorados, em ordem alfabética
  readonly redesDoFiltro = computed<RedeDoFiltro[]>(() => {
    const saude = this.saude();
    if (!saude) {
      return [];
    }
    return saude.redes
      .map(rede => ({ clienteId: rede.clienteId, nome: rede.nome }))
      .sort((a, b) => a.nome.localeCompare(b.nome));
  });
  readonly redesDaPagina = computed(() => paginar(this.redesFiltradas(), this.pagina(), this.tamanhoPagina()));
  readonly jobsComProblema = computed(() => {
    const saude = this.saude();
    if (!saude) {
      return 0;
    }
    return saude.jobs.filter(job => job.situacao === 'Falhou' || job.situacao === 'TentandoDeNovo').length;
  });

  ngOnInit(): void {
    this.atualizar();
  }

  atualizar(): void {
    this.carregando.set(true);
    this.erro.set('');
    this.api.obterSaude().subscribe({
      next: saude => {
        this.saude.set(saude);
        this.carregando.set(false);
      },
      error: erro => {
        this.carregando.set(false);
        this.erro.set(mensagemDaApi(erro, 'Não consegui carregar a saúde das integrações. Tente de novo.'));
      }
    });
  }

  mudarBusca(texto: string): void {
    this.busca.set(texto);
    this.pagina.set(1);
  }

  mudarSoAtencao(valor: boolean): void {
    this.soAtencao.set(valor);
    this.pagina.set(1);
  }

  selo(rede: SaudeRede): { texto: string; classe: string } {
    return seloContrato(rede.contrato, rede.diasRestantesTeste);
  }

  textoJob(job: SaudeJob): string {
    return TEXTO_JOB[job.situacao] || job.situacao;
  }

  classeJob(job: SaudeJob): string {
    return CLASSE_JOB[job.situacao] || '';
  }

  /** "1 aviso", "3 avisos" */
  quantidade(numero: number, singular: string, plural: string): string {
    if (numero === 1) {
      return '1 ' + singular;
    }
    return numero + ' ' + plural;
  }
}
