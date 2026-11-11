import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';

import { formatarCnpj } from '../../core/implantacao';
import { abrirJanelaBling, ouvirRetornoBling } from '../../core/janela-bling';
import { mensagemDaApi } from '../../core/mensagem-erro';
import { DESCRICAO_ORIGEM_APLICATIVO, Empresa } from '../../core/previsio.models';
import { PrevisioService } from '../../core/previsio.service';
import { CarregandoDirective } from '../../shared/carregando.directive';
import { TAMANHO_PAGINA_PADRAO, paginar } from '../../core/paginacao';
import { PaginacaoComponent } from '../../shared/paginacao/paginacao.component';

/**
 * Etapa 3: autorizar o Previsio na conta Bling de cada empresa, na mesma janela usada
 * na tela Empresas. Quando a autorização termina, a lista atualiza sozinha.
 */
@Component({
  selector: 'app-etapa-bling',
  imports: [CarregandoDirective, PaginacaoComponent],
  templateUrl: './etapa-bling.component.html'
})
export class EtapaBlingComponent {
  private readonly previsio = inject(PrevisioService);

  readonly empresas = input.required<Empresa[]>();
  readonly alterou = output<void>();
  readonly continuar = output<void>();

  readonly origens = DESCRICAO_ORIGEM_APLICATIVO;
  readonly formatarCnpj = formatarCnpj;

  readonly processandoId = signal<number | null>(null);
  readonly erro = signal('');
  readonly mensagem = signal('');

  readonly ativas = computed(() => this.empresas().filter(empresa => empresa.ativo));
  readonly pendentes = computed(() => this.ativas().filter(empresa => !empresa.blingConectado).length);

  // Paginação da tabela (a lista inteira vem da API)
  readonly pagina = signal(1);
  readonly tamanhoPagina = signal(TAMANHO_PAGINA_PADRAO);
  readonly ativasDaPagina = computed(() => paginar(this.ativas(), this.pagina(), this.tamanhoPagina()));

  constructor() {
    const pararDeOuvir = ouvirRetornoBling(retorno => {
      this.erro.set('');
      this.mensagem.set('');
      if (retorno === 'conectado') {
        this.mensagem.set('Conta Bling conectada.');
      } else if (retorno === 'expirado') {
        this.erro.set('O link de conexão expirou. Clique em "Conectar Bling" novamente.');
      } else {
        this.erro.set('Não foi possível conectar ao Bling. Tente novamente.');
      }
      this.alterou.emit();
    });
    inject(DestroyRef).onDestroy(pararDeOuvir);
  }

  conectar(empresa: Empresa): void {
    this.erro.set('');
    this.mensagem.set('');
    // Abre a janela já no clique: depois da chamada à API o navegador bloquearia
    const janela = abrirJanelaBling();
    this.processandoId.set(empresa.id);
    this.previsio.obterUrlConexaoBling(empresa.id).subscribe({
      next: resposta => {
        this.processandoId.set(null);
        if (janela !== null && !janela.closed) {
          janela.location.href = resposta.url;
          janela.focus();
          this.mensagem.set('Conclua o login e a autorização de ' + empresa.nome
            + ' na janela do Bling. Esta tela atualiza sozinha quando terminar.');
        } else {
          this.erro.set('O navegador bloqueou a janela do Bling. Permita pop-ups para o Previsio e clique de novo.');
        }
      },
      error: resposta => {
        this.processandoId.set(null);
        if (janela !== null) {
          janela.close();
        }
        this.erro.set(mensagemDaApi(resposta, 'Não foi possível gerar o link de conexão.'));
      }
    });
  }
}
