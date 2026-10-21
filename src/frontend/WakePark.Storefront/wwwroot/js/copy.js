// Tlačítka „Kopírovat“ u platebních údajů. Jediný skript webu — bez něj stránka funguje
// dál, tlačítka jsou jen skrytá (atribut hidden) a údaje se dají označit ručně.
document.querySelectorAll('button[data-copy]').forEach((button) => {
    if (!navigator.clipboard) {
        return;
    }

    const label = button.textContent;
    button.hidden = false;
    button.addEventListener('click', async () => {
        try {
            await navigator.clipboard.writeText(button.dataset.copy);
            button.textContent = button.dataset.copied;
        } catch {
            button.textContent = label;
            return;
        }
        setTimeout(() => { button.textContent = label; }, 2000);
    });
});
