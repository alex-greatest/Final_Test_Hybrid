window.scrollGridToBottom = (containerId) => {
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }

    const scrollArea = container.querySelector(".rz-data-grid-data");
    if (!(scrollArea instanceof HTMLElement)) {
        return;
    }

    const scrollToBottom = (attempt) => {
        scrollArea.scrollTop = scrollArea.scrollHeight;
        if (attempt >= 2 || !scrollArea.isConnected) {
            return;
        }

        window.requestAnimationFrame(() => {
            scrollToBottom(attempt + 1);
        });
    };

    scrollToBottom(0);
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

window.mainTestSequenceGridResizeFix = {
    register: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container || container.__mainTestSequenceGridResizeFixRegistered) {
            return;
        }

        if (!this._patchRadzenStartResize()) {
            return;
        }

        container.__mainTestSequenceGridResizeFixRegistered = true;
    },
    sync: function (containerId) {
        window.requestAnimationFrame(() => {
            const container = document.getElementById(containerId);
            if (!container) {
                return;
            }

            this._syncCurrentWidths(container);
        });
    },
    _patchRadzenStartResize: function () {
        if (!window.Radzen || window.Radzen.__mainTestSequenceGridResizeFixPatched) {
            return !!(window.Radzen && window.Radzen.__mainTestSequenceGridResizeFixPatched);
        }

        const originalStartColumnResize = window.Radzen.startColumnResize;
        if (typeof originalStartColumnResize !== "function") {
            return false;
        }

        const helper = this;
        window.Radzen.startColumnResize = function (id, grid, columnIndex, clientX) {
            try {
                helper._prepareResizeBoundsById(id);
            } catch {
                // Resize must stay available even if the legacy-grid guard cannot compute bounds.
            }

            return originalStartColumnResize.call(this, id, grid, columnIndex, clientX);
        };

        window.Radzen.__mainTestSequenceGridResizeFixPatched = true;
        return true;
    },
    _prepareResizeBoundsById: function (id) {
        const resizer = document.getElementById(id + "-resizer");
        if (!resizer) {
            return;
        }

        const cell = resizer.parentNode && resizer.parentNode.parentNode;
        if (!(cell instanceof HTMLTableCellElement)) {
            return;
        }

        const container = cell.closest("#test-sequense-grid-container");
        if (!container) {
            return;
        }

        this._prepareResizeBounds(container, cell);
    },
    _prepareResizeBounds: function (container, activeCell) {
        this._syncCurrentWidths(container);

        const table = container.querySelector(".main-grid-legacy .rz-grid-table");
        const scrollArea = container.querySelector(".main-grid-legacy .rz-data-grid-data");
        if (!(table instanceof HTMLTableElement)
            || !(scrollArea instanceof HTMLElement)
            || !(activeCell instanceof HTMLTableCellElement)) {
            return;
        }

        const headerCells = Array.from(table.querySelectorAll("thead th"));
        const activeIndex = headerCells.indexOf(activeCell);
        if (activeIndex < 0) {
            return;
        }

        headerCells.forEach((cell) => {
            if (cell instanceof HTMLElement) {
                cell.style.maxWidth = "";
            }
        });

        const widths = headerCells.map(cell => Math.round(cell.getBoundingClientRect().width));
        const currentWidth = widths[activeIndex];
        const otherWidths = widths.reduce((sum, width, index) => {
            return index == activeIndex ? sum : sum + width;
        }, 0);

        const minWidth = parseFloat(activeCell.style.minWidth || 0);
        const viewportWidth = Math.floor(scrollArea.clientWidth);
        const availableWidth = viewportWidth - otherWidths;
        const maxWidth = Math.max(currentWidth, minWidth, availableWidth);
        activeCell.style.maxWidth = maxWidth + "px";
    },
    _syncCurrentWidths: function (container) {
        const table = container.querySelector(".main-grid-legacy .rz-grid-table");
        if (!(table instanceof HTMLTableElement)) {
            return;
        }

        const headerCells = Array.from(table.querySelectorAll("thead th"));
        if (headerCells.length === 0) {
            return;
        }

        const widths = headerCells.map(cell => Math.round(cell.getBoundingClientRect().width));
        if (widths.some(width => width <= 0)) {
            return;
        }

        const columns = Array.from(table.querySelectorAll("colgroup col"));
        headerCells.forEach((cell, index) => {
            const width = widths[index] + "px";
            cell.style.width = width;
            if (columns[index] instanceof HTMLTableColElement) {
                columns[index].style.width = width;
            }
        });
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
