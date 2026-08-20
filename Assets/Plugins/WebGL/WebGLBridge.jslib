mergeInto(LibraryManager.library, {
    PingReady: function () {
        if (typeof window.UnityReady !== 'undefined') {
            window.UnityReady();
        } else {
            console.warn("Função UnityReady não encontrada no HTML do servidor.");
        }
    }
});