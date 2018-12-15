WebFsc = {
    initAce: function (id, initText, onEdit) {
        var editor = ace.edit(id, { mode: "ace/mode/fsharp" });
        editor.session.setValue(initText);
        editor.session.on('change', () => {
            onEdit.invokeMethodAsync('SetText', editor.session.getValue());
        });
        editor.focus();
        this.editor = editor;
    },
    selectMessage: function (start, end) {
        var el = document.getElementById("editor");
        el.focus();
        el.setSelectionRange(start, end);
    },
    write: function (s, isErr) {
        var node = document.createTextNode(s);
        if (isErr) {
            var wrap = document.createElement("span");
            wrap.setAttribute("class", "stderr");
            wrap.appendChild(node);
            node = wrap;
        }
        document.getElementById("stdout")
            .appendChild(node);
    },
    clear: function () {
        let out = document.getElementById("stdout");
        for (var n = out.lastChild; n; n = out.lastChild) {
            out.removeChild(n);
        }
    },
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
