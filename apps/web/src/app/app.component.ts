import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ChatComponent } from './features/chat/components/chat/chat.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MatIconModule, MatToolbarModule, ChatComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {}
