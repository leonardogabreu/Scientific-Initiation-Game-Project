mergeInto(LibraryManager.library, {
    PingReady: function () {
        // Verifica de forma segura se o Arthur colocou a função no site antes de chamar
        if (typeof window.UnityReady !== 'undefined') {
            window.UnityReady();
        } else {
            console.warn("Função UnityReady não encontrada no HTML do servidor.");
        }
    }
});