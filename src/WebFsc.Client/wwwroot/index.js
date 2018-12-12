WebFsc = {
    /// Retrieve the file at the given path in the emscripten file system and download it.
    getCompiledFile: function (path) {
        var data = FS.readFile(path);
        var url = URL.createObjectURL(new Blob([data], { type: 'application/octet-stream' }));
        var link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', 'out.exe');
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};
