mergeInto(LibraryManager.library, {
    /**
     * Redirects the entire browser window to the given URL.
     * @param {string} url - The full URL to redirect to.
     */
    RedirectToURL: function(url) {
        var urlString = UTF8ToString(url);
        console.log("Redirecting to: " + urlString);
        window.location.href = urlString;
    },

    /**
     * Reads a query parameter from the current browser URL.
     * @param {string} name - The name of the parameter to get.
     * @returns {string} - The value of the parameter, or an empty string if not found.
     */
    GetURLParameter: function(name) {
        var paramName = UTF8ToString(name);
        paramName = paramName.replace(/[\[\]]/g, '\\$&');
        var regex = new RegExp('[?&]' + paramName + '(=([^&#]*)|&|#|$)'),
            results = regex.exec(window.location.href);
        if (!results) return "";
        if (!results[2]) return "";
        var value = decodeURIComponent(results[2].replace(/\+/g, ' '));
        
        // Return the value as a string that C# can read
        var bufferSize = lengthBytesUTF8(value) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(value, buffer, bufferSize);
        return buffer;
    },

    /**
     * Cleans the query parameters from the URL without reloading the page.
     */
    CleanURLParameters: function() {
        var newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
        window.history.replaceState({ path: newUrl }, '', newUrl);
        console.log("URL has been cleaned.");
    }
});