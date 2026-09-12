import { useState } from 'react';
import { FileText, MessageSquare, Moon, Sun } from 'lucide-react';
import DocumentTab from './DocumentTab';
import ChatTab from './ChatTab';
import './App.css';

export default function App() {
  const [tab, setTab] = useState('chat');
  const [dark, setDark] = useState(true);

  return (
    <div className={`app${dark ? ' dark' : ''}`}>
      <header className="header">
        <div className="header-brand">
          <div className="brand-icon">🤖</div>
          <div>
            <h1>RAG Helpdesk</h1>
            <p>Trợ lý AI từ tài liệu của bạn</p>
          </div>
        </div>
        <div className="header-right">
          <nav className="tabs">
            <button
              className={`tab-btn${tab === 'docs' ? ' active' : ''}`}
              onClick={() => setTab('docs')}
            >
              <FileText size={16} />
              Tài liệu
            </button>
            <button
              className={`tab-btn${tab === 'chat' ? ' active' : ''}`}
              onClick={() => setTab('chat')}
            >
              <MessageSquare size={16} />
              Trợ lý AI
            </button>
          </nav>
          <button className="theme-toggle" onClick={() => setDark(!dark)} title="Chuyển theme">
            {dark ? <Sun size={18} /> : <Moon size={18} />}
          </button>
        </div>
      </header>

      <main className="main">
        {tab === 'docs' ? <DocumentTab /> : <ChatTab />}
      </main>
    </div>
  );
}
