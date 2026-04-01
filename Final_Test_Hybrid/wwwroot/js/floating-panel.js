window.floatingPanel = {
    _dragState: null,
    _dragDistanceThresholdPx: 5,
    _recentDragTtlMs: 250,
    _recentDragByElement: {},
    startDrag: (elementId, pointerId, startX, startY) => {
        const el = document.getElementById(elementId);
        if (!el) {
            return;
        }

        window.floatingPanel._clearDragState(false);

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
            pointerId: pointerId,
            startX: startX,
            startY: startY,
            offsetX: startX - rect.left,
            offsetY: startY - rect.top,
            hasMoved: false
        };

        if (typeof el.setPointerCapture === "function") {
            try {
                el.setPointerCapture(pointerId);
            } catch {
                // ignore: pointer may no longer be active
            }
        }

        document.addEventListener("pointermove", window.floatingPanel._onPointerMove);
        document.addEventListener("pointerup", window.floatingPanel._onPointerUpOrCancel);
        document.addEventListener("pointercancel", window.floatingPanel._onPointerUpOrCancel);
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
    restartAnimation: (elementId) => {
        const el = document.getElementById(elementId);
        if (!el) {
            return;
        }

        if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        el.style.animation = "none";
        void el.offsetWidth;
        el.style.removeProperty("animation");
    },
    _clearDragState: (recordRecentDrag) => {
        const state = window.floatingPanel._dragState;
        if (!state) {
            return;
        }

        const el = document.getElementById(state.elementId);
        if (recordRecentDrag && state.hasMoved) {
            window.floatingPanel._recentDragByElement[state.elementId] = Date.now();
        }

        if (el && typeof el.releasePointerCapture === "function") {
            try {
                if (el.hasPointerCapture(state.pointerId)) {
                    el.releasePointerCapture(state.pointerId);
                }
            } catch {
                // ignore: pointer capture may already be released
            }
        }

        window.floatingPanel._dragState = null;
        document.removeEventListener("pointermove", window.floatingPanel._onPointerMove);
        document.removeEventListener("pointerup", window.floatingPanel._onPointerUpOrCancel);
        document.removeEventListener("pointercancel", window.floatingPanel._onPointerUpOrCancel);
    },
    _onPointerMove: (e) => {
        const state = window.floatingPanel._dragState;
        if (!state || e.pointerId !== state.pointerId) {
            return;
        }

        const el = document.getElementById(state.elementId);
        if (!el) {
            window.floatingPanel._clearDragState(false);
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
    _onPointerUpOrCancel: (e) => {
        const state = window.floatingPanel._dragState;
        if (!state || e.pointerId !== state.pointerId) {
            return;
        }

        window.floatingPanel._clearDragState(true);
    }
};
