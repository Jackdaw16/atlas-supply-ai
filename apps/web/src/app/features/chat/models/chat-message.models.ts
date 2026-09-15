import { RagSource } from './chat-api.models';

export type ChatRole = 'user' | 'assistant';

export interface ChatMessage {
  id: number;
  role: ChatRole;
  content: string;
  toolsUsed?: string[];
  ragSources?: RagSource[];
}
