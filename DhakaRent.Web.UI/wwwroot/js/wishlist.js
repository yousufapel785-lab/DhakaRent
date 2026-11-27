for (const form of document.querySelectorAll("[data-wishlist-form]")) {
    form.addEventListener("submit", async event => {
        event.preventDefault();
        const button = form.querySelector("button");
        if (button.disabled) return;
        const status = form.querySelector("[data-wishlist-feedback]");
        button.disabled = true;
        status.textContent = "";
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "application/json" }
            });
            if (response.redirected) {
                location.reload();
                return;
            }
            if (!response.headers.get("Content-Type")?.includes("application/json")) {
                throw new Error("Refresh the page and try again.");
            }
            const result = await response.json();
            if (!response.ok || typeof result.saved !== "boolean") {
                throw new Error(result.error || "Could not update your wishlist. Try again.");
            }
            button.setAttribute("aria-pressed", String(result.saved));
            button.querySelector("[data-wishlist-label]").textContent = result.saved ? "Saved to wishlist" : "Save to wishlist";
            form.action = result.saved ? form.dataset.remove : form.dataset.save;
        } catch (error) {
            status.textContent = error instanceof TypeError ? "Check your connection and try again." : error.message;
        } finally {
            button.disabled = false;
        }
    });
}
