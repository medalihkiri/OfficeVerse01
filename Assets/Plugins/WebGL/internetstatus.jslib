mergeInto(LibraryManager.library, {
  IsBrowserOnline: function () {
    try {
      return navigator.onLine ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  RegisterOnlineOfflineCallbacks: function (gameObjectPtr) {
    var gameObject = UTF8ToString(gameObjectPtr);

    function goOnline() {
      // send to Unity game object: method OnBrowserOnline with empty string param
      try {
        if (typeof SendMessage !== 'undefined') SendMessage(gameObject, 'OnBrowserOnline', '');
      } catch (e) { console.warn('RegisterOnlineOfflineCallbacks: SendMessage error', e); }
    }

    function goOffline() {
      try {
        if (typeof SendMessage !== 'undefined') SendMessage(gameObject, 'OnBrowserOffline', '');
      } catch (e) { console.warn('RegisterOnlineOfflineCallbacks: SendMessage error', e); }
    }

    try {
      window.addEventListener('online', goOnline);
      window.addEventListener('offline', goOffline);
    } catch (e) {
      console.warn('RegisterOnlineOfflineCallbacks: could not attach events', e);
    }
  }
});
