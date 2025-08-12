mergeInto(LibraryManager.library, {
  ShowContextMenu: function (gameObjectName, x, y, isMessageField) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    var existingMenu = document.getElementById('customContextMenu');
    if (existingMenu) {
      document.body.removeChild(existingMenu);
    }
    var contextMenu = document.createElement('div');
    contextMenu.id = 'customContextMenu';
    contextMenu.style.position = 'absolute';
    var canvas = unityInstance.Module.canvas;
    var rect = canvas.getBoundingClientRect();
    var scaleX = rect.width / canvas.width;
    var scaleY = rect.height / canvas.height;
    var posX = x * scaleX;
    var posY = (canvas.height - y) * scaleY;
    contextMenu.style.left = (rect.left + posX) + 'px';
    contextMenu.style.top = (rect.top + posY) + 'px';
    contextMenu.style.backgroundColor = 'rgb(32, 37, 64)';
    contextMenu.style.border = '1px solid rgb(49, 56, 98)';
    contextMenu.style.borderRadius = '5px';
    contextMenu.style.padding = '5px';
    contextMenu.style.zIndex = '1000';
    var buttonStyle = 'background-color: rgb(49, 56, 98); color: white; border: none; padding: 5px 10px; margin: 2px; border-radius: 3px;';
    
    var copyButton = document.createElement('button');
    copyButton.textContent = 'Copy';
    copyButton.style.cssText = buttonStyle;
    copyButton.onclick = function() {
        unityInstance.SendMessage(gameObjectNameStr, 'CopySelectedText');
        document.body.removeChild(contextMenu);
    };
    contextMenu.appendChild(copyButton);
    
    if (!isMessageField) {
        var pasteButton = document.createElement('button');
        pasteButton.textContent = 'Paste';
        pasteButton.style.cssText = buttonStyle;
        pasteButton.onclick = function() {
            unityInstance.SendMessage(gameObjectNameStr, 'RequestPaste');
            document.body.removeChild(contextMenu);
        };
        contextMenu.appendChild(pasteButton);
    }
    
    document.body.appendChild(contextMenu);
    document.addEventListener('click', function removeMenu(e) {
      if (!contextMenu.contains(e.target)) {
        if (document.body.contains(contextMenu)) {
          document.body.removeChild(contextMenu);
        }
        document.removeEventListener('click', removeMenu);
      }
    });
  },

  CopyToClipboard: function (text) {
    var textStr = UTF8ToString(text);
    navigator.clipboard.writeText(textStr).then(function() {
      console.log('Text successfully copied to clipboard');
    }).catch(function(err) {
      console.error('Unable to copy text to clipboard', err);
    });
  },

  RequestPasteFromClipboard: function (gameObjectName) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    navigator.clipboard.readText().then(function(text) {
      console.log("Pasting from clipboard:", text);
      unityInstance.SendMessage(gameObjectNameStr, 'PasteText', text);
    }).catch(function(err) {
      console.error('Failed to read clipboard contents: ', err);
    });
  },

  DetectKeyEvents: function (gameObjectName) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    document.addEventListener("keydown", function(event) {
        if (event.ctrlKey) {
            let key = event.key.toLowerCase();
            if (unityInstance) {
                if (key === "c") { unityInstance.SendMessage(gameObjectNameStr, "OnCtrlKey", "C"); }
                else if (key === "v") { unityInstance.SendMessage(gameObjectNameStr, "OnCtrlKey", "V"); }
                else if (key === "a") { unityInstance.SendMessage(gameObjectNameStr, "OnCtrlKey", "A"); event.preventDefault(); } // Prevents browser select-all
                else if (key === "z") { unityInstance.SendMessage(gameObjectNameStr, "OnCtrlKey", "Z"); event.preventDefault(); } // Prevents browser undo
            }
        }
    });
  },

  DisableDefaultCopyPaste: function () {},

  SetInputFieldHoverState: function (gameObjectName, isHovering) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    var canvas = unityInstance.Module.canvas;
    
    if (isHovering) {
      canvas.style.cursor = 'text';
    } else {
      canvas.style.cursor = 'default';
    }

    if (!canvas.hasMouseLeaveHandler) {
      canvas.addEventListener('mouseleave', function() {
        canvas.style.cursor = 'default';
      });
      canvas.hasMouseLeaveHandler = true;
    }
  },

  SetLinkHoverState: function (gameObjectName, isHovering) {
    var canvas = unityInstance.Module.canvas;
    if (isHovering) {
      canvas.style.cursor = 'pointer';
    } else {
      canvas.style.cursor = 'default';
    }
  }
});
