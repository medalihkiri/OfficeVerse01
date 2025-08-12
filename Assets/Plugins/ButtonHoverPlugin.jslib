mergeInto(LibraryManager.library, {
  SetButtonHoverState: function (gameObjectName, isHovering) {
    var gameObjectNameStr = UTF8ToString(gameObjectName);
    var canvas = unityInstance.Module.canvas;
    
    if (isHovering) {
      canvas.style.cursor = 'pointer';
    } else {
      canvas.style.cursor = 'default';
    }

    // Add a mouseleave event listener to the canvas if not already added
    if (!canvas.hasMouseLeaveHandler) {
      canvas.addEventListener('mouseleave', function() {
        canvas.style.cursor = 'default';
      });
      canvas.hasMouseLeaveHandler = true;
    }
  }
});