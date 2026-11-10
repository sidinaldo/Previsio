import { Component, afterNextRender } from '@angular/core';
import { RouterOutlet } from '@angular/router';

// jQuery é carregado como script global (angular.json), só para o Bootstrap Material Design
declare const $: any;

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html'
})
export class App {
  constructor() {
    afterNextRender(() => {
      // Liga o efeito "ripple" dos botões e os campos com label flutuante.
      // Com o arrive.js, elementos que aparecem depois também são inicializados.
      $('body').bootstrapMaterialDesign();
    });
  }
}
