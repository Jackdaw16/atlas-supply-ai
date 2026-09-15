import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CHAT_API_CONFIG } from '../../../core/services/chat-api.config';
import { ChatRequest, ChatResponse } from '../models/chat-api.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(CHAT_API_CONFIG);

  send(request: ChatRequest) {
    return this.http.post<ChatResponse>(`${this.apiConfig.baseUrl}/api/chat`, request);
  }
}
