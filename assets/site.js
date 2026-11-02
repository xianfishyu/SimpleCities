(() => {
  const draw = () => {
    if (!window.mermaid || !document.querySelector(".mermaid")) return;
    window.mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      theme: document.body.dataset.mdColorScheme === "slate" ? "dark" : "default",
      fontFamily: '"Segoe UI", "Microsoft YaHei", sans-serif'
    });
    window.mermaid.run({ querySelector: ".mermaid" }).catch(error => {
      document.querySelectorAll(".mermaid").forEach(node => node.dataset.renderError = error.message);
    });
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", draw);
  else draw();
})();
