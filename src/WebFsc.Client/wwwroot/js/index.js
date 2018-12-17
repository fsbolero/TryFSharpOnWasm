WebFsc = {
  /// Initialize the Ace editor.
  initAce: function (id, initText, onEdit) {
    var editor = WebFsc.editor = ace.edit(id, { mode: "ace/mode/fsharp" });
    editor.session.setValue(initText);
    editor.session.on('change', () => {
      onEdit.invokeMethodAsync('SetText', editor.session.getValue());
    });
    editor.focus();
  },
  /// Update the error/warning annotations.
  setAnnotations: function (annotations) {
    var es = WebFsc.editor.session;
    es.clearAnnotations();
    es.setAnnotations(annotations);
    if (WebFsc.markers) {
      WebFsc.markers.forEach(function (m) { es.removeMarker(m); });
    }
    WebFsc.markers = annotations.map(function (a) {
      var range = new ace.Range();
      range.start = es.doc.createAnchor(a.row, a.column);
      range.end = es.doc.createAnchor(a.y2, a.x2);
      return es.addMarker(range, "marker-" + a.type, "text", false);
    });
  },
  /// Select the given range.
  selectRange: function (startLine, startCol, endLine, endCol) {
    WebFsc.editor.focus();
    var range = new ace.Range(startLine, startCol, endLine, endCol);
    WebFsc.editor.session.getSelection()
      .setSelectionRange(range, false);
  },
  /// Write the given text to standard output, or standard error if isErr is true.
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
  /// Clear standard output and standard error;
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
