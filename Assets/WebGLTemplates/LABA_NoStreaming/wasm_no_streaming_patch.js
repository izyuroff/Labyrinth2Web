(function () {
  if (typeof WebAssembly !== "object" || typeof WebAssembly.instantiate !== "function") {
    return;
  }

  var originalInstantiateStreaming = (typeof WebAssembly.instantiateStreaming === "function")
    ? WebAssembly.instantiateStreaming
    : null;

  async function resolveResponse(source) {
    var resolved = await Promise.resolve(source);
    if (resolved && typeof resolved.arrayBuffer === "function") {
      return resolved;
    }
    return fetch(resolved);
  }

  WebAssembly.instantiateStreaming = async function (source, importObject) {
    var response = await resolveResponse(source);
    var bytes = await response.arrayBuffer();
    return WebAssembly.instantiate(bytes, importObject);
  };

  WebAssembly.instantiateStreaming.original = originalInstantiateStreaming;
  WebAssembly.instantiateStreaming.noStreamingPatch = true;
})();
