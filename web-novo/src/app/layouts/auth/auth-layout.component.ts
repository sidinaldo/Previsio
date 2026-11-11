import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Layout de tela cheia (sem menu lateral nem barra do topo), usado pela tela de login.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet],
  templateUrl: './auth-layout.component.html'
})
export class AuthLayoutComponent {}
