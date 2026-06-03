(function () {
  function qs(id) { return document.getElementById(id); }

  function init() {
    const box = qs('chatbox');
    if (!box) return false;

    const header = qs('chatHeader');
    const messages = qs('chatMessages');
    const input = qs('chatInput');
    const sendBtn = qs('chatSend');

    function renderText(text) {
      const safe = (text || '')
        .toString()
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
      return safe.replace(/\n/g, '<br>');
    }

    function append(role, text) {
      const row = document.createElement('div');
      row.className = 'msg ' + role;
      const bubble = document.createElement('div');
      bubble.className = 'bubble';
      bubble.innerHTML = renderText(text);
      row.appendChild(bubble);
      messages.appendChild(row);
      messages.scrollTop = messages.scrollHeight;
    }

    async function send() {
      const text = (input.value || '').trim();
      if (!text) return;
      input.value = '';
      append('user', text);

      try {
        const form = new FormData();
        form.append('message', text);
        const res = await fetch('/Chat/Ask', {
          method: 'POST',
          body: form,
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        const data = await res.json();
        append('bot', data.reply || 'Mình chưa hiểu câu hỏi của bạn.');
      } catch (e) {
        append('bot', 'Hiện chat đang lỗi. Bạn thử lại sau giúp mình nhé.');
      }
    }

    header?.addEventListener('click', function () {
      box.classList.toggle('collapsed');
    });

    sendBtn?.addEventListener('click', send);
    input?.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        send();
      }
    });

    // Welcome message (only once)
    if (!box.dataset.welcomed) {
      box.dataset.welcomed = '1';
      append('bot', 'Chào bạn! Bạn có thể hỏi về sản phẩm, vùng miền, giá, giao hàng, thanh toán, đổi trả.');
    }

    return true;
  }

  // If script is loaded before the chatbox markup, wait for DOMContentLoaded.
  if (!init()) {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  }
})();

