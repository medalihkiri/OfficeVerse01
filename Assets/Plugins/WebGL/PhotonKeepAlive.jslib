mergeInto(LibraryManager.library, {
  StartPhotonBackgroundPings: function () {
    if (window._photonPingId) return;

    function tick() {
      if (document.hidden && window.unityInstance) {
        // Call Unity method
        window.unityInstance.SendMessage("PhotonBridge", "BackgroundTick", "");
      }
    }

    // Browser clamps hidden tabs to ~1s anyway
    window._photonPingId = setInterval(tick, 1000);

    // Also trigger immediately on visibility change
    document.addEventListener("visibilitychange", tick);
  },

  StopPhotonBackgroundPings: function () {
    if (window._photonPingId) {
      clearInterval(window._photonPingId);
      window._photonPingId = 0;
    }
  }
});
