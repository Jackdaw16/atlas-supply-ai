import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ChatMessage } from './core/models/chat-message';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatToolbarModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  protected draft = '';

  protected readonly messages: ChatMessage[] = [
    {
      role: 'assistant',
      content: 'Hello. I am the Atlas Supply enterprise assistant. The backend connection will be added in the next iteration.'
    }
  ];

  protected send(): void {
    const content = this.draft.trim();

    if (!content) {
      return;
    }

    this.messages.push({ role: 'user', content });
    this.draft = '';
  }
}
