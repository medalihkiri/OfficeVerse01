var FileUploaderPlugin = {
    // Variable to store the array of files
    $selectedFiles: [],

    // 1. OPEN PICKER (MULTI-SELECT)
    BrowserFileSelect: function(gameObjectNamePtr, methodNamePtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);

        var fileInput = document.createElement('input');
        fileInput.setAttribute('type', 'file');
        // Enable Multiple Selection
        fileInput.setAttribute('multiple', 'multiple');
        fileInput.setAttribute('accept', '.pdf,.png,.jpg,.jpeg,.txt');
        fileInput.style.display = 'none';

        fileInput.onclick = function (event) { this.value = null; };

        fileInput.onchange = function (event) {
            if (!event.target.files || event.target.files.length === 0) return;

            // Convert FileList to Array and store globally
            selectedFiles = Array.from(event.target.files);

            // Calculate Metadata for Unity
            var totalSize = 0;
            var names = [];
            
            for (var i = 0; i < selectedFiles.length; i++) {
                totalSize += selectedFiles[i].size;
                names.push(selectedFiles[i].name);
            }

            // Create a summary string
            // Format: "Count|Name1, Name2, Name3...|TotalSizeBytes"
            var namesStr = names.join(", ");
            // Truncate names if too long for UI
            if (namesStr.length > 50) namesStr = namesStr.substring(0, 47) + "...";

            var payload = selectedFiles.length + "|" + namesStr + "|" + totalSize;
            
            SendMessage(gameObjectName, methodName, payload);
        };

        document.body.appendChild(fileInput);
        fileInput.click();
        document.body.removeChild(fileInput);
    },

    // 2. UPLOAD ALL FILES
    BrowserFileSubmit: function(urlPtr, tokenPtr, titlePtr, contextPtr, gameObjectNamePtr, callbackPtr) {
        var url = UTF8ToString(urlPtr);
        var token = UTF8ToString(tokenPtr);
        var title = UTF8ToString(titlePtr);
        var context = UTF8ToString(contextPtr);
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var callbackMethod = UTF8ToString(callbackPtr);

        var formData = new FormData();
        formData.append("title", title);
        formData.append("contextText", context);

        // Append all files to FormData
        // IMPORTANT: The key must be "files" to match Node's upload.array('files')
        if (selectedFiles && selectedFiles.length > 0) {
            for (var i = 0; i < selectedFiles.length; i++) {
                formData.append("files", selectedFiles[i]);
            }
        }

        fetch(url, {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + token
                // Content-Type is set automatically by fetch for FormData
            },
            body: formData
        })
        .then(response => {
            if (!response.ok) throw new Error("HTTP " + response.status);
            return response.text();
        })
        .then(text => {
            selectedFiles = []; // Clear memory on success
            SendMessage(gameObjectName, callbackMethod, "SUCCESS|" + text);
        })
        .catch(error => {
            console.error("Upload Error:", error);
            SendMessage(gameObjectName, callbackMethod, "ERROR|" + error.message);
        });
    },

    BrowserFileClear: function() {
        selectedFiles = [];
    }
};

autoAddDeps(FileUploaderPlugin, '$selectedFiles');
mergeInto(LibraryManager.library, FileUploaderPlugin);