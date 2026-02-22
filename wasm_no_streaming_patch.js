(function () {
  if (typeof WebAssembly !== 'object') return;
  const nativeInstantiate = WebAssembly.instantiate;
  if (typeof nativeInstantiate !== 'function') return;
  const streamingInstantiate = WebAssembly.instantiateStreaming;
  if (typeof streamingInstantiate !== 'function') return;
  const patched = async function (source, importObject) {
    const response = await Promise.resolve(source);
    const arrayBuffer = await response.arrayBuffer();
    return nativeInstantiate(arrayBuffer, importObject);
  };
  patched.original = streamingInstantiate;
  WebAssembly.instantiateStreaming = patched;
})();
