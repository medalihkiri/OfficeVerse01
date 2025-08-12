mergeInto(LibraryManager.library, {
    EnableUndoSupport: function (elementIDPtr) {
        var elementID = UTF8ToString(elementIDPtr);
        setTimeout(() => {
            let input = document.getElementById(elementID);
            if (input) {
                input.addEventListener("keydown", function(event) {
                    // Handle Ctrl + Z (Undo)
                    if (event.ctrlKey && event.key === "z") {
                        event.preventDefault(); // Prevent browser behavior
                        // Call Unity C# method
                        SendMessage("TMPClipboardHandler", "UndoLastChange");
                    }

                    // Handle Ctrl + A (Select All)
                    if (event.ctrlKey && event.key === "a") {
                        event.preventDefault(); // Prevent browser behavior
                        // Call Unity C# method
                        SendMessage("TMPClipboardHandler", "SelectAllText");
                    }
                });
            }
        }, 100);
    },

    SelectAllJS: function (elementIDPtr) {
        var elementID = UTF8ToString(elementIDPtr);
        setTimeout(() => {
            let input = document.getElementById(elementID);
            if (input) {
                input.select();
            }
        }, 100);
    }
});
