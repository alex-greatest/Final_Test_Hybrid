window.outsideClickHandler = {
    add: (elementId, dotNetHelper) => {
        if (!window._outsideClickHandlers) {
            window._outsideClickHandlers = {};
        }

        const handler = (e) => {
            const element = document.getElementById(elementId);
            if (element && !element.contains(e.target)) {
                dotNetHelper.invokeMethodAsync("CloseEdit");
            }
        };

        window._outsideClickHandlers[elementId] = handler;
        document.addEventListener("click", handler);
    },
    remove: (elementId) => {
        if (window._outsideClickHandlers && window._outsideClickHandlers[elementId]) {
            document.removeEventListener("click", window._outsideClickHandlers[elementId]);
            delete window._outsideClickHandlers[elementId];
        }
    }
};

window.updateDialogTitle = (title) => {
    const titleElement = document.querySelector(".rz-dialog-titlebar .rz-dialog-title");
    if (titleElement) {
        titleElement.textContent = title;
    }
};
