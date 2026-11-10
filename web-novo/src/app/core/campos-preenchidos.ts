/**
 * O Bootstrap Material Design só sobe o rótulo flutuante (classe is-filled) quando o campo
 * recebe um evento "change". Valores que o Angular escreve sozinho (dados vindos da API,
 * selects com valor inicial) não disparam esse evento e o rótulo ficaria por cima do texto.
 * Chame depois que os valores estiverem na tela.
 */
export function avisarCamposPreenchidos(raiz: HTMLElement): void {
  // setTimeout: espera o ngModel escrever os valores, o que acontece logo depois da renderização
  setTimeout(() => {
    const campos = raiz.querySelectorAll('.bmd-form-group input, .bmd-form-group select');
    campos.forEach(campo => campo.dispatchEvent(new Event('change', { bubbles: true })));
  });
}
