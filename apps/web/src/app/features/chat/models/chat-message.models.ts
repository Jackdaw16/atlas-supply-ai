import { RagSource } from './chat-api.models';

export type ChatRole = 'user' | 'assistant';
export type ChatPendingStatus = 'processing' | 'tool' | 'rag' | 'waiting';

export interface ChatMessage {
  id: number;
  role: ChatRole;
  content: string;
  toolsUsed?: string[];
  ragSources?: RagSource[];
  isPending?: boolean;
  pendingStatus?: ChatPendingStatus;
}
