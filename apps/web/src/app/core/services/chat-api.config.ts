import { InjectionToken } from '@angular/core';

export interface ChatApiConfig {
  baseUrl: string;
}

export const CHAT_API_CONFIG = new InjectionToken<ChatApiConfig>('CHAT_API_CONFIG');
