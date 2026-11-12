import { Component, computed, input, model } from '@angular/core';

import { AJUDA_OUTRO, PROVEDOR_OUTRO, PROVEDORES_EMAIL, provedorDoHost, provedorPorId } from '../../core/provedores-email';

/**
 * Escolha do provedor de e-mail da caixa que recebe as notas: preenche servidor, porta e SSL,
 * mostra como gerar a senha e, para quem não pode usar IMAP com senha (Microsoft), o encaminhamento.
 *
 *   <app-provedor-email [(host)]="imapHost" [(porta)]="imapPorta" [(ssl)]="imapSsl" [destino]="emailDaRede" />
 */
@Component({
  selector: 'app-provedor-email',
  host: { class: 'pv-provedor' },
  template: `
    <div class="pv-resumo__campo pv-provedor__escolha">
      <label class="pv-rotulo" [for]="idCampo()">Provedor do e-mail</label>
      <span class="pv-campo-grupo bmd-form-group">
        <select [id]="idCampo()" class="pv-campo" (change)="escolher($event)">
          @for (provedor of provedores; track provedor.id) {
            <option [value]="provedor.id" [selected]="provedor.id === escolhido()">{{ provedor.nome }}</option>
          }
          <option [value]="outro" [selected]="escolhido() === outro">Outro (hospedagem com domínio próprio)</option>
        </select>
      </span>
    </div>

    @if (naoSuportado()) {
      <div class="alert alert-warning pv-provedor__aviso" role="status">
        <span>
          A Microsoft não aceita mais que outros sistemas leiam o e-mail com senha. Para usar uma caixa Outlook,
          Hotmail ou Microsoft 365, <strong>encaminhe as notas</strong> para uma caixa Gmail e cadastre a do Gmail aqui
          (passo a passo abaixo).
        </span>
      </div>
    } @else {
      <p class="pv-formulario__ajuda">{{ ajuda() }}</p>
    }

    <details class="pv-opcional pv-provedor__encaminhar" [open]="naoSuportado()">
      <summary>As notas chegam em outro e-mail? Encaminhe automaticamente</summary>
      <p class="pv-formulario__ajuda">
        Crie no e-mail que recebe as notas uma regra que encaminha as mensagens com anexo para
        @if (destino()) {
          <strong>{{ destino() }}</strong>.
        } @else {
          a caixa cadastrada aqui (de preferência um Gmail só para as notas).
        }
        O Previsio lê a nota encaminhada normalmente.
      </p>
      <ul class="pv-provedor__passos">
        <li>
          <strong>Gmail:</strong> Configurações › Encaminhamento e POP/IMAP › Adicionar endereço de encaminhamento
          (confirme o código que chega no destino). Depois crie um filtro com <code>has:attachment filename:xml</code>
          e a ação "Encaminhar para".
        </li>
        <li>
          <strong>Outlook, Hotmail ou Microsoft 365:</strong> Configurações › Email › Regras › Adicionar nova regra:
          condição "Tem anexo", ação "Encaminhar para". No Microsoft 365, o administrador pode precisar liberar
          o encaminhamento automático para fora da empresa.
        </li>
        <li>
          <strong>Outros webmails:</strong> procure por "Regras", "Filtros" ou "Encaminhamento" nas configurações.
        </li>
      </ul>
    </details>
  `
})
export class ProvedorEmailComponent {
  readonly host = model.required<string>();
  readonly porta = model.required<number | null>();
  readonly ssl = model.required<boolean>();
  // E-mail para onde encaminhar (a caixa da rede, no cadastro da empresa)
  readonly destino = input<string | null>(null);
  // Prefixo do id do select, para não repetir quando há mais de um na tela
  readonly prefixo = input('provedor');

  readonly provedores = PROVEDORES_EMAIL;
  readonly outro = PROVEDOR_OUTRO;

  readonly escolhido = computed(() => provedorDoHost(this.host()));
  readonly idCampo = computed(() => this.prefixo() + '-provedor');

  readonly naoSuportado = computed(() => {
    const provedor = provedorPorId(this.escolhido());
    return provedor !== undefined && !provedor.suportado;
  });

  readonly ajuda = computed(() => {
    const provedor = provedorPorId(this.escolhido());
    if (provedor) {
      return provedor.ajuda;
    }
    return AJUDA_OUTRO;
  });

  escolher(evento: Event): void {
    const id = (evento.target as HTMLSelectElement).value;
    const provedor = provedorPorId(id);
    if (provedor) {
      this.host.set(provedor.host);
      this.porta.set(provedor.porta);
      this.ssl.set(provedor.ssl);
      return;
    }
    // "Outro": limpa o servidor de um provedor da lista para a pessoa digitar o da hospedagem
    if (provedorDoHost(this.host()) !== PROVEDOR_OUTRO) {
      this.host.set('');
    }
    this.porta.set(993);
    this.ssl.set(true);
  }
}
