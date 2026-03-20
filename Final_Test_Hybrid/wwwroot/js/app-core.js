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

window.resultImageProbe = {
    attach: (elementId, dotNetHelper, renderVersion) => {
        window.resultImageProbe.detach(elementId);

        const element = document.getElementById(elementId);
        if (!element) {
            dotNetHelper.invokeMethodAsync("HandleImageError", renderVersion, "Элемент result-image не найден.");
            return;
        }

        const notifyLoaded = () =>
            dotNetHelper.invokeMethodAsync(
                "HandleImageLoaded",
                renderVersion,
                element.naturalWidth || 0,
                element.naturalHeight || 0);

        const notifyError = (reason) =>
            dotNetHelper.invokeMethodAsync("HandleImageError", renderVersion, reason);

        const handleLoad = () => notifyLoaded();
        const handleError = () => notifyError("Событие error при загрузке result-image.");

        if (!window._resultImageProbeHandlers) {
            window._resultImageProbeHandlers = {};
        }

        window._resultImageProbeHandlers[elementId] = {
            element,
            handleLoad,
            handleError
        };

        element.addEventListener("load", handleLoad, { once: true });
        element.addEventListener("error", handleError, { once: true });

        if (!element.complete) {
            return;
        }

        if ((element.naturalWidth || 0) > 0 && (element.naturalHeight || 0) > 0) {
            notifyLoaded();
            return;
        }

        notifyError("Картинка завершила загрузку с нулевым размером.");
    },

    detach: (elementId) => {
        if (!window._resultImageProbeHandlers || !window._resultImageProbeHandlers[elementId]) {
            return;
        }

        const { element, handleLoad, handleError } = window._resultImageProbeHandlers[elementId];
        element.removeEventListener("load", handleLoad);
        element.removeEventListener("error", handleError);
        delete window._resultImageProbeHandlers[elementId];
    }
};
