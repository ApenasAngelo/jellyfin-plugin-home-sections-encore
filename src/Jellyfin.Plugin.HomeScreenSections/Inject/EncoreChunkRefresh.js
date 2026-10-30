// A device can keep the home chunk from before the plugin changed it, cached without revalidation. Then the stock home
// runs and window.HssPageMeta never appears. When that happens, download the chunk again and reload, at most once a session.
(function () {
    var KEY = 'encore-chunk-refreshed', tries = 0;

    function check() {
        if (window.HssPageMeta || tries > 20) {
            return clearInterval(timer);
        }
        if (location.hash.indexOf('#/home') !== 0 || !window.ApiClient || !document.querySelector('.homeSectionsContainer .verticalSection')) {
            return;
        }
        if (++tries < 3) {
            return; // the stock home may just have been drawn first
        }
        clearInterval(timer);
        try {
            if (sessionStorage.getItem(KEY)) {
                return;
            }
            sessionStorage.setItem(KEY, '1');
        } catch (e) {
            return;
        }
        window.ApiClient.getJSON(window.ApiClient.getUrl('HomeScreen/Meta')).then(function (meta) {
            if (meta.Enabled && meta.ChunkFile) {
                return fetch(window.ApiClient.getUrl('web/' + meta.ChunkFile), { cache: 'reload' }).then(function () {
                    location.reload();
                });
            }
        });
    }

    var timer = setInterval(check, 2000);
})();
