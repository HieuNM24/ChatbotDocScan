import { useState, useRef, useEffect } from 'react';
import { Send, Bot, User, Loader2, Trash2 } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import { sendChat, getChatHistory, clearChatHistory } from './api';
import './ChatTab.css';

const WELCOME = {
  id: 'welcome',
  role: 'ai',
  content: 'Xin chào! Tôi là trợ lý AI. Hãy upload tài liệu PDF và đặt câu hỏi về nội dung của chúng.',
};

export default function ChatTab() {
  const [messages, setMessages] = useState([WELCOME]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const bottomRef = useRef(null);
  const inputRef = useRef(null);

  // Tải lịch sử trò chuyện đã lưu từ CSDL PostgreSQL khi mở tab/tải lại trang
  useEffect(() => {
    const fetchHistory = async () => {
      setLoadingHistory(true);
      try {
        const res = await getChatHistory();
        if (res.data && res.data.length > 0) {
          // Backend trả về mới nhất trước (OrderByDescending), đảo ngược lại để hiển thị từ cũ -> mới
          const historyLogs = [...res.data].reverse();
          const restoredMessages = [];

          historyLogs.forEach((log) => {
            restoredMessages.push({
              id: `${log.id}-q`,
              role: 'user',
              content: log.question,
            });
            restoredMessages.push({
              id: `${log.id}-a`,
              role: 'ai',
              content: log.answer,
            });
          });

          setMessages([WELCOME, ...restoredMessages]);
        }
      } catch (err) {
        console.error('Không thể tải lịch sử trò chuyện:', err);
      } finally {
        setLoadingHistory(false);
      }
    };

    fetchHistory();
  }, []);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = async () => {
    const q = input.trim();
    if (!q || loading) return;

    const userMsg = { id: Date.now(), role: 'user', content: q };
    setMessages((prev) => [...prev, userMsg]);
    setInput('');
    setLoading(true);

    const thinkingId = Date.now() + 1;
    setMessages((prev) => [...prev, { id: thinkingId, role: 'ai', content: null, thinking: true }]);

    try {
      const res = await sendChat(q);
      setMessages((prev) =>
        prev.map((m) =>
          m.id === thinkingId
            ? { id: thinkingId, role: 'ai', content: res.data.answer, thinking: false }
            : m
        )
      );
    } catch (err) {
      const errMsg = err.response?.data?.message || 'Đã có lỗi xảy ra. Vui lòng thử lại.';
      setMessages((prev) =>
        prev.map((m) =>
          m.id === thinkingId
            ? { id: thinkingId, role: 'ai', content: `❌ ${errMsg}`, thinking: false, error: true }
            : m
        )
      );
    } finally {
      setLoading(false);
      inputRef.current?.focus();
    }
  };

  const handleClear = async () => {
    if (!confirm('Bạn có chắc muốn xóa toàn bộ lịch sử trò chuyện?')) return;
    try {
      await clearChatHistory();
      setMessages([WELCOME]);
    } catch (err) {
      alert('Không thể xóa lịch sử chat.');
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <div className="chat-tab">
      {/* Chat header bar */}
      <div className="chat-top-bar">
        <span className="chat-status">
          {loadingHistory ? (
            <>
              <Loader2 size={13} className="spin" /> Đang khôi phục lịch sử...
            </>
          ) : (
            `Cuộc hội thoại (${messages.length > 1 ? messages.length - 1 : 0} tin nhắn)`
          )}
        </span>
        {messages.length > 1 && (
          <button className="btn-clear-chat" onClick={handleClear} title="Xóa lịch sử trò chuyện">
            <Trash2 size={14} /> Xóa lịch sử
          </button>
        )}
      </div>

      {/* Message list */}
      <div className="messages">
        {messages.map((msg) => (
          <div key={msg.id} className={`message message-${msg.role}`}>
            <div className="msg-avatar">
              {msg.role === 'user' ? <User size={16} /> : <Bot size={16} />}
            </div>
            <div className={`msg-bubble${msg.error ? ' msg-error' : ''}`}>
              {msg.thinking ? (
                <div className="thinking">
                  <Loader2 size={14} className="spin" />
                  <span>AI đang suy nghĩ…</span>
                </div>
              ) : msg.role === 'ai' ? (
                <ReactMarkdown>{msg.content}</ReactMarkdown>
              ) : (
                <p>{msg.content}</p>
              )}
            </div>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      {/* Input area */}
      <div className="chat-input-area">
        <textarea
          ref={inputRef}
          className="chat-input"
          rows={1}
          placeholder="Nhập câu hỏi về tài liệu... (Enter để gửi, Shift+Enter xuống dòng)"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={loading}
        />
        <button
          className="send-btn"
          onClick={handleSend}
          disabled={!input.trim() || loading}
          title="Gửi"
        >
          {loading ? <Loader2 size={18} className="spin" /> : <Send size={18} />}
        </button>
      </div>
    </div>
  );
}
