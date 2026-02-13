window.floatingPanel = {
    _dragState: null,
    _dragDistanceThresholdPx: 5,
    _recentDragTtlMs: 250,
    _recentDragByElement: {},
    startDrag: (elementId, startX, startY) => {
        const el = document.getElementById(elementId);
        if (!el) {
            return;
        }

        const rect = el.getBoundingClientRect();
        const computedStyle = window.getComputedStyle(el);
        if (computedStyle.transform && computedStyle.transform !== "none") {
            el.style.left = rect.left + "px";
            el.style.top = rect.top + "px";
            el.style.right = "auto";
            el.style.bottom = "auto";
            el.style.transform = "none";
        }

        window.floatingPanel._dragState = {
            elementId: elementId,
            startX: startX,
            startY: startY,
            offsetX: startX - rect.left,
            offsetY: startY - rect.top,
            hasMoved: false
        };

        document.addEventListener("mousemove", window.floatingPanel._onMouseMove);
        document.addEventListener("mouseup", window.floatingPanel._onMouseUp);
    },
    consumeRecentDrag: (elementId) => {
        const dragAt = window.floatingPanel._recentDragByElement[elementId];
        if (typeof dragAt !== "number") {
            return false;
        }

        delete window.floatingPanel._recentDragByElement[elementId];
        const elapsedMs = Date.now() - dragAt;
        return elapsedMs <= window.floatingPanel._recentDragTtlMs;
    },
    resetToCenter: (elementId) => {
        const el = document.getElementById(elementId);
        if (!el) {
            return;
        }

        el.style.left = "50%";
        el.style.top = "50%";
        el.style.right = "auto";
        el.style.bottom = "auto";
        el.style.transform = "translate(-50%, -50%)";
    },
    _onMouseMove: (e) => {
        const state = window.floatingPanel._dragState;
        if (!state) {
            return;
        }

        const el = document.getElementById(state.elementId);
        if (!el) {
            return;
        }

        if (!state.hasMoved) {
            const distancePx = Math.hypot(e.clientX - state.startX, e.clientY - state.startY);
            if (distancePx >= window.floatingPanel._dragDistanceThresholdPx) {
                state.hasMoved = true;
            }
        }

        const newX = e.clientX - state.offsetX;
        const newY = e.clientY - state.offsetY;

        el.style.left = newX + "px";
        el.style.top = newY + "px";
        el.style.right = "auto";
        el.style.bottom = "auto";
    },
    _onMouseUp: () => {
        const state = window.floatingPanel._dragState;
        if (state && state.hasMoved) {
            window.floatingPanel._recentDragByElement[state.elementId] = Date.now();
        }

        window.floatingPanel._dragState = null;
        document.removeEventListener("mousemove", window.floatingPanel._onMouseMove);
        document.removeEventListener("mouseup", window.floatingPanel._onMouseUp);
    }
};
