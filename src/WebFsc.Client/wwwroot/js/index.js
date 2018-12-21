WebFsc = {
  /// Initialize the Ace editor.
  initAce: function (id, initText, onEdit, complete) {
    Split(['#editor', '#outputs'], { gutterSize: 5 });
    Split(['#messages-panel', '#stdout-panel'], { gutterSize: 5, direction: 'vertical' });
    ace.require('ace/ext/language_tools');
    var editor = WebFsc.editor = ace.edit(id, { mode: "ace/mode/fsharp" });
    editor.setOptions({
      enableBasicAutocompletion: true
    });
    editor.completers = [WebFsc.completer(complete)];
    editor.session.setValue(initText);
    editor.session.on('change', (ev) => {
      onEdit.invokeMethodAsync('Invoke', editor.session.getValue());
      // Uncomment the following to activate autocomplete on dot:
      //if (ev.action === 'insert' && ev.lines.length === 1 && ev.lines[0] === '.') {
      //  setTimeout(function () {
      //    editor.commands.byName.startAutocomplete.exec(editor);
      //  }, 50);
      //}
    });
    editor.focus();
  },
  /// Set the text contents of the Ace editor.
  setText: function (text) {
    WebFsc.editor.session.setValue(text);
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
  /// Ace autocomplete config.
  completer: function (complete) {
    return {
      getCompletions: function (editor, session, pos, prefix, callback) {
        let line = session.getLine(pos.row);
        complete.invokeMethodAsync("Complete", pos.row + 1, pos.column - 1, line)
          .then(results => callback(null, results));
      },
      getDocTooltip: function (item) {
        return { docHTML: ['<pre>', item.cache.invokeMethod('GetTooltip', item.index), '</pre>'].join('') };
      }
    };
  },
  /// Called by DeclarationSet to signal that it is done computing the tooltip.
  updateTooltip: function () {
    WebFsc.editor.completer.updateDocTooltip();
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
  },
  /// Retrieve the given URL query parameter, or null if it is absent.
  getQueryParam: function (param) {
    return new URLSearchParams(window.location.search).get(param);
  },
  /// Listens to URL changes for the given query parameter.
  /// Call callback on every URL change with this parameter.
  listenToQueryParam: function (param, callback) {
    addEventListener('popstate', function () {
      console.log('before');
      callback.invokeMethodAsync('Invoke', WebFsc.getQueryParam(param));
      console.log('after');
    });
  },
  /// Set the given URL query parameter to the given value, if it isn't already.
  setQueryParam: function (param, value) {
    let p = new URLSearchParams(window.location.search);
    if (p.get(param) !== value) {
      p.set(param, value);
      let q = p.toString();
      setTimeout(function () { history.pushState(q, '', '?' + q) }, 100);
    }
  }
};

// Very rudimentary browser detection:
// Firefox has "Gecko/someversion" while others have "(KHTML, like Gecko)"
if (/Gecko\//.test(navigator.userAgent)) {
  let s = document.createElement("style");
  s.appendChild(document.createTextNode(".loader-firefox{display:none}"));
  document.head.appendChild(s);
}
