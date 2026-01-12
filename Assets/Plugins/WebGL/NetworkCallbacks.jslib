mergeInto(LibraryManager.library, {
  IsBrowserOnline: function() {
    return navigator.onLine ? 1 : 0;
  },

  RegisterOnlineOfflineCallbacks: function(gameObjectName) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    
    window.addEventListener('online', function() {
      try {
        SendMessage(gameObjectNameStr, 'OnBrowserOnline', '');
      } catch (e) {
        console.error("Could not send 'OnBrowserOnline' message: " + e);
      }
    });
    
    window.addEventListener('offline', function() {
      try {
        SendMessage(gameObjectNameStr, 'OnBrowserOffline', '');
      } catch (e) {
        console.error("Could not send 'OnBrowserOffline' message: " + e);
      }
    });
  }
});