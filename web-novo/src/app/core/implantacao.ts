import { Cliente, Empresa, PERFIL_ADMIN, Usuario } from './previsio.models';

/** As cinco etapas da implantação de um cliente novo, na ordem em que são feitas. */
export type EtapaImplantacao = 'rede' | 'empresas' | 'bling' | 'usuario' | 'leitura';

export const ETAPAS_IMPLANTACAO: EtapaImplantacao[] = ['rede', 'empresas', 'bling', 'usuario', 'leitura'];

export const TITULO_ETAPA: Record<EtapaImplantacao, string> = {
  rede: 'Rede e e-mail',
  empresas: 'Empresas',
  bling: 'Conectar Bling',
  usuario: 'Usuário do cliente',
  leitura: 'Primeira leitura'
};

/**
 * O que já está cadastrado do cliente. A implantação não grava em que etapa parou:
 * ela olha estes dados e continua da primeira etapa que falta.
 */
export interface SituacaoImplantacao {
  cliente: Cliente | null;
  empresas: Empresa[];
  usuarios: Usuario[];
  notasLidas: number;
}

export function empresasAtivas(situacao: SituacaoImplantacao): Empresa[] {
  return situacao.empresas.filter(empresa => empresa.ativo);
}

/** Usuários desta rede, gestores e usuários comuns (o admin não conta). */
export function usuariosDoCliente(situacao: SituacaoImplantacao): Usuario[] {
  const cliente = situacao.cliente;
  if (cliente === null) {
    return [];
  }
  return situacao.usuarios.filter(usuario => usuario.clienteId === cliente.id && usuario.perfil !== PERFIL_ADMIN);
}

export function etapaConcluida(situacao: SituacaoImplantacao, etapa: EtapaImplantacao): boolean {
  if (etapa === 'rede') {
    return situacao.cliente !== null;
  }
  if (etapa === 'empresas') {
    return empresasAtivas(situacao).length > 0;
  }
  if (etapa === 'bling') {
    const ativas = empresasAtivas(situacao);
    return ativas.length > 0 && ativas.every(empresa => empresa.blingConectado);
  }
  if (etapa === 'usuario') {
    return usuariosDoCliente(situacao).length > 0;
  }
  return situacao.notasLidas > 0;
}

/** Primeira etapa que ainda falta; com tudo feito, a última (para ver o resultado da leitura). */
export function primeiraEtapaPendente(situacao: SituacaoImplantacao): EtapaImplantacao {
  for (const etapa of ETAPAS_IMPLANTACAO) {
    if (!etapaConcluida(situacao, etapa)) {
      return etapa;
    }
  }
  return 'leitura';
}

/** Sem a rede cadastrada só a primeira etapa abre: as outras dependem do cliente. */
export function podeAbrirEtapa(situacao: SituacaoImplantacao, etapa: EtapaImplantacao): boolean {
  if (etapa === 'rede') {
    return true;
  }
  return situacao.cliente !== null;
}

export function proximaEtapa(etapa: EtapaImplantacao): EtapaImplantacao {
  const indice = ETAPAS_IMPLANTACAO.indexOf(etapa);
  if (indice < 0 || indice === ETAPAS_IMPLANTACAO.length - 1) {
    return 'leitura';
  }
  return ETAPAS_IMPLANTACAO[indice + 1];
}

/** 11222333000181 → 11.222.333/0001-81 */
export function formatarCnpj(cnpj: string): string {
  if (!cnpj || cnpj.length !== 14) {
    return cnpj;
  }
  return cnpj.substring(0, 2) + '.' + cnpj.substring(2, 5) + '.' + cnpj.substring(5, 8) + '/'
    + cnpj.substring(8, 12) + '-' + cnpj.substring(12, 14);
}
