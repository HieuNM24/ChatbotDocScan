import { useState, useCallback } from 'react';
import { Upload, FileText, Trash2, CheckCircle, AlertCircle, Loader2 } from 'lucide-react';
import { uploadDocument, getDocuments, deleteDocument } from './api';
import './DocumentTab.css';

export default function DocumentTab() {
  const [dragging, setDragging] = useState(false);
  const [file, setFile] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [toast, setToast] = useState(null); // { type: 'success'|'error', msg }
  const [documents, setDocuments] = useState([]);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [docsLoaded, setDocsLoaded] = useState(false);

  const showToast = (type, msg) => {
    setToast({ type, msg });
    setTimeout(() => setToast(null), 4000);
  };

  const handleDrop = useCallback((e) => {
    e.preventDefault();
    setDragging(false);
    const dropped = e.dataTransfer.files[0];
    if (dropped && dropped.name.endsWith('.pdf')) setFile(dropped);
    else showToast('error', 'Chỉ hỗ trợ file PDF.');
  }, []);

  const handleUpload = async () => {
    if (!file) return;
    setUploading(true);
    setProgress(0);
    try {
      const res = await uploadDocument(file, setProgress);
      showToast('success', `✅ "${res.data.fileName}" — ${res.data.chunkCount} chunks đã được lập chỉ mục.`);
      setFile(null);
      if (docsLoaded) loadDocuments();
    } catch (err) {
      showToast('error', err.response?.data?.message || 'Upload thất bại. Kiểm tra kết nối backend.');
    } finally {
      setUploading(false);
      setProgress(0);
    }
  };

  const loadDocuments = async () => {
    setLoadingDocs(true);
    try {
      const res = await getDocuments();
      setDocuments(res.data);
      setDocsLoaded(true);
    } catch {
      showToast('error', 'Không thể tải danh sách tài liệu.');
    } finally {
      setLoadingDocs(false);
    }
  };

  const handleDelete = async (id, name) => {
    if (!confirm(`Xóa "${name}"?`)) return;
    try {
      await deleteDocument(id);
      setDocuments((prev) => prev.filter((d) => d.id !== id));
      showToast('success', `Đã xóa "${name}".`);
    } catch {
      showToast('error', 'Xóa thất bại.');
    }
  };

  return (
    <div className="doc-tab">
      {/* Toast */}
      {toast && (
        <div className={`toast toast-${toast.type}`}>
          {toast.type === 'success' ? <CheckCircle size={16} /> : <AlertCircle size={16} />}
          <span>{toast.msg}</span>
        </div>
      )}

      {/* Drop zone */}
      <div
        className={`dropzone${dragging ? ' dragging' : ''}${file ? ' has-file' : ''}`}
        onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={handleDrop}
        onClick={() => document.getElementById('pdf-input').click()}
      >
        <input
          id="pdf-input"
          type="file"
          accept=".pdf"
          hidden
          onChange={(e) => setFile(e.target.files[0])}
        />
        {file ? (
          <div className="file-preview">
            <FileText size={40} className="icon-file" />
            <p className="file-name">{file.name}</p>
            <p className="file-size">{(file.size / 1024).toFixed(1)} KB</p>
          </div>
        ) : (
          <div className="drop-hint">
            <Upload size={40} className="icon-upload" />
            <p>Kéo thả file PDF vào đây</p>
            <span>hoặc click để chọn file</span>
          </div>
        )}
      </div>

      {/* Upload button */}
      <div className="upload-actions">
        {file && !uploading && (
          <button className="btn btn-ghost" onClick={() => setFile(null)}>Hủy</button>
        )}
        <button
          className="btn btn-primary"
          onClick={handleUpload}
          disabled={!file || uploading}
        >
          {uploading ? (
            <><Loader2 size={16} className="spin" /> Đang xử lý… {progress}%</>
          ) : (
            <><Upload size={16} /> Upload & Index</>
          )}
        </button>
      </div>

      {uploading && (
        <div className="progress-bar">
          <div className="progress-fill" style={{ width: `${progress}%` }} />
        </div>
      )}

      {/* Document list */}
      <div className="doc-list-section">
        <div className="doc-list-header">
          <h3>Tài liệu đã tải lên</h3>
          <button className="btn btn-ghost btn-sm" onClick={loadDocuments} disabled={loadingDocs}>
            {loadingDocs ? <Loader2 size={14} className="spin" /> : '↻'} Tải danh sách
          </button>
        </div>

        {docsLoaded && documents.length === 0 && (
          <p className="empty-state">Chưa có tài liệu nào. Hãy upload PDF để bắt đầu.</p>
        )}

        {documents.map((doc) => (
          <div key={doc.id} className="doc-item">
            <FileText size={18} className="icon-file" />
            <div className="doc-info">
              <span className="doc-name">{doc.fileName}</span>
              <span className="doc-meta">
                {doc.chunkCount} chunks · {new Date(doc.uploadedAt).toLocaleDateString('vi-VN')}
              </span>
            </div>
            <button
              className="btn-icon btn-danger"
              onClick={() => handleDelete(doc.id, doc.fileName)}
              title="Xóa tài liệu"
            >
              <Trash2 size={15} />
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
