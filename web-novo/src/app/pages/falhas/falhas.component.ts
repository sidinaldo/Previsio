import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { mensagemDaApi } from '../../core/mensagem-erro';
import { PrevisioService } from '../../core/previsio.service';
import { DESCRICAO_ETAPA, Empresa, Falha, FiltroFalhas } from '../../core/previsio.models';
import { totalDePaginas } from '../../core/filtro-falhas';

/**
 * Histórico das falhas: por que falhou, qual endereço tentou, qual nota, empresa e valor.
 * Abre filtrado por nota ou empresa quando vem de outra tela (?notaId= / ?empresaId=).
 */
@Component({
  selector: 'app-falhas',
  imports: [FormsModule, CurrencyPipe, DatePipe, SlicePipe],
  templateUrl: './falhas.component.html'
})
export class FalhasComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly etapas = Object.keys(DESCRICAO_ETAPA);
  readonly empresas = signal<Empresa[]>([]);

  // Filtros
  readonly empresaId = signal<number | null>(null);
  readonly notaId = signal<number | null>(null);
  readonly etapa = signal('');
  readonly de = signal('');
  readonly ate = signal('');
  readonly pagina = signal(1);

  readonly falhas = signal<Falha[]>([]);
  readonly total = signal(0);
  readonly tamanhoPagina = signal(50);
  readonly carregando = signal(false);
  readonly erro = signal('');

  // Linha com os detalhes abertos
  readonly abertaId = signal<number | null>(null);

  readonly totalPaginas = computed(() => totalDePaginas(this.total(), this.tamanhoPagina()));

  // Número da nota filtrada, para o aviso no topo
  readonly notaFiltrada = computed(() => {
    const notaId = this.notaId();
    if (notaId === null) {
      return null;
    }
    const comNumero = this.falhas().find(f => f.notaFiscalId === notaId && f.numeroNota);
    if (comNumero) {
      return comNumero.numeroNota;
    }
    return String(notaId);
  });

  constructor() {
    const parametros = this.rota.snapshot.queryParamMap;
    this.notaId.set(lerNumero(parametros.get('notaId')));
    this.empresaId.set(lerNumero(parametros.get('empresaId')));

    this.previsio.listarEmpresas().subscribe({
      next: empresas => this.empresas.set(empresas),
      error: () => this.empresas.set([])
    });

    this.carregar();
  }

  descricaoEtapa(etapa: string): string {
    const texto = DESCRICAO_ETAPA[etapa];
    if (texto) {
      return texto;
    }
    return etapa;
  }

  filtrar(): void {
    this.pagina.set(1);
    this.carregar();
  }

  limpar(): void {
    this.empresaId.set(null);
    this.notaId.set(null);
    this.etapa.set('');
    this.de.set('');
    this.ate.set('');
    this.pagina.set(1);
    // Tira ?notaId= / ?empresaId= da URL para o filtro não voltar ao atualizar a página
    this.router.navigate([], { relativeTo: this.rota, queryParams: {} });
    this.carregar();
  }

  irParaPagina(pagina: number): void {
    if (pagina < 1 || pagina > this.totalPaginas()) {
      return;
    }
    this.pagina.set(pagina);
    this.carregar();
  }

  alternarDetalhes(falha: Falha): void {
    if (this.abertaId() === falha.id) {
      this.abertaId.set(null);
    } else {
      this.abertaId.set(falha.id);
    }
  }

  carregar(): void {
    if (this.de() && this.ate() && this.ate() < this.de()) {
      this.erro.set('A data final deve ser igual ou posterior à inicial.');
      return;
    }

    const filtro: FiltroFalhas = {
      empresaId: this.empresaId(),
      notaId: this.notaId(),
      etapa: this.etapa(),
      de: this.de(),
      ate: this.ate(),
      pagina: this.pagina()
    };

    this.carregando.set(true);
    this.erro.set('');
    this.abertaId.set(null);

    this.previsio.obterFalhas(filtro).subscribe({
      next: resposta => {
        this.falhas.set(resposta.itens);
        this.total.set(resposta.total);
        this.tamanhoPagina.set(resposta.tamanhoPagina);
        this.carregando.set(false);
      },
      error: erro => {
        this.erro.set(mensagemDaApi(erro, 'Não foi possível carregar as falhas.'));
        this.carregando.set(false);
      }
    });
  }
}

function lerNumero(texto: string | null): number | null {
  if (!texto) {
    return null;
  }
  const numero = Number(texto);
  if (Number.isInteger(numero) && numero > 0) {
    return numero;
  }
  return null;
}
