import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ClientesService } from '../../core/clientes.service';
import {
  ETAPAS_IMPLANTACAO,
  EtapaImplantacao,
  SituacaoImplantacao,
  TITULO_ETAPA,
  etapaConcluida,
  podeAbrirEtapa,
  primeiraEtapaPendente,
  proximaEtapa
} from '../../core/implantacao';
import { Cliente } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { SessaoService } from '../../core/sessao.service';
import { EtapaBlingComponent } from './etapa-bling.component';
import { EtapaEmpresasComponent } from './etapa-empresas.component';
import { EtapaLeituraComponent } from './etapa-leitura.component';
import { EtapaRedeComponent } from './etapa-rede.component';
import { EtapaUsuarioComponent } from './etapa-usuario.component';

/**
 * Assistente de implantação de um cliente novo (só admin), em cinco etapas:
 * rede e e-mail → empresas → conectar Bling → usuário do cliente → primeira leitura.
 *
 * Não grava em que etapa parou: ao abrir /pages/implantacao/:id olha o que já está
 * cadastrado e continua da primeira etapa que falta.
 */
@Component({
  selector: 'app-implantacao',
  imports: [RouterLink, EtapaRedeComponent, EtapaEmpresasComponent, EtapaBlingComponent, EtapaUsuarioComponent, EtapaLeituraComponent],
  templateUrl: './implantacao.component.html'
})
export class ImplantacaoComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly clientes = inject(ClientesService);
  private readonly sessao = inject(SessaoService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly etapas = ETAPAS_IMPLANTACAO;
  readonly titulos = TITULO_ETAPA;

  readonly situacao = signal<SituacaoImplantacao>({ cliente: null, empresas: [], usuarios: [], notasLidas: 0 });
  readonly etapaAtual = signal<EtapaImplantacao>('rede');
  readonly carregando = signal(false);
  readonly erro = signal('');

  readonly cliente = computed(() => this.situacao().cliente);
  readonly concluidas = computed(() => {
    const situacao = this.situacao();
    let total = 0;
    for (const etapa of ETAPAS_IMPLANTACAO) {
      if (etapaConcluida(situacao, etapa)) {
        total++;
      }
    }
    return total;
  });

  private readonly clienteId: number | null;

  constructor() {
    const id = Number(this.rota.snapshot.paramMap.get('id'));
    if (Number.isInteger(id) && id > 0) {
      this.clienteId = id;
      // As telas de notas e falhas usam o cliente exibido; a primeira leitura mostra as deste
      this.sessao.escolherCliente(id);
      this.carregar(true);
    } else {
      this.clienteId = null;
    }
  }

  concluida(etapa: EtapaImplantacao): boolean {
    return etapaConcluida(this.situacao(), etapa);
  }

  podeAbrir(etapa: EtapaImplantacao): boolean {
    return podeAbrirEtapa(this.situacao(), etapa);
  }

  numero(etapa: EtapaImplantacao): number {
    return ETAPAS_IMPLANTACAO.indexOf(etapa) + 1;
  }

  abrir(etapa: EtapaImplantacao): void {
    if (this.podeAbrir(etapa)) {
      this.etapaAtual.set(etapa);
    }
  }

  continuar(): void {
    this.etapaAtual.set(proximaEtapa(this.etapaAtual()));
  }

  /** A rede acabou de ser cadastrada: o endereço passa a ter o id, para poder retomar depois. */
  redeCadastrada(cliente: Cliente): void {
    this.clientes.carregar();
    this.router.navigate(['/pages/implantacao', cliente.id]);
  }

  /** Busca de novo o que está cadastrado. Na abertura, vai direto para a primeira etapa que falta. */
  carregar(irParaPendente: boolean = false): void {
    if (this.clienteId === null) {
      return;
    }
    this.carregando.set(true);
    forkJoin({
      cliente: this.previsio.obterCliente(this.clienteId),
      empresas: this.previsio.listarEmpresasDoCliente(this.clienteId),
      usuarios: this.previsio.listarUsuarios(),
      notas: this.previsio.obterNotas({ pagina: 1 })
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: dados => {
        const situacao: SituacaoImplantacao = {
          cliente: dados.cliente,
          empresas: dados.empresas,
          usuarios: dados.usuarios,
          notasLidas: dados.notas.total
        };
        this.situacao.set(situacao);
        this.carregando.set(false);
        this.erro.set('');
        if (irParaPendente) {
          this.etapaAtual.set(primeiraEtapaPendente(situacao));
        }
      },
      error: () => {
        this.carregando.set(false);
        this.erro.set('Não foi possível carregar o cadastro deste cliente. Verifique se a API está no ar.');
      }
    });
  }
}
