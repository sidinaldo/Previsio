import { Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { PrevisioService } from '../../core/previsio.service';
import { TotalEmpresa, VencimentoDia } from '../../core/previsio.models';
import { NotasPendentesComponent } from '../notas-pendentes/notas-pendentes.component';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';

@Component({
  selector: 'app-empresa-detalhe',
  imports: [RouterLink, CurrencyPipe, DatePipe, DecimalPipe, NotasPendentesComponent, PaginacaoComponent],
  templateUrl: './empresa-detalhe.component.html'
})
export class EmpresaDetalheComponent {
  private readonly previsio = inject(PrevisioService);
  private readonly rota = inject(ActivatedRoute);

  readonly dias = 30;
  readonly empresaId = Number(this.rota.snapshot.paramMap.get('id'));
  readonly empresa = signal<TotalEmpresa | null>(null);
  readonly vencimentos = signal<VencimentoDia[]>([]);

  // Paginação da tabela (a API devolve a lista inteira)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly vencimentosDaPagina = computed(() => paginar(this.vencimentos(), this.pagina(), this.tamanhoPagina()));

  constructor() {
    this.previsio.obterPainel(this.dias).subscribe(resumo => {
      const encontrada = resumo.empresas.find(e => e.empresaId === this.empresaId);
      if (encontrada) {
        this.empresa.set(encontrada);
      } else {
        this.empresa.set(null);
      }
    });

    this.previsio.obterVencimentos(this.empresaId, this.dias).subscribe(lista => this.vencimentos.set(lista));
  }
}
