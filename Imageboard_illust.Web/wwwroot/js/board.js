(function () {
    const canvas = document.getElementById('board-canvas');
    if (!canvas) return;

    const boardId = canvas.dataset.boardId;
    const canEdit = canvas.dataset.canEdit === '1';
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    function post(handler, params) {
        const body = new URLSearchParams(params);
        return fetch(`/Board/${boardId}?handler=${handler}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token,
            },
            body: body.toString(),
            credentials: 'same-origin',
        }).then(r => r.json());
    }

    if (!canEdit) return;

    let activeCard = null;
    let mode = null; // 'move' | 'resize'
    let startX = 0, startY = 0;
    let startLeft = 0, startTop = 0, startWidth = 0, startHeight = 0;

    canvas.querySelectorAll('.board-card').forEach(setupCard);

    function setupCard(card) {
        const title = card.querySelector('.board-card-title');
        const resize = card.querySelector('.board-card-resize');
        const closeBtn = card.querySelector('.board-card-close');
        const nameEl = card.querySelector('.board-card-name');

        title.addEventListener('mousedown', (e) => {
            if (e.target === closeBtn) return;
            startDrag(card, 'move', e);
        });

        if (resize) {
            resize.addEventListener('mousedown', (e) => {
                e.stopPropagation();
                startDrag(card, 'resize', e);
            });
        }

        if (closeBtn) {
            closeBtn.addEventListener('click', () => {
                if (!confirm('Delete this image?')) return;
                const itemId = card.dataset.itemId;
                post('DeleteItem', { id: boardId, itemId }).then(res => {
                    if (res.ok) card.remove();
                });
            });
        }

        nameEl.addEventListener('dblclick', () => {
            const itemId = card.dataset.itemId;
            const next = prompt('Rename image', nameEl.textContent.trim());
            if (next === null) return;
            post('Rename', { id: boardId, itemId, name: next }).then(res => {
                if (res.ok) nameEl.textContent = res.name;
            });
        });
    }

    function startDrag(card, newMode, e) {
        e.preventDefault();
        activeCard = card;
        mode = newMode;
        startX = e.clientX;
        startY = e.clientY;
        startLeft = card.offsetLeft;
        startTop = card.offsetTop;
        startWidth = card.offsetWidth;
        startHeight = card.offsetHeight;
        canvas.appendChild(card); // bring to front
    }

    document.addEventListener('mousemove', (e) => {
        if (!activeCard) return;
        const dx = e.clientX - startX;
        const dy = e.clientY - startY;

        if (mode === 'move') {
            activeCard.style.left = Math.max(0, startLeft + dx) + 'px';
            activeCard.style.top = Math.max(0, startTop + dy) + 'px';
        } else if (mode === 'resize') {
            activeCard.style.width = Math.max(80, startWidth + dx) + 'px';
            activeCard.style.height = Math.max(68, startHeight + dy) + 'px';
        }
    });

    document.addEventListener('mouseup', () => {
        if (!activeCard) return;
        const card = activeCard;
        const itemId = card.dataset.itemId;
        activeCard = null;
        mode = null;
        post('Layout', {
            id: boardId,
            itemId,
            x: card.offsetLeft,
            y: card.offsetTop,
            width: card.offsetWidth,
            height: card.offsetHeight,
        });
    });
})();
