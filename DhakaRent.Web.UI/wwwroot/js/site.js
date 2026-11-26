(() => {
    const search = document.getElementById("rental-search");
    const prompt = document.getElementById("account-prompt");

    function openSearch() {
        search.showModal();
        search.querySelector("input").focus();
    }

    document.querySelectorAll("[data-open-search]").forEach(button => {
        button.addEventListener("click", openSearch);
    });
    if (search.dataset.invalid === "true") openSearch();

    document.querySelectorAll("[data-account-prompt]").forEach(button => {
        button.addEventListener("click", () => {
            prompt.querySelector("p").textContent =
                "Register or log in with a tenant account to " + button.dataset.accountPrompt + ".";
            prompt.querySelectorAll("a").forEach(link => {
                const url = new URL(link.href);
                url.searchParams.set("returnUrl", button.dataset.returnUrl);
                link.href = url.href;
            });
            prompt.showModal();
        });
    });

    document.querySelectorAll(".shell-dialog").forEach(dialog => {
        dialog.querySelector("[data-close-dialog]").addEventListener("click", () => dialog.close());
        dialog.addEventListener("click", event => {
            const box = dialog.getBoundingClientRect();
            if (event.target === dialog && (event.clientX < box.left || event.clientX > box.right ||
                event.clientY < box.top || event.clientY > box.bottom)) dialog.close();
        });
    });

    const back = document.querySelector("[data-go-back]");
    if (back && document.referrer && new URL(document.referrer).origin === location.origin &&
        document.referrer !== location.href && history.length > 1) {
        back.addEventListener("click", event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            history.back();
        });
    }
})();
