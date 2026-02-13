window.scrollGridToBottom = (containerId) => {
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }

    const scrollArea = container.querySelector(".rz-data-grid-data");
    if (scrollArea) {
        scrollArea.scrollTop = scrollArea.scrollHeight;
    }
};

window.scrollGridToTop = (containerId) => {
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }

    const scrollArea = container.querySelector(".rz-data-grid-data");
    if (scrollArea) {
        scrollArea.scrollTop = 0;
    }
};

window.testSequenceEditorPopupFix = {
    _handlersByContainer: {},
    register: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) {
            return;
        }

        this.unregister(containerId);

        const clickHandler = (event) => {
            if (!this._isDropDownEvent(container, event)) {
                return;
            }

            this._scheduleReposition(containerId, 0);
            this._scheduleReposition(containerId, 120);
        };

        const keydownHandler = (event) => {
            if (!this._isDropDownEvent(container, event)) {
                return;
            }

            const key = event.key;
            if (key !== "Enter" && key !== " " && key !== "Spacebar" && key !== "ArrowDown" && key !== "F4") {
                return;
            }

            this._scheduleReposition(containerId, 0);
            this._scheduleReposition(containerId, 120);
        };

        container.addEventListener("click", clickHandler, true);
        container.addEventListener("keydown", keydownHandler, true);

        this._handlersByContainer[containerId] = {
            clickHandler: clickHandler,
            keydownHandler: keydownHandler
        };
    },
    unregister: function (containerId) {
        const handlers = this._handlersByContainer[containerId];
        if (!handlers) {
            return;
        }

        const container = document.getElementById(containerId);
        if (container) {
            container.removeEventListener("click", handlers.clickHandler, true);
            container.removeEventListener("keydown", handlers.keydownHandler, true);
        }

        delete this._handlersByContainer[containerId];
    },
    _isDropDownEvent: function (container, event) {
        const target = event && event.target;
        if (!(target instanceof Element)) {
            return false;
        }

        const dropDown = target.closest(".rz-dropdown");
        return !!dropDown && container.contains(dropDown);
    },
    _scheduleReposition: function (containerId, delayMs) {
        window.setTimeout(() => {
            window.requestAnimationFrame(() => {
                const container = document.getElementById(containerId);
                if (!container) {
                    return;
                }

                this._repositionLastPopup(container);
            });
        }, delayMs);
    },
    _repositionLastPopup: function (container) {
        if (!window.Radzen || !Array.isArray(window.Radzen.popups) || typeof window.Radzen.repositionPopup !== "function") {
            return;
        }

        for (let i = window.Radzen.popups.length - 1; i >= 0; i--) {
            const popupEntry = window.Radzen.popups[i];
            if (!popupEntry || !(popupEntry.parent instanceof Element)) {
                continue;
            }

            if (!container.contains(popupEntry.parent)) {
                continue;
            }

            if (!popupEntry.parent.classList.contains("rz-dropdown")) {
                continue;
            }

            const popup = document.getElementById(popupEntry.id);
            if (!popup || popup.style.display === "none") {
                continue;
            }

            window.Radzen.repositionPopup(popupEntry.parent, popupEntry.id);
            return;
        }
    }
};
