import { ChatHistoryMessage } from './chat-message.models';

export interface ChatRequest {
  message: string;
  history: ChatHistoryMessage[];
}

export interface RagSource {
  sourcePath: string;
  heading: string | null;
}

export interface ChatResponse {
  message: string;
  toolsUsed: string[];
  ragSources: RagSource[];
}
