// Fotogalerie: kliknutí na náhled otevře velkou fotku v <dialog>, šipky a swipe listují, Esc zavře.
(() => {
  for (const list of document.querySelectorAll(".gallery")) {
    const links = [...list.querySelectorAll("a")];
    const dialog = document.createElement("dialog");
    dialog.className = "lightbox";
    dialog.innerHTML =
      `<img alt="">` +
      `<button type="button" class="lb-close" aria-label="${list.dataset.close}">×</button>` +
      `<button type="button" class="lb-prev" aria-label="${list.dataset.prev}">‹</button>` +
      `<button type="button" class="lb-next" aria-label="${list.dataset.next}">›</button>`;
    document.body.append(dialog);
    const img = dialog.querySelector("img");
    let index = 0;

    const show = (i) => {
      index = (i + links.length) % links.length;
      img.src = links[index].href;
    };

    links.forEach((link, i) => link.addEventListener("click", (e) => {
      e.preventDefault();
      show(i);
      dialog.showModal();
    }));
    dialog.querySelector(".lb-close").addEventListener("click", () => dialog.close());
    dialog.querySelector(".lb-prev").addEventListener("click", () => show(index - 1));
    dialog.querySelector(".lb-next").addEventListener("click", () => show(index + 1));
    // klik mimo fotku (na ztmavené pozadí) zavře
    dialog.addEventListener("click", (e) => { if (e.target === dialog) dialog.close(); });
    dialog.addEventListener("keydown", (e) => {
      if (e.key === "ArrowLeft") show(index - 1);
      if (e.key === "ArrowRight") show(index + 1);
    });
    let startX = null;
    dialog.addEventListener("touchstart", (e) => { startX = e.touches[0].clientX; }, { passive: true });
    dialog.addEventListener("touchend", (e) => {
      if (startX === null) return;
      const dx = e.changedTouches[0].clientX - startX;
      if (Math.abs(dx) > 40) show(index + (dx < 0 ? 1 : -1));
      startX = null;
    });
  }
})();
